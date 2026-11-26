# PassManager

A secure, offline password manager for Windows, built with .NET 10 (LTS) and WPF.
Your vault never leaves your computer: there is no cloud, no account and no telemetry.

## Features

- **Strong encryption**: Argon2id key derivation and AES‑256‑GCM authenticated encryption.
- **Entries**: title, username, password, website, category, notes and favorites, with instant search and category filters.
- **Password generator**: uses the operating system's cryptographically secure random generator, with configurable length and character sets and an option to avoid look‑alike characters.
- **Password strength meter**: runs offline and flags common passwords, keyboard patterns, repeats and leetspeak.
- **Auto‑lock**: after a period of inactivity, when Windows locks or the computer sleeps, and optionally on minimize.
- **Clipboard protection**: copied secrets are cleared automatically (default 20 s) and are kept out of Win+V clipboard history and cloud clipboard sync.
- **Screen‑capture protection** (optional): hides the windows from screenshots, recordings and screen sharing.
- **Change master password**: verifies the current password, then re‑encrypts the vault under a brand‑new key.
- **Crash‑safe storage**: writes are atomic, and a backup of the previous version is kept.
- **Keyboard shortcuts**:

  | Shortcut | Action |
  | --- | --- |
  | Ctrl+N | New entry |
  | Ctrl+E / Enter | Edit entry |
  | Del | Delete entry |
  | Ctrl+F | Search |
  | Ctrl+B | Copy username |
  | Ctrl+Shift+C | Copy password |
  | Ctrl+U | Open website |
  | Ctrl+G | Password generator |
  | Ctrl+L | Lock vault |

## Downloads (build output)

`build.ps1` writes these files to `artifacts/`:

| File | Purpose |
| --- | --- |
| `PassManager-<ver>-win-x64-setup.exe` | Recommended installer. Installs per user without admin rights; the user may choose "all users". |
| `PassManager-<ver>-win-x64.msi` | Windows Installer package for managed deployment (Intune, GPO, SCCM). Per‑machine; supports silent installs and major upgrades. |
| `PassManager-<ver>-win-x64-portable.exe` | Single self‑contained executable that needs no installation. |
| `SHA256SUMS.txt` | Checksums for verifying downloads. |

All three are self‑contained, so the target machine does not need the .NET runtime.
Supported systems: Windows 10 version 1809 or later, and Windows 11 (x64).

The vault is stored at `%LOCALAPPDATA%\PassManager\vault.pmv`. Back this file up; it is useless without the master password.
Uninstalling never deletes the vault unless you explicitly confirm it.

## Building

Prerequisites:

- .NET 10 SDK
- [Inno Setup 6](https://jrsoftware.org/isinfo.php), only needed for `setup.exe`

The WiX Toolset is restored automatically from NuGet.

```powershell
.\build.ps1 -Version 1.0.0          # tests, publish, setup.exe, .msi, checksums
.\build.ps1 -SkipInstallers         # tests and portable exe only
.\build.ps1 -SignCertificateThumbprint <thumbprint>   # Authenticode-sign all outputs
```

Day‑to‑day development:

```powershell
dotnet build
dotnet test --project tests\PassManager.Tests\PassManager.Tests.csproj
dotnet run --project src\PassManager.App
```

To develop against a throw‑away vault, set `PASSMANAGER_DATA_DIR` to another folder before starting the app.

## Architecture

```
src/
  PassManager.Core            Domain + application logic. No UI or platform dependencies.
    Security/                 SensitiveBytes, ProtectedKey, SecureString helpers
    Cryptography/             Key hierarchy (VaultCryptoService), algorithm abstractions
    Vault/                    VaultSession (state machine + CRUD), entries, ports (IVaultStore, ...)
    Passwords/                Generator, strength evaluator, master-password policy
    Settings/                 AppSettings, ISettingsService
  PassManager.Infrastructure  Implementations of the Core ports
    Cryptography/             Argon2id, AES-256-GCM, DPAPI memory protection
    Storage/                  Binary vault format, atomic file store, JSON content and settings
  PassManager.App             WPF (MVVM with CommunityToolkit.Mvvm + Microsoft DI)
    ViewModels/ Views/ Services/ Helpers/ Converters/ Themes/ Composition/
tests/
  PassManager.Tests           xUnit v3: crypto, file format, storage, vault session, passwords
installer/
  inno/                       setup.exe (Inno Setup)
  wix/                        .msi (WiX Toolset)
```

Dependencies point inward: App → Infrastructure → Core. `CompositionRoot` is the only place where interfaces are bound to implementations.

### How SOLID is applied

| Principle | Where |
| --- | --- |
| **Single responsibility** | Each class has one job. Key derivation, the cipher, the file codec, the file store and content serialization are separate. In the UI, clipboard, dialogs, auto‑lock and shell launching are separate services. |
| **Open/closed** | `VaultCryptoService` looks up algorithms by the ID stored in the vault header. Adding a new KDF or cipher means registering another `IKeyDerivationFunction` or `IAuthenticatedCipher`, with no changes to existing code. |
| **Liskov substitution** | Tests swap in `InMemoryVaultStore` and `XorMemoryProtector` for the real implementations without changing behavior. |
| **Interface segregation** | `VaultSession` exposes two narrow interfaces: `IVaultLifecycle` (create, unlock, lock, re‑key) and `IVaultEntries` (CRUD). Consumers depend only on what they use. |
| **Dependency inversion** | Core defines the ports (`IVaultStore`, `IVaultEnvelopeCodec`, `IMemoryProtector`, …). Infrastructure implements them, and DI wires them together. View models never touch WPF APIs directly; they use services such as `IDialogService`. |

## Security model

### Key hierarchy

```
master password ──Argon2id(salt, 64 MiB, t=3, p=4)──► key-encryption key (KEK, 256-bit)
                                                        │ AES-256-GCM (unwrap)
                                                        ▼
                                  random data key (DEK, 256-bit) ──AES-256-GCM──► vault payload (JSON)
```

- The DEK is random and generated by the operating system's CSPRNG.
- **Changing the master password generates a new salt *and* a new DEK** and re‑encrypts everything. It also deletes the backup that was encrypted under the old password. Someone who learned the old password therefore cannot decrypt newer versions.
- Vaults created with weaker KDF parameters are upgraded transparently to the current policy on the next unlock.

### Authenticated header

Every ciphertext carries associated data with a domain‑separation label:

- The **wrapped key** is bound to the format version, cipher, KDF algorithm and its cost parameters, and the salt.
- The **payload** is bound to all of the above plus the wrapped key.

Changing any header field, for example to downgrade the KDF cost, therefore makes decryption fail. A wrong master password and a tampered header are indistinguishable by design.

### File format v1

Little‑endian, every field bounds‑checked when read:

```
"PMVAULT\0" | u16 version | u8 cipher | u8 kdf | i32 memoryKiB | i32 iterations | i32 parallelism
| u8+salt | wrapped key: u8+nonce, u8+tag, i32+ciphertext | payload: u8+nonce, u8+tag, i32+ciphertext
```

The reader rejects:

- truncated files and trailing data
- unknown versions or algorithms
- KDF costs outside a safe range, which prevents denial of service from a crafted file

### Secrets in memory

- **The master password never becomes a .NET `string`.** It goes from the `PasswordBox` as a `SecureString`, through unmanaged memory, into a pinned UTF‑8 buffer that is zeroed immediately after key derivation.
- Keys and decrypted payloads live in **pinned** buffers (`SensitiveBytes`, `ProtectedKey`). The garbage collector can't copy them around, and they are wiped deterministically.
- The DEK stays **encrypted in RAM with DPAPI** (`CryptProtectMemory`, same‑process scope) except during the moment it is in use.
- Locking wipes the key, discards all decrypted entries, closes every open dialog and clears the clipboard if it still holds a secret from the app.
- `VaultEntry.ToString()` omits secrets, so entries cannot leak into logs.

### Other hardening

- **Brute force**: the main defense is the Argon2id cost, which also applies offline. The unlock screen adds a growing cool‑down after 3 failed attempts.
- **Websites**: only `http` and `https` addresses can be opened. Entry content can never launch `file://` paths, UNC shares or custom protocol handlers.
- **Single instance**: two instances cannot write the same vault concurrently.
- **Code quality**: nullable reference types, .NET analyzers, and warnings treated as errors. All package versions are pinned centrally.

### Known limitations (threat model)

- **Malware running as your user** while the vault is unlocked can read process memory or the keyboard. No password manager can defend against this; keep the system clean.
- **Entry passwords are .NET strings while the vault is unlocked**, because WPF text controls and the clipboard require strings. They become eligible for garbage collection when the vault locks, but they cannot be zeroed deterministically.
- **Forgotten master password**: it cannot be recovered, by design.
- **Unsigned binaries**: Windows SmartScreen will warn about them. Sign release builds (`-SignCertificateThumbprint`) with an Authenticode certificate.

## Testing

```powershell
dotnet test --project tests\PassManager.Tests\PassManager.Tests.csproj
```

There are 87 tests. They include:

- The official Argon2id reference test vector (RFC 9106 reference implementation).
- AES‑GCM tamper, wrong‑key and wrong‑context rejection.
- Truncation fuzzing of the file format.
- Detection of KDF‑downgrade attacks.
- Transactional writes: a failed disk write leaves memory unchanged.
- Re‑keying and the transparent KDF upgrade.
- Password generator distribution and constraints, and URL safety rules.

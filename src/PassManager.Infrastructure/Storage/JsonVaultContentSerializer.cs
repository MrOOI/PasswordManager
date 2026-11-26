using System.Text.Json;
using System.Text.Json.Serialization;
using PassManager.Core.Vault;

namespace PassManager.Infrastructure.Storage;

/// <summary>
/// Serialises the decrypted vault content as compact UTF-8 JSON using source-generated (reflection-free)
/// metadata. DTOs keep the persisted schema independent from the domain model.
/// </summary>
public sealed class JsonVaultContentSerializer : IVaultContentSerializer
{
    public byte[] Serialize(VaultDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var dto = new VaultDocumentDto
        {
            SchemaVersion = document.SchemaVersion,
            Entries = document.Entries.Select(ToDto).ToList(),
        };

        return JsonSerializer.SerializeToUtf8Bytes(dto, VaultJsonContext.Default.VaultDocumentDto);
    }

    public VaultDocument Deserialize(ReadOnlySpan<byte> data)
    {
        VaultDocumentDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(data, VaultJsonContext.Default.VaultDocumentDto);
        }
        catch (JsonException ex)
        {
            throw new VaultCorruptedException("The vault content could not be read.", ex);
        }

        if (dto is null)
        {
            throw new VaultCorruptedException("The vault content is empty.");
        }

        if (dto.SchemaVersion > VaultDocument.CurrentSchemaVersion)
        {
            throw new UnsupportedVaultFormatException(
                $"This vault was created by a newer version of PassManager (schema {dto.SchemaVersion}). Please update the application.");
        }

        return new VaultDocument(dto.SchemaVersion, (dto.Entries ?? []).Select(FromDto).ToList());
    }

    private static VaultEntryDto ToDto(VaultEntry entry) => new()
    {
        Id = entry.Id,
        Title = entry.Title,
        Username = entry.Username,
        Password = entry.Password,
        Url = entry.Url,
        Notes = entry.Notes,
        Category = entry.Category,
        IsFavorite = entry.IsFavorite,
        CreatedAt = entry.CreatedAt,
        ModifiedAt = entry.ModifiedAt,
    };

    private static VaultEntry FromDto(VaultEntryDto dto)
    {
        if (dto.Id == Guid.Empty)
        {
            throw new VaultCorruptedException("The vault contains an entry without an identifier.");
        }

        return new VaultEntry
        {
            Id = dto.Id,
            Title = dto.Title ?? string.Empty,
            Username = dto.Username ?? string.Empty,
            Password = dto.Password ?? string.Empty,
            Url = dto.Url ?? string.Empty,
            Notes = dto.Notes ?? string.Empty,
            Category = dto.Category ?? string.Empty,
            IsFavorite = dto.IsFavorite,
            CreatedAt = dto.CreatedAt,
            ModifiedAt = dto.ModifiedAt,
        };
    }
}

internal sealed class VaultDocumentDto
{
    public int SchemaVersion { get; set; }

    public List<VaultEntryDto>? Entries { get; set; }
}

internal sealed class VaultEntryDto
{
    public Guid Id { get; set; }

    public string? Title { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? Url { get; set; }

    public string? Notes { get; set; }

    public string? Category { get; set; }

    public bool IsFavorite { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(VaultDocumentDto))]
internal sealed partial class VaultJsonContext : JsonSerializerContext;

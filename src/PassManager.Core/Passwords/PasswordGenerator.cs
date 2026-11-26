using System.Security.Cryptography;

namespace PassManager.Core.Passwords;

/// <summary>
/// Generates passwords with the operating system CSPRNG. Selection uses
/// <see cref="RandomNumberGenerator.GetItems{T}(ReadOnlySpan{T}, Span{T})"/>, which is free of modulo bias.
/// </summary>
public sealed class PasswordGenerator : IPasswordGenerator
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{};:,.<>/?~";
    private const string Ambiguous = "Il1O0o|";

    public string Generate(PasswordGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Length, PasswordGeneratorOptions.MinimumLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Length, PasswordGeneratorOptions.MaximumLength);

        List<string> sets = BuildCharacterSets(options);
        if (sets.Count == 0)
        {
            throw new ArgumentException("At least one character set must be selected.", nameof(options));
        }

        string alphabet = string.Concat(sets);
        char[] buffer = new char[options.Length];
        try
        {
            // Guarantee at least one character from every selected set, then fill and shuffle.
            for (int i = 0; i < sets.Count; i++)
            {
                buffer[i] = sets[i][RandomNumberGenerator.GetInt32(sets[i].Length)];
            }

            RandomNumberGenerator.GetItems<char>(alphabet, buffer.AsSpan(sets.Count));
            RandomNumberGenerator.Shuffle(buffer.AsSpan());
            return new string(buffer);
        }
        finally
        {
            Array.Clear(buffer);
        }
    }

    /// <summary>Theoretical entropy in bits of a password produced with <paramref name="options"/>.</summary>
    public static double EstimateEntropyBits(PasswordGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        int alphabetSize = BuildCharacterSets(options).Sum(s => s.Length);
        return alphabetSize == 0 ? 0 : options.Length * Math.Log2(alphabetSize);
    }

    private static List<string> BuildCharacterSets(PasswordGeneratorOptions options)
    {
        var sets = new List<string>(4);
        AddSet(sets, options.IncludeLowercase, Lowercase, options.ExcludeAmbiguous);
        AddSet(sets, options.IncludeUppercase, Uppercase, options.ExcludeAmbiguous);
        AddSet(sets, options.IncludeDigits, Digits, options.ExcludeAmbiguous);
        AddSet(sets, options.IncludeSymbols, Symbols, options.ExcludeAmbiguous);
        return sets;
    }

    private static void AddSet(List<string> sets, bool include, string characters, bool excludeAmbiguous)
    {
        if (!include)
        {
            return;
        }

        string set = excludeAmbiguous
            ? new string(characters.Where(c => !Ambiguous.Contains(c, StringComparison.Ordinal)).ToArray())
            : characters;

        if (set.Length > 0)
        {
            sets.Add(set);
        }
    }
}

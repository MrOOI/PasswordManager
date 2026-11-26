namespace PassManager.Core.Passwords;

public sealed record PasswordGeneratorOptions
{
    public const int MinimumLength = 4;
    public const int MaximumLength = 128;

    public int Length { get; init; } = 20;

    public bool IncludeLowercase { get; init; } = true;

    public bool IncludeUppercase { get; init; } = true;

    public bool IncludeDigits { get; init; } = true;

    public bool IncludeSymbols { get; init; } = true;

    /// <summary>Skip characters that are easy to confuse when read aloud or typed (I, l, 1, O, 0, ...).</summary>
    public bool ExcludeAmbiguous { get; init; }
}

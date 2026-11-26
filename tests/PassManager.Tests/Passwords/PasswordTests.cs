using PassManager.Core.Passwords;
using PassManager.Core.Vault;

namespace PassManager.Tests.Passwords;

public sealed class PasswordGeneratorTests
{
    private readonly PasswordGenerator _generator = new();

    [Theory]
    [InlineData(4)]
    [InlineData(20)]
    [InlineData(128)]
    public void Generates_requested_length(int length)
    {
        Assert.Equal(length, _generator.Generate(new PasswordGeneratorOptions { Length = length }).Length);
    }

    [Fact]
    public void Contains_every_selected_character_class()
    {
        for (int i = 0; i < 200; i++)
        {
            string password = _generator.Generate(new PasswordGeneratorOptions { Length = 8 });

            Assert.Contains(password, char.IsAsciiLetterLower);
            Assert.Contains(password, char.IsAsciiLetterUpper);
            Assert.Contains(password, char.IsAsciiDigit);
            Assert.Contains(password, c => !char.IsAsciiLetterOrDigit(c));
        }
    }

    [Fact]
    public void Respects_disabled_character_classes()
    {
        var options = new PasswordGeneratorOptions { Length = 64, IncludeUppercase = false, IncludeSymbols = false };

        string password = _generator.Generate(options);

        Assert.All(password, c => Assert.True(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)));
    }

    [Fact]
    public void Excludes_ambiguous_characters_when_requested()
    {
        var options = new PasswordGeneratorOptions { Length = 128, ExcludeAmbiguous = true };

        for (int i = 0; i < 50; i++)
        {
            Assert.DoesNotContain(_generator.Generate(options), c => "Il1O0o|".Contains(c, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Produces_unique_passwords()
    {
        var passwords = Enumerable.Range(0, 500).Select(_ => _generator.Generate(new PasswordGeneratorOptions())).ToHashSet();

        Assert.Equal(500, passwords.Count);
    }

    [Fact]
    public void Requires_at_least_one_character_class()
    {
        var options = new PasswordGeneratorOptions
        {
            IncludeLowercase = false,
            IncludeUppercase = false,
            IncludeDigits = false,
            IncludeSymbols = false,
        };

        Assert.Throws<ArgumentException>(() => _generator.Generate(options));
    }

    [Fact]
    public void Default_options_exceed_128_bits_of_entropy()
    {
        Assert.True(PasswordGenerator.EstimateEntropyBits(new PasswordGeneratorOptions()) > 128);
    }
}

public sealed class PasswordStrengthEvaluatorTests
{
    private readonly PasswordStrengthEvaluator _evaluator = new();

    [Theory]
    [InlineData("password")]
    [InlineData("Password123!")]
    [InlineData("P@ssw0rd")]
    [InlineData("qwerty123")]
    [InlineData("aaaaaaaaaaaaaaaa")]
    [InlineData("123456789012")]
    [InlineData("abcdefghijkl")]
    public void Common_or_patterned_passwords_are_weak(string password)
    {
        Assert.True(_evaluator.Evaluate(password).Strength <= PasswordStrength.Weak, password);
    }

    [Theory]
    [InlineData("v9#Lq2!xT7@mR4$w")]
    [InlineData("glacier-Tunnel-orbit-73-Maple")]
    public void Long_random_passwords_are_strong(string password)
    {
        Assert.True(_evaluator.Evaluate(password).Strength >= PasswordStrength.Strong, password);
    }

    [Fact]
    public void Empty_password_is_very_weak()
    {
        PasswordStrengthResult result = _evaluator.Evaluate(ReadOnlySpan<char>.Empty);

        Assert.Equal(PasswordStrength.VeryWeak, result.Strength);
        Assert.Equal(0, result.EntropyBits);
    }

    [Fact]
    public void Short_passwords_are_capped_at_weak()
    {
        Assert.True(_evaluator.Evaluate("x7#Kq").Strength <= PasswordStrength.Weak);
    }
}

public sealed class MasterPasswordPolicyTests
{
    private readonly MasterPasswordPolicy _policy = new(new PasswordStrengthEvaluator());

    [Theory]
    [InlineData("Sh0rt!pw")]
    [InlineData("password1234")]
    [InlineData("aaaaaaaaaaaaaaaaaaaa")]
    public void Rejects_short_or_weak_passwords(string password)
    {
        MasterPasswordValidation result = _policy.Validate(password);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Accepts_a_strong_passphrase()
    {
        Assert.True(_policy.Validate("Velvet-Comet-Harbor-2931").IsValid);
    }
}

public sealed class WebAddressTests
{
    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("http://intranet.local/login", "http://intranet.local/login")]
    [InlineData("https://github.com/settings", "https://github.com/settings")]
    public void Accepts_web_addresses(string input, string expected)
    {
        Assert.True(WebAddress.TryNormalize(input, out Uri? uri));
        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ftp://example.com")]
    [InlineData("\\\\server\\share")]
    [InlineData("has spaces.com")]
    [InlineData("")]
    public void Rejects_non_web_or_dangerous_addresses(string input)
    {
        Assert.False(WebAddress.TryNormalize(input, out _));
    }
}

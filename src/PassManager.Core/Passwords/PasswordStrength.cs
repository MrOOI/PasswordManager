namespace PassManager.Core.Passwords;

public enum PasswordStrength
{
    VeryWeak = 0,
    Weak = 1,
    Fair = 2,
    Strong = 3,
    VeryStrong = 4,
}

public sealed record PasswordStrengthResult(PasswordStrength Strength, double EntropyBits, string Feedback)
{
    public string Label => Strength switch
    {
        PasswordStrength.VeryWeak => "Very weak",
        PasswordStrength.Weak => "Weak",
        PasswordStrength.Fair => "Fair",
        PasswordStrength.Strong => "Strong",
        _ => "Very strong",
    };
}

public interface IPasswordStrengthEvaluator
{
    PasswordStrengthResult Evaluate(ReadOnlySpan<char> password);
}

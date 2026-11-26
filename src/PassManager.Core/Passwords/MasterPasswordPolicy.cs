namespace PassManager.Core.Passwords;

public sealed record MasterPasswordValidation(bool IsValid, PasswordStrengthResult Strength, IReadOnlyList<string> Errors);

public interface IMasterPasswordPolicy
{
    int MinimumLength { get; }

    MasterPasswordValidation Validate(ReadOnlySpan<char> password);
}

/// <summary>
/// Requirements for the master password. It is the only secret protecting the vault, so it must be
/// long and not trivially guessable; the KDF only multiplies the cost of each guess.
/// </summary>
public sealed class MasterPasswordPolicy : IMasterPasswordPolicy
{
    private readonly IPasswordStrengthEvaluator _evaluator;

    public MasterPasswordPolicy(IPasswordStrengthEvaluator evaluator)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    public int MinimumLength => 12;

    public PasswordStrength MinimumStrength => PasswordStrength.Fair;

    public MasterPasswordValidation Validate(ReadOnlySpan<char> password)
    {
        PasswordStrengthResult strength = _evaluator.Evaluate(password);
        var errors = new List<string>();

        if (password.Length < MinimumLength)
        {
            errors.Add($"The master password must be at least {MinimumLength} characters long.");
        }
        else if (strength.Strength < MinimumStrength)
        {
            errors.Add($"The master password is too weak. {strength.Feedback}");
        }

        return new MasterPasswordValidation(errors.Count == 0, strength, errors);
    }
}

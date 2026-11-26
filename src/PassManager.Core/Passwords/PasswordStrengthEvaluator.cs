namespace PassManager.Core.Passwords;

/// <summary>
/// Offline password strength estimator. Starts from the character-pool entropy and discounts
/// repetitions, sequences (abc, 321) and common passwords/words, including simple leetspeak.
/// Works on spans so it can evaluate secrets without allocating strings.
/// </summary>
public sealed class PasswordStrengthEvaluator : IPasswordStrengthEvaluator
{
    private const int StackAllocThreshold = 256;

    private static readonly string[] CommonWords =
    [
        "password", "passwort", "passw0rd", "qwerty", "qwertz", "azerty", "asdfgh", "zxcvbn", "qazwsx",
        "letmein", "welcome", "admin", "administrator", "iloveyou", "monkey", "dragon", "football",
        "baseball", "soccer", "hockey", "master", "login", "sunshine", "princess", "shadow", "superman",
        "batman", "trustno", "starwars", "whatever", "freedom", "hello", "secret", "computer", "internet",
        "google", "michael", "jessica", "charlie", "summer", "winter", "spring", "autumn", "changeme",
        "default", "access", "killer", "pokemon", "ninja", "mustang", "cookie", "flower", "family",
        "friend", "orange", "banana", "apple", "chocolate", "purple", "lovely", "angel", "jordan",
        "hunter", "ranger", "tigger", "matrix", "london", "paris", "berlin", "guest", "root", "user",
        "test", "pass", "love", "abc123", "1234", "123123", "111111", "000000", "1q2w3e", "abcd",
    ];

    public PasswordStrengthResult Evaluate(ReadOnlySpan<char> password)
    {
        if (password.IsEmpty)
        {
            return new PasswordStrengthResult(PasswordStrength.VeryWeak, 0, "Enter a password.");
        }

        CharacterClasses classes = Classify(password);
        int poolSize = classes.PoolSize;

        Span<char> lowered = password.Length <= StackAllocThreshold ? stackalloc char[password.Length] : new char[password.Length];
        Span<char> deLeeted = password.Length <= StackAllocThreshold ? stackalloc char[password.Length] : new char[password.Length];
        try
        {
            password.ToLowerInvariant(lowered);
            DeLeet(lowered, deLeeted);

            int longestCommonWord = Math.Max(LongestCommonWord(lowered), LongestCommonWord(deLeeted));
            bool isCommonPassword = IsEntirelyCommon(lowered) || IsEntirelyCommon(deLeeted);

            double effectiveLength = EffectiveLength(password, out bool hasPatterns);
            if (longestCommonWord > 0)
            {
                effectiveLength = Math.Max(1, effectiveLength - (longestCommonWord - 1));
            }

            double entropy = effectiveLength * Math.Log2(poolSize);
            PasswordStrength strength = isCommonPassword ? PasswordStrength.VeryWeak : Score(entropy);
            if (password.Length < 8 && strength > PasswordStrength.Weak)
            {
                strength = PasswordStrength.Weak;
            }

            string feedback = BuildFeedback(strength, password.Length, classes.Count, isCommonPassword, longestCommonWord > 0, hasPatterns);
            return new PasswordStrengthResult(strength, Math.Round(entropy, 1), feedback);
        }
        finally
        {
            lowered.Clear();
            deLeeted.Clear();
        }
    }

    private static PasswordStrength Score(double entropyBits) => entropyBits switch
    {
        < 28 => PasswordStrength.VeryWeak,
        < 45 => PasswordStrength.Weak,
        < 65 => PasswordStrength.Fair,
        < 90 => PasswordStrength.Strong,
        _ => PasswordStrength.VeryStrong,
    };

    private static string BuildFeedback(PasswordStrength strength, int length, int classCount, bool isCommon, bool hasCommonWord, bool hasPatterns)
    {
        if (isCommon)
        {
            return "This is one of the most commonly used passwords.";
        }

        if (strength >= PasswordStrength.Strong)
        {
            return strength == PasswordStrength.VeryStrong ? "Excellent password." : "Good password.";
        }

        if (hasCommonWord)
        {
            return "Avoid common words, names and keyboard patterns.";
        }

        if (length < 12)
        {
            return "Use at least 12 characters.";
        }

        if (hasPatterns)
        {
            return "Avoid repeated or sequential characters.";
        }

        return classCount < 3
            ? "Mix upper and lower case letters, digits and symbols."
            : "Make it longer for extra protection.";
    }

    private static double EffectiveLength(ReadOnlySpan<char> password, out bool hasPatterns)
    {
        hasPatterns = false;
        double length = 1;

        for (int i = 1; i < password.Length; i++)
        {
            char previous = password[i - 1];
            char current = password[i];

            if (current == previous)
            {
                length += 0.25;
                hasPatterns = true;
            }
            else if (IsSequential(previous, current))
            {
                length += 0.35;
                hasPatterns = true;
            }
            else
            {
                length += 1;
            }
        }

        return length;
    }

    private static bool IsSequential(char previous, char current)
    {
        bool bothDigits = char.IsAsciiDigit(previous) && char.IsAsciiDigit(current);
        bool bothLetters = char.IsAsciiLetter(previous) && char.IsAsciiLetter(current);
        if (!bothDigits && !bothLetters)
        {
            return false;
        }

        int delta = char.ToLowerInvariant(current) - char.ToLowerInvariant(previous);
        return delta is 1 or -1;
    }

    private static int LongestCommonWord(ReadOnlySpan<char> candidate)
    {
        int longest = 0;
        foreach (string word in CommonWords)
        {
            if (word.Length > longest && candidate.Contains(word, StringComparison.Ordinal))
            {
                longest = word.Length;
            }
        }

        return longest;
    }

    private static bool IsEntirelyCommon(ReadOnlySpan<char> candidate)
    {
        // "Password123!" is still "password": strip trailing digits and symbols before comparing.
        ReadOnlySpan<char> core = candidate.TrimEnd("0123456789!@#$%^&*?.-_ ");
        if (core.Length < 3)
        {
            core = candidate;
        }

        foreach (string word in CommonWords)
        {
            if (core.SequenceEqual(word))
            {
                return true;
            }
        }

        return false;
    }

    private static void DeLeet(ReadOnlySpan<char> source, Span<char> destination)
    {
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i] switch
            {
                '0' => 'o',
                '1' or '!' or '|' => 'i',
                '3' => 'e',
                '4' or '@' => 'a',
                '5' or '$' => 's',
                '7' => 't',
                '8' => 'b',
                char other => other,
            };
        }
    }

    private static CharacterClasses Classify(ReadOnlySpan<char> password)
    {
        bool lower = false, upper = false, digit = false, symbol = false, other = false;
        foreach (char c in password)
        {
            if (char.IsAsciiLetterLower(c))
            {
                lower = true;
            }
            else if (char.IsAsciiLetterUpper(c))
            {
                upper = true;
            }
            else if (char.IsAsciiDigit(c))
            {
                digit = true;
            }
            else if (c is >= ' ' and <= '~')
            {
                symbol = true;
            }
            else
            {
                other = true;
            }
        }

        int pool = (lower ? 26 : 0) + (upper ? 26 : 0) + (digit ? 10 : 0) + (symbol ? 33 : 0) + (other ? 100 : 0);
        int count = (lower ? 1 : 0) + (upper ? 1 : 0) + (digit ? 1 : 0) + (symbol ? 1 : 0) + (other ? 1 : 0);
        return new CharacterClasses(Math.Max(pool, 2), count);
    }

    private readonly record struct CharacterClasses(int PoolSize, int Count);
}

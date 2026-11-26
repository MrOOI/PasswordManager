namespace PassManager.Core.Passwords;

public interface IPasswordGenerator
{
    string Generate(PasswordGeneratorOptions options);
}

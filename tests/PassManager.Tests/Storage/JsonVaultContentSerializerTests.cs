using System.Text;
using PassManager.Core.Vault;
using PassManager.Infrastructure.Storage;

namespace PassManager.Tests.Storage;

public sealed class JsonVaultContentSerializerTests
{
    private readonly JsonVaultContentSerializer _serializer = new();

    [Fact]
    public void Serialize_then_deserialize_round_trips_entries()
    {
        var entry = new VaultEntry
        {
            Id = Guid.NewGuid(),
            Title = "GitHub",
            Username = "octocat",
            Password = "p@ss \"quoted\" ünicode 🔐",
            Url = "https://github.com",
            Notes = "line 1\nline 2",
            Category = "Dev",
            IsFavorite = true,
            CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            ModifiedAt = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero),
        };

        VaultDocument result = _serializer.Deserialize(_serializer.Serialize(new VaultDocument(1, [entry])));

        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal(entry, Assert.Single(result.Entries));
    }

    [Fact]
    public void Newer_schema_is_rejected()
    {
        byte[] json = Encoding.UTF8.GetBytes("""{"schemaVersion":99,"entries":[]}""");

        Assert.Throws<UnsupportedVaultFormatException>(() => _serializer.Deserialize(json));
    }

    [Fact]
    public void Malformed_content_is_reported_as_corruption()
    {
        Assert.Throws<VaultCorruptedException>(() => _serializer.Deserialize("{not json"u8));
    }

    [Fact]
    public void Entry_tostring_never_contains_the_password()
    {
        var entry = new VaultEntry { Id = Guid.NewGuid(), Title = "Bank", Password = "hunter2-SECRET" };

        Assert.DoesNotContain("hunter2-SECRET", entry.ToString(), StringComparison.Ordinal);
    }
}

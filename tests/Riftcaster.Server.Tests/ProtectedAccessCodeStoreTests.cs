using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Core.Network;

namespace Riftcaster.Server.Tests;

public sealed class ProtectedAccessCodeStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("riftcaster-tests-");

    private readonly string _path;

    private readonly EphemeralDataProtectionProvider _keys = new();

    public ProtectedAccessCodeStoreTests()
    {
        _path = Path.Combine(_directory.FullName, "data", "accessCode.json");
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    [Fact]
    public void Load_MissingFile_FindsNoCode()
    {
        Assert.Equal(new LoadedAccessCode(null, Unreadable: false), CreateStore(_keys).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var code = new StoredAccessCode("K7QM-4XPA", "v1");
        CreateStore(_keys).Save(code);

        Assert.Equal(new LoadedAccessCode(code), CreateStore(_keys).Load());
    }

    [Fact]
    public void Save_DoesNotWriteTheCodeAsPlainText()
    {
        CreateStore(_keys).Save(new StoredAccessCode("K7QM-4XPA", "v1"));

        Assert.DoesNotContain("K7QM", File.ReadAllText(_path));
    }

    [Fact]
    public void Save_Null_RemovesIt()
    {
        var store = CreateStore(_keys);
        store.Save(new StoredAccessCode("K7QM-4XPA", "v1"));

        store.Save(null);

        Assert.Equal(new LoadedAccessCode(null, Unreadable: false), CreateStore(_keys).Load());
    }

    [Fact]
    public void Load_WithOtherKeys_IsUnreadable()
    {
        // E.g. another computer or Windows account, or the keys were lost: say so, don't crash.
        CreateStore(_keys).Save(new StoredAccessCode("K7QM-4XPA", "v1"));

        Assert.Equal(new LoadedAccessCode(null, Unreadable: true), CreateStore(new EphemeralDataProtectionProvider()).Load());
    }

    private ProtectedAccessCodeStore CreateStore(IDataProtectionProvider keys) =>
        new(_path, keys, NullLogger<ProtectedAccessCodeStore>.Instance);
}

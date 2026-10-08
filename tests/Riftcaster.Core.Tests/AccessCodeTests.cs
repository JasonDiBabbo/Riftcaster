using Riftcaster.Core.Network;

namespace Riftcaster.Core.Tests;

public class AccessCodeTests
{
    private readonly InMemoryAccessCodeStore _store = new();

    private readonly AccessCode _accessCode;

    private int _changedCount;

    public AccessCodeTests()
    {
        _accessCode = new AccessCode(_store);
        _accessCode.Changed += () => _changedCount++;
    }

    [Fact]
    public void NothingSaved_IsNotSet()
    {
        Assert.False(_accessCode.IsSet);
        Assert.Null(_accessCode.Code);
        Assert.Null(_accessCode.Version);
    }

    [Fact]
    public void Set_SavesItWithAVersionAndRaisesChanged()
    {
        Assert.True(_accessCode.Set("  K7QM-4XPA "));

        Assert.True(_accessCode.IsSet);
        Assert.Equal("K7QM-4XPA", _accessCode.Code); // Trimmed, otherwise as typed
        Assert.NotNull(_accessCode.Version);
        Assert.Equal(new StoredAccessCode("K7QM-4XPA", _accessCode.Version), _store.Code);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Constructor_LoadsTheSavedCode()
    {
        _store.Save(new StoredAccessCode("K7QM-4XPA", "v1"));

        var accessCode = new AccessCode(_store);

        Assert.Equal(("K7QM-4XPA", "v1"), (accessCode.Code, accessCode.Version));
    }

    [Fact]
    public void Constructor_UnreadableSavedCode_GeneratesAndSavesANewOne()
    {
        var store = new UnreadableStore();

        var accessCode = new AccessCode(store);

        Assert.True(accessCode.IsSet);
        Assert.Matches("^[ACDEFGHJKMNPQRTUVWXY34679]{4}-[ACDEFGHJKMNPQRTUVWXY34679]{4}$", accessCode.Code); // As Generate makes them
        Assert.NotNull(accessCode.Version);
        Assert.Equal(new StoredAccessCode(accessCode.Code!, accessCode.Version), store.Saved);
    }

    [Theory]
    [InlineData("12345")] // Too short
    [InlineData("12-34 5")] // Dashes and spaces don't count
    [InlineData("")]
    public void Set_TooShort_IsRefused(string code)
    {
        Assert.False(_accessCode.Set(code));

        Assert.False(_accessCode.IsSet);
        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public void Set_TooLong_IsRefused()
    {
        Assert.False(_accessCode.Set(new string('A', AccessCode.MaxLength + 1)));
    }

    [Fact]
    public void Set_Again_ChangesTheVersion()
    {
        _accessCode.Set("K7QM-4XPA");
        var first = _accessCode.Version;

        _accessCode.Set("K7QM-4XPA"); // Even the same code: a new version signs everyone out

        Assert.NotEqual(first, _accessCode.Version);
    }

    [Fact]
    public void Remove_ClearsItSavesAndRaisesChanged()
    {
        _accessCode.Set("K7QM-4XPA");

        _accessCode.Remove();

        Assert.False(_accessCode.IsSet);
        Assert.Null(_accessCode.Version);
        Assert.Null(_store.Code);
        Assert.Equal(2, _changedCount);
    }

    [Fact]
    public void Remove_WhenNotSet_DoesNothing()
    {
        _accessCode.Remove();

        Assert.Equal(0, _changedCount);
    }

    [Theory]
    [InlineData("K7QM-4XPA", true)]
    [InlineData("k7qm4xpa", true)] // Case, spaces and dashes don't matter
    [InlineData(" K7QM 4XPA ", true)]
    [InlineData("K7QM-4XPB", false)]
    [InlineData("K7QM-4XP", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Matches(string? attempt, bool expected)
    {
        _accessCode.Set("K7QM-4XPA");

        Assert.Equal(expected, _accessCode.Matches(attempt));
    }

    [Fact]
    public void Matches_WhenNotSet_IsFalse()
    {
        Assert.False(_accessCode.Matches(""));
        Assert.False(_accessCode.Matches("anything"));
    }

    [Fact]
    public void Generate_IsTwoGroupsOfFourUnambiguousCharacters()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => AccessCode.Generate()).ToList();

        Assert.All(codes, code => Assert.Matches("^[ACDEFGHJKMNPQRTUVWXY34679]{4}-[ACDEFGHJKMNPQRTUVWXY34679]{4}$", code));
        Assert.True(codes.Distinct().Count() > 190); // Random, not a fixed sequence
        Assert.All(codes, code => Assert.True(new AccessCode(new InMemoryAccessCodeStore()).Set(code))); // Always long enough
    }

    // A store whose saved code was saved somewhere else, so it can't be read.
    private sealed class UnreadableStore : IAccessCodeStore
    {
        public StoredAccessCode? Saved { get; private set; }

        public LoadedAccessCode Load() => new(null, Unreadable: true);

        public void Save(StoredAccessCode? code) => Saved = code;
    }
}

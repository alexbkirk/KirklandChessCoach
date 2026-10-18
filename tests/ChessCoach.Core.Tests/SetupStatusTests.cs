using ChessCoach.Core.Setup;

namespace ChessCoach.Core.Tests;

public sealed class SetupStatusTests
{
    private static readonly IReadOnlyList<EngineComponent> NoEngines = [];

    [Fact]
    public void Setup_is_complete_only_with_a_username_every_engine_and_some_games()
    {
        Assert.True(new SetupStatus("xkirke", NoEngines, 1408).Complete);
        Assert.False(new SetupStatus(null, NoEngines, 1408).Complete);
        Assert.False(new SetupStatus("xkirke", NoEngines, 0).Complete);
        Assert.False(new SetupStatus("xkirke", EngineCatalog.All, 1408).Complete);
    }

    [Theory]
    [InlineData("xkirke", true)]
    [InlineData("Some_Player-1", true)]
    [InlineData("abc", true)]
    [InlineData("ab", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("has space", false)]
    [InlineData("name.dot", false)]
    [InlineData("x' OR '1'='1", false)]
    [InlineData("averyveryveryveryveryverylongname", false)]
    [InlineData(null, false)]
    public void Usernames_follow_the_chess_com_rule(string? name, bool valid)
    {
        Assert.Equal(valid, ChessComUsername.IsValid(name));
    }
}

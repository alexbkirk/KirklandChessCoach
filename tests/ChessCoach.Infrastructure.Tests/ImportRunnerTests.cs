
using ChessCoach.Core.Abstractions;
using ChessCoach.Core.Setup;
using ChessCoach.Infrastructure.Setup;

namespace ChessCoach.Infrastructure.Tests;

public sealed class ImportRunnerTests
{
    [Fact]
    public void A_progress_line_becomes_an_update()
    {
        var update = ImportRunner.ParseProgress("""{"stage": "analyze", "done": 12, "total": 40, "message": "Analysing game 12 of 40"}""");

        Assert.Equal(new ImportProgress("analyze", 12, 40, "Analysing game 12 of 40"), update);
    }

    [Theory]
    [InlineData("Downloaded month 3 of 9")]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("""{"done": 1}""")]
    public void Ordinary_output_and_broken_lines_are_not_progress(string line)
    {
        Assert.Null(ImportRunner.ParseProgress(line));
    }

    [Fact]
    public void Missing_numbers_default_to_zero_so_a_message_still_shows()
    {
        var update = ImportRunner.ParseProgress("""{"stage": "fetch", "message": "Checking chess.com"}""");

        Assert.Equal(new ImportProgress("fetch", 0, 0, "Checking chess.com"), update);
    }
}

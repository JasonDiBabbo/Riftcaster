using Bunit;
using Riftcaster.WebUI.Shared;

namespace Riftcaster.WebUI.Tests;

// A row's note sits under its control, and on a phone joins the description as another sentence.
public class SettingRowTests : BunitContext
{
    private string InlineNote(string note) =>
        Render<SettingRow>(parameters => parameters
            .Add(p => p.Title, "Saved catalog")
            .Add(p => p.Description, "What is saved.")
            .Add(p => p.Note, note))
            .Find(".description .note-inline").TextContent;

    [Fact]
    public void ANoteWithoutAnEnd_GetsAPeriodWhereItJoinsTheDescription()
    {
        Assert.Equal("Applies immediately.", InlineNote("Applies immediately"));
    }

    [Theory]
    [InlineData("Cards keep working while new downloads process.")]
    [InlineData("Applies at the next start!")]
    [InlineData("Applies when you switch?")]
    public void ANoteThatEndsItself_IsLeftAlone(string note)
    {
        Assert.Equal(note, InlineNote(note));
    }

    [Fact]
    public void TheNoteUnderTheControl_IsNeverChanged()
    {
        var row = Render<SettingRow>(parameters => parameters
            .Add(p => p.Title, "Saved catalog")
            .Add(p => p.Description, "What is saved.")
            .Add(p => p.Note, "Cards keep working."));

        Assert.Equal("Cards keep working.", row.Find(".control .note").TextContent);
    }
}

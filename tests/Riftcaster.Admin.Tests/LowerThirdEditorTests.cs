using Bunit;
using Riftcaster.Admin.LowerThird;
using Riftcaster.Contracts;

namespace Riftcaster.Admin.Tests;

public class LowerThirdEditorTests : BunitContext
{
    private LowerThirdMessage? _saved;

    [Fact]
    public void Keyword_Saved_Trimmed()
    {
        var editor = RenderEditor();

        editor.Find("input[aria-label=Keyword]").Change("  Burn ");
        editor.Find("textarea[aria-label=Description]").Change(" Send cards to the trash. ");
        editor.Find("form").Submit();

        Assert.Equal(new LowerThirdKeywordMessage("Burn", "Send cards to the trash."), _saved);
    }

    [Fact]
    public void Keyword_MissingDescription_IsNotSaved()
    {
        var editor = RenderEditor();

        editor.Find("input[aria-label=Keyword]").Change("Burn");
        editor.Find("form").Submit();

        Assert.Null(_saved);
        Assert.NotEmpty(editor.FindAll(".validation-message"));
    }

    [Fact]
    public void Information_Saved()
    {
        var editor = RenderEditor();

        SegmentButton(editor, "Information").Click();
        editor.Find("textarea[aria-label=Message]").Change("Top 8 cut begins at 3:00 PM.");
        editor.Find("form").Submit();

        Assert.Equal(new LowerThirdInformationMessage("Top 8 cut begins at 3:00 PM."), _saved);
    }

    [Fact]
    public void Socials_EmptyRowsAreDropped()
    {
        var editor = RenderEditor();
        SegmentButton(editor, "Socials").Click();

        editor.Find("input[aria-label=Handle]").Change("riftcaster");
        editor.Find("button.add").Click(); // A second row, left empty
        editor.Find("form").Submit();

        var socials = Assert.IsType<LowerThirdSocialsMessage>(_saved);
        Assert.Equal([new SocialLink(SocialNetwork.Twitch, "riftcaster")], socials.Links);
    }

    [Fact]
    public void Socials_NoHandles_IsNotSaved()
    {
        var editor = RenderEditor();
        SegmentButton(editor, "Socials").Click();

        editor.Find("form").Submit();

        Assert.Null(_saved);
        Assert.Contains("Add at least one handle.", editor.Find(".validation-message").TextContent);
    }

    [Fact]
    public void Socials_RemovingTheLastRow_ClearsIt()
    {
        var editor = RenderEditor();
        SegmentButton(editor, "Socials").Click();
        editor.Find("input[aria-label=Handle]").Change("riftcaster");

        editor.Find("button[aria-label=Remove]").Click();

        Assert.Single(editor.FindAll(".social-row")); // Always a row to type in
        Assert.Equal("", editor.Find("input[aria-label=Handle]").GetAttribute("value"));
    }

    [Fact]
    public void Editing_StartsWithTheEntrysTypeAndValues()
    {
        var entry = new LowerThirdEntry(Guid.NewGuid(), new LowerThirdInformationMessage("Back in 5"));

        var editor = RenderEditor(entry);

        Assert.Contains("Edit lower third", editor.Markup);
        Assert.Equal("Back in 5", editor.Find("textarea[aria-label=Message]").GetAttribute("value"));
    }

    [Fact]
    public void Cancel_RaisesOnCancel()
    {
        var cancelled = false;
        var editor = Render<LowerThirdEditor>(parameters => parameters.Add(e => e.OnCancel, () => cancelled = true));

        editor.Find("button.cancel").Click();

        Assert.True(cancelled);
    }

    private IRenderedComponent<LowerThirdEditor> RenderEditor(LowerThirdEntry? entry = null) =>
        Render<LowerThirdEditor>(parameters => parameters
            .Add(e => e.Entry, entry)
            .Add(e => e.OnSave, (LowerThirdMessage message) => _saved = message));

    private static AngleSharp.Dom.IElement SegmentButton(IRenderedComponent<LowerThirdEditor> editor, string label) =>
        editor.FindAll("button").Single(button => button.TextContent.Trim() == label);
}

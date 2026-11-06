using Bunit;
using Microsoft.AspNetCore.Components;
using Riftcaster.WebUI.Shared;

namespace Riftcaster.WebUI.Tests;

// The question that has to be answered before anything else on the page can be used.
public class ConfirmDialogTests : BunitContext
{
    private readonly BunitJSModuleInterop _script;

    private int _confirmed;

    private int _cancelled;

    public ConfirmDialogTests()
    {
        _script = JSInterop.SetupModule("./_content/Riftcaster.WebUI/Shared/ConfirmDialog.razor.js");
        _script.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<ConfirmDialog> RenderDialog(bool open) =>
        Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Open, open)
            .Add(p => p.Title, "Switch?")
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "It will be deleted.")))
            .Add(p => p.ConfirmText, "Switch it")
            .Add(p => p.CancelText, "Keep it")
            .Add(p => p.OnConfirm, EventCallback.Factory.Create(this, () => _confirmed++))
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () => _cancelled++)));

    [Fact]
    public void WhileNotOpen_ItRendersNothing_AndCallsNoScript()
    {
        var dialog = RenderDialog(open: false);

        Assert.Empty(dialog.Markup.Trim());
        Assert.Empty(_script.Invocations);
    }

    [Fact]
    public void WhenOpen_ItIsAnAlertDialogWithItsTitleTextAndButtons_MadeModalByScript()
    {
        var dialog = RenderDialog(open: true);

        var element = dialog.Find("dialog");
        Assert.Equal("alertdialog", element.GetAttribute("role"));
        Assert.True(element.HasAttribute("open")); // Still shows if the script can't run
        Assert.Equal("Switch?", dialog.Find(".title").TextContent);
        Assert.Equal("It will be deleted.", dialog.Find(".text").TextContent);
        Assert.Equal("Keep it", dialog.Find(".button-secondary").TextContent);
        Assert.Equal("Switch it", dialog.Find(".confirm-button").TextContent);
        Assert.Equal(element.GetAttribute("aria-labelledby"), dialog.Find(".title").Id);
        Assert.Equal(element.GetAttribute("aria-describedby"), dialog.Find(".text").Id);
        Assert.Single(_script.Invocations["show"]);
    }

    [Fact]
    public void TheButtons_ReportTheAnswer()
    {
        var dialog = RenderDialog(open: true);

        dialog.Find(".confirm-button").Click();
        dialog.Find(".button-secondary").Click();

        Assert.Equal(1, _confirmed);
        Assert.Equal(1, _cancelled);
    }

    [Fact]
    public void ReRenderingWhileOpen_DoesNotOpenItAgain_ButOpeningAgainDoes()
    {
        var dialog = RenderDialog(open: true);

        dialog.Render();
        Assert.Single(_script.Invocations["show"]);

        dialog.Render(parameters => parameters.Add(p => p.Open, false));
        dialog.Render(parameters => parameters.Add(p => p.Open, true));

        Assert.Equal(2, _script.Invocations["show"].Count());
    }
}

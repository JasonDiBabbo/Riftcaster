using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Riftcaster.Admin.Timer;
using Riftcaster.Core.Match;
using Riftcaster.Core.Timer;

namespace Riftcaster.Admin.Tests;

public sealed class TimerPanelTests : BunitContext
{
    private readonly FakeTimeProvider _time = new();

    private readonly TimerService _timer;

    public TimerPanelTests()
    {
        _timer = new TimerService(new MatchService(new InMemoryMatchStore()), _time);
        Services.AddSingleton(_timer);
    }

    protected override void Dispose(bool disposing)
    {
        _timer.Dispose();
        base.Dispose(disposing);
    }

    [Fact]
    public void StartPauseResumeReset_ControlTheTimer()
    {
        var panel = Render<TimerPanel>();
        Assert.Equal("Ready", Status(panel));

        panel.Find(".start").Click();
        Assert.True(_timer.State.Running);
        panel.WaitForAssertion(() => Assert.Equal("Pause", panel.Find(".start").TextContent.Trim()));
        Assert.Equal("Running", Status(panel));

        _time.Advance(TimeSpan.FromSeconds(5));
        panel.Find(".start").Click();
        Assert.False(_timer.State.Running);
        Assert.Equal("Resume", panel.Find(".start").TextContent.Trim());
        Assert.Equal("Paused", Status(panel));

        panel.Find(".reset").Click();
        Assert.Equal("Start", panel.Find(".start").TextContent.Trim());
    }

    [Fact]
    public void AdjustButtons_ChangeTheLength()
    {
        var panel = Render<TimerPanel>();
        var before = _timer.State.TotalSeconds;

        panel.FindAll(".adjust button").Single(button => button.TextContent.Trim() == "+1m").Click();

        Assert.Equal(before + 60, _timer.State.TotalSeconds);
    }

    [Fact]
    public void SetRemaining_ValidTime_SetsTheTimerAndClearsTheBox()
    {
        var panel = Render<TimerPanel>();

        SetBox(panel).Input("1:30");
        panel.Find("form.set").Submit();

        Assert.Equal(90, _timer.State.TotalSeconds);
        Assert.Equal("", SetBox(panel).GetAttribute("value"));
        Assert.False(IsMarked(panel));
    }

    [Fact]
    public void SetRemaining_InvalidTime_IsMarkedOnSet()
    {
        var panel = Render<TimerPanel>();

        SetBox(panel).Input("abc");
        panel.Find("form.set").Submit();

        Assert.True(IsMarked(panel));
        Assert.Equal("abc", SetBox(panel).GetAttribute("value")); // Kept, to fix
    }

    [Fact]
    public void InvalidTime_IsMarkedWhenTheBoxLosesFocus_NotWhileTyping()
    {
        var panel = Render<TimerPanel>();

        SetBox(panel).Input("abc");
        Assert.False(IsMarked(panel));

        SetBox(panel).Blur();
        Assert.True(IsMarked(panel));
    }

    [Theory]
    [InlineData("abc", "Input time must use mm:ss formatting, or be a whole number of minutes.")]
    [InlineData("12:75", "Input time must use mm:ss formatting, or be a whole number of minutes.")]
    [InlineData("-5", "Input time must not be negative.")]
    [InlineData("1000", "Input time must be no greater than 999:59.")]
    public void MarkedTime_SaysWhatsWrong(string typed, string message)
    {
        var panel = Render<TimerPanel>();

        SetBox(panel).Input(typed);
        SetBox(panel).Blur();

        Assert.True(IsMarked(panel));
        Assert.Equal(message, panel.Find(".set-field .validation-message").TextContent);
    }

    [Fact]
    public void MarkedTime_MessageFollowsTheMistake()
    {
        var panel = Render<TimerPanel>();
        SetBox(panel).Input("-5");
        SetBox(panel).Blur();

        SetBox(panel).Input("1000"); // Still wrong, differently

        Assert.Equal("Input time must be no greater than 999:59.", panel.Find(".set-field .validation-message").TextContent);
    }

    [Theory]
    [InlineData("2:00")] // Fixed
    [InlineData("")] // Emptied
    public void MarkedTime_ClearsAsSoonAsItsFixed(string typed)
    {
        var panel = Render<TimerPanel>();
        SetBox(panel).Input("abc");
        SetBox(panel).Blur();

        SetBox(panel).Input(typed);

        Assert.False(IsMarked(panel));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyBox_IsNotMarkedWhenItLosesFocus(string typed)
    {
        var panel = Render<TimerPanel>();

        SetBox(panel).Input(typed);
        SetBox(panel).Blur();

        Assert.False(IsMarked(panel));
    }

    private static string Status(IRenderedComponent<TimerPanel> panel) => panel.Find(".status").TextContent.Trim();

    private static AngleSharp.Dom.IElement SetBox(IRenderedComponent<TimerPanel> panel) => panel.Find("form.set input");

    // Marked red, and explained underneath (tied to the box for screen readers), or neither.
    private static bool IsMarked(IRenderedComponent<TimerPanel> panel)
    {
        var box = SetBox(panel);
        var errors = panel.FindAll(".set-field .validation-message");
        var marked = box.ClassList.Contains("invalid") && box.GetAttribute("aria-invalid") == "true";
        Assert.Equal(marked, errors.Count == 1);
        if (marked)
        {
            Assert.Equal(errors[0].Id, box.GetAttribute("aria-describedby"));
            Assert.StartsWith("Input time must", errors[0].TextContent);
        }
        else
        {
            Assert.Null(box.GetAttribute("aria-describedby"));
        }

        return marked;
    }
}

using Bunit;
using Riftcaster.Admin.Shared;

namespace Riftcaster.Admin.Tests;

public class StepperTests : BunitContext
{
    private int? _changedTo;

    [Theory]
    [InlineData(3, 5, 5)] // Up to the next multiple of the step, like a browser's number input
    [InlineData(5, 5, 10)]
    [InlineData(0, 1, 1)]
    public void Increase_MovesUpToTheNextStep(int value, int step, int expected)
    {
        var stepper = RenderStepper(value, step: step);

        stepper.Find("button[aria-label='Increase Points']").Click();

        Assert.Equal(expected, _changedTo);
    }

    [Theory]
    [InlineData(7, 5, 5)]
    [InlineData(10, 5, 5)]
    public void Decrease_MovesDownToThePreviousStep(int value, int step, int expected)
    {
        var stepper = RenderStepper(value, step: step);

        stepper.Find("button[aria-label='Decrease Points']").Click();

        Assert.Equal(expected, _changedTo);
    }

    [Fact]
    public void Increase_AtTheMaximum_DoesNothing()
    {
        var stepper = RenderStepper(8, max: 8);

        stepper.Find("button[aria-label='Increase Points']").Click();

        Assert.Null(_changedTo);
    }

    [Fact]
    public void Decrease_AtTheMinimum_DoesNothing()
    {
        var stepper = RenderStepper(0);

        stepper.Find("button[aria-label='Decrease Points']").Click();

        Assert.Null(_changedTo);
    }

    [Theory]
    [InlineData("6", 6)]
    [InlineData("42", 8)] // Clamped to the maximum
    [InlineData("-3", 0)] // Clamped to the minimum
    public void Typing_SetsTheValueWithinTheLimits(string typed, int expected)
    {
        var stepper = RenderStepper(2, max: 8);

        stepper.Find("input").Change(typed);

        Assert.Equal(expected, _changedTo);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("2.5")]
    [InlineData("")]
    public void Typing_NotAWholeNumber_ChangesNothing(string typed)
    {
        var stepper = RenderStepper(2, max: 8);

        stepper.Find("input").Change(typed);

        Assert.Null(_changedTo);
        Assert.Equal("2", stepper.Find("input").GetAttribute("value")); // Shows the current value again
    }

    private IRenderedComponent<Stepper> RenderStepper(int value, int max = int.MaxValue, int step = 1) =>
        Render<Stepper>(parameters => parameters
            .Add(stepper => stepper.Value, value)
            .Add(stepper => stepper.Max, max)
            .Add(stepper => stepper.Step, step)
            .Add(stepper => stepper.Label, "Points")
            .Add(stepper => stepper.ValueChanged, (int changed) => _changedTo = changed));
}

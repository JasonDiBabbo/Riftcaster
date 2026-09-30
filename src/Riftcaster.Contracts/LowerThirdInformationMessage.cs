namespace Riftcaster.Contracts;

/// <summary>
/// A lower third that shows a short piece of information, such as a schedule note or an announcement.
/// </summary>
/// <param name="Message">The text to show.</param>
public record LowerThirdInformationMessage(string Message) : LowerThirdMessage;

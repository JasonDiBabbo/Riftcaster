namespace Riftcaster.Contracts;

/// <summary>
/// What can be changed on the Settings page. One set for the whole Riftcaster installation: every
/// operator, on every device, sees and changes the same settings. Nothing is kept per operator.
/// Each part of the page has its own record, so a part can be added without touching the others.
/// </summary>
/// <remarks>
/// Not <c>appsettings.json</c>, the developer's configuration file: these are what the page
/// changes while Riftcaster runs, and they sit on top of that file (see LogDetailSource).
/// <para>
/// Properties with defaults, not constructor parameters, so a settings file saved by an earlier
/// version (without a part added since) still loads: the new part starts at its defaults.
/// </para>
/// </remarks>
public record AppSettings
{
    /// <summary>How much the server writes to its log.</summary>
    public LoggingSettings Logging { get; init; } = new();

    /// <summary>
    /// The rules that Reset to defaults on the Match setup page gives the current match. Null until
    /// someone changes them: the design's defaults then apply (see MatchRules.Default), so a copy that
    /// never customized them follows any change to those.
    /// </summary>
    public MatchSettings? MatchDefaults { get; init; }

    /// <summary>The port the server listens on from the next start.</summary>
    public ServerSettings Server { get; init; } = new();

    /// <summary>What Riftcaster does about a newer version, and which ones it looks for.</summary>
    public UpdateSettings Updates { get; init; } = new();
}

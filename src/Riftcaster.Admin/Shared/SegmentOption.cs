namespace Riftcaster.Admin.Shared;

/// <summary>
/// A choice in a segmented control.
/// </summary>
/// <typeparam name="TValue">The type of the value presented</typeparam>
/// <param name="Value">The option value</param>
/// <param name="Label">The option label</param>
public sealed record SegmentOption<TValue>(TValue Value, string Label);

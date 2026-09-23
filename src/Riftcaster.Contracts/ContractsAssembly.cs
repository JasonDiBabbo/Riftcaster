namespace Riftcaster.Contracts;

/// <summary>
/// Marker for locating this assembly via <c>typeof(ContractsAssembly).Assembly</c>, so tooling
/// such as Riftcaster.Codegen doesn't depend on any particular DTO existing. Not a wire type;
/// codegen skips static classes.
/// </summary>
public static class ContractsAssembly;

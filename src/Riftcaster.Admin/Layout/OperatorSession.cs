namespace Riftcaster.Admin.Layout;

/// <summary>
/// Who is using this admin page: an operator at the computer running the server, or a remote
/// operator signed in with the access code (issue #16). Cascaded from <see cref="Routes"/> to
/// every component.
/// </summary>
/// <remarks>
/// Decided on the server from the page request's connection, in <see cref="App"/>, and handed to
/// the interactive components as a parameter, which Blazor encrypts and signs, so the browser
/// can't change it.
/// </remarks>
/// <param name="IsLocal">Whether the page was opened on the computer running the server.</param>
/// <param name="CodeVersion">
/// The access code's version when the page was opened (a remote operator's sign-in used it), or
/// <see langword="null"/> if none was set.
/// </param>
public sealed record OperatorSession(bool IsLocal, string? CodeVersion);

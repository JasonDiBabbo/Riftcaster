// @ts-check
/**
 * The line coverage each C# source file must keep, checked by check-coverage.mjs in CI (issue #18).
 *
 * Each file is checked on its own, against the first rule that matches it: a single number for
 * everything would let well-tested files hide untested ones. `match` is a path from the repository
 * root, either one file or a folder ending in "/". Specific files come before their folder.
 *
 * Every source file must match a rule, so a new file gets a deliberate minimum, and every rule must
 * match a file, so the list can't go stale. Minimums sit a little below today's numbers (rounded
 * down to a multiple of 5, and the lower of Debug and Release, which count lines differently), so a
 * real drop fails but measuring noise doesn't. Raise them as tests are added; never lower one
 * without saying why in `why`.
 *
 * @type {{ match: string, min: number, why: string }[]}
 */
export const minimums = [
  // --- Core: the rules the show runs on. Thoroughly unit-tested; keep it that way. ---
  {
    match: 'src/Riftcaster.Core/Storage/JsonDocumentFile.cs',
    min: 80,
    why: 'Its untested lines handle a locked or unreadable disk, which a unit test cannot cause.',
  },
  {
    match: 'src/Riftcaster.Core/',
    min: 90,
    why: 'The services and rules the show depends on, all unit-tested. Most files are at 100%.',
  },

  // --- Contracts: the shapes sent to the overlays. ---
  {
    match: 'src/Riftcaster.Contracts/',
    min: 100,
    why: 'Plain records the services and server tests create; a gap means a contract no test touches.',
  },

  // --- Server: tested over HTTP and WebSockets by the integration tests. ---
  {
    match: 'src/Riftcaster.Server/Program.cs',
    min: 85,
    why: 'Startup wiring. The untested lines report an unknown --network value and no network address.',
  },
  {
    match: 'src/Riftcaster.Server/StateSockets.cs',
    min: 75,
    why: 'The untested lines handle an overlay dropping mid-send and a close that never completes.',
  },
  {
    match: 'src/Riftcaster.Server/OverlayHostingExtensions.cs',
    min: 65,
    why: 'The untested lines warn when the overlays folder is missing or not configured; see #48.',
  },
  {
    match: 'src/Riftcaster.Server/',
    min: 90,
    why: 'Endpoints, sign-in and the network switch, covered by the integration tests.',
  },

  // --- Admin: Blazor components, tested with bUnit since #18. Panels added before then have little
  // or no component test yet; their minimums record where they are, to rise as tests are added. ---
  {
    match: 'src/Riftcaster.Admin/Layout/OnAirPill.razor',
    min: 0,
    why: 'Not tested yet: a label and a clear button, used by the header.',
  },
  {
    match: 'src/Riftcaster.Admin/Layout/AdminHeader.razor',
    min: 70,
    why: 'Not tested directly yet; rendered through the server tests. The pills inside it are tested.',
  },
  {
    match: 'src/Riftcaster.Admin/LowerThird/LowerThirdListItem.razor',
    min: 0,
    why: 'Not tested yet: one saved lower third, with Edit, Delete and Show buttons.',
  },
  {
    match: 'src/Riftcaster.Admin/LowerThird/LowerThirdPanel.razor',
    min: 20,
    why: 'Not tested yet: a thin layer over LowerThirdService, which is tested. Its editor is tested.',
  },
  {
    match: 'src/Riftcaster.Admin/LowerThird/LowerThirdSummary.cs',
    min: 0,
    why: 'Not tested yet: how each kind of lower third is labelled in the list.',
  },
  {
    match: 'src/Riftcaster.Admin/Match/MatchPanel.razor',
    min: 35,
    why: 'Not tested yet: controls over MatchService, which is tested.',
  },
  {
    match: 'src/Riftcaster.Admin/Pages/Home.razor',
    min: 65,
    why: 'The page layout: which panels go where. Rendered through the server tests.',
  },
  {
    match: 'src/Riftcaster.Admin/Players/PlayerCard.razor',
    min: 40,
    why: 'Not tested yet. Its card select and champion filter are tested on their own.',
  },
  {
    match: 'src/Riftcaster.Admin/Players/PlayerLabels.cs',
    min: 60,
    why: 'Not tested yet: player names, sides, colours and team names per match mode.',
  },
  {
    match: 'src/Riftcaster.Admin/Players/PlayersPanel.razor',
    min: 60,
    why: 'Not tested yet: lays out the player cards per match mode.',
  },
  {
    match: 'src/Riftcaster.Admin/Players/ScoreStrip.razor',
    min: 70,
    why: 'Not tested yet: the scores summary above the player cards.',
  },
  {
    match: 'src/Riftcaster.Admin/Players/TeamHeader.razor',
    min: 0,
    why: 'Not tested yet: only shown in 2v2.',
  },
  {
    match: 'src/Riftcaster.Admin/Timer/TimerPanel.razor',
    min: 55,
    why: 'Not tested yet: controls over TimerService, which is tested, and a redraw loop.',
  },
  {
    match: 'src/Riftcaster.Admin/',
    min: 80,
    why: 'Components and helpers with bUnit or unit tests. A new component should come with them.',
  },
];

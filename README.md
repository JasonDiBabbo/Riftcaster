# Riftcaster

Live, data-driven stream overlays for Riftbound, controlled from a browser-based admin dashboard.

Riftcaster runs as a single server process on the same machine as OBS. It serves three things:

- **Overlay pages.** Each overlay element is its own page, added to OBS as a Browser Source, so OBS's own scene editor handles layout and positioning.
- **The admin dashboard.** A Blazor web app for controlling what the overlays show. It's meant to be opened from any device on the network (desktop, laptop, tablet, phone) once network access and an access code are set up; see below.
- **An API:** live streams the overlays read their data from, and a REST API for controlling the show from Stream Deck, Bitfocus Companion or scripts (see "The REST API").

## Tech stack

| Layer | Technology |
| --- | --- |
| Server | ASP.NET Core (.NET 10), C# |
| Admin dashboard | Blazor, Interactive Server render mode (C#/Razor) |
| Overlays | Plain HTML, CSS, and TypeScript, bundled with esbuild |
| Shared types | C# records in `Riftcaster.Contracts`, turned into TypeScript interfaces by [TypeGen](https://github.com/jburzynski/TypeGen) on every build |

## Repository layout

```
src/
├── Riftcaster.Server/      ASP.NET Core host: API, admin dashboard, overlay files, and the overlays build
├── Riftcaster.Admin/       Blazor components for the admin dashboard (hosted by Server)
├── Riftcaster.Core/        The services and rules: players, match, timer, cards, network access
├── Riftcaster.Contracts/   Shared DTOs; the source of truth for server ↔ overlay data shapes
└── overlays/               npm project: one folder per overlay under src/components/
                            (overlays.esproj only lists it in Visual Studio; it builds nothing)
tests/                      One test project per project above (see "Tests")
tools/
├── Riftcaster.Codegen/     Build-time tool: Contracts → src/overlays/src/generated/*.ts
├── coverage/               CI's per-file coverage check (see "Coverage")
└── publish/                CI's check of a published build (see "Running a published build")
Riftcaster.slnx             Solution file
Riftcaster.slnLaunch        Visual Studio launch profile (see "Developing in Visual Studio")
```

## Prerequisites

- **.NET SDK 10.0.401 or later** (pinned in `global.json`)
- **Node.js 22.18 or later**, used to build the overlays

## Build and run

```bash
dotnet build
dotnet run --project src/Riftcaster.Server
```

`dotnet build` builds everything, in order:

1. The .NET projects.
2. Regenerated TypeScript types in `src/overlays/src/generated/`.
3. The overlays' npm packages, installed when needed.
4. A typecheck, lint ([Oxlint](https://oxc.rs/docs/guide/usage/linter)) and format check (Prettier) of the overlays, then a bundle into `src/overlays/dist/`. Lint and formatting problems fail the build just like type errors.

Each step is skipped when its inputs haven't changed. The steps are MSBuild targets in `Riftcaster.Server.csproj`, so building from Visual Studio, Rider, or VS Code (with the C# Dev Kit extension) does exactly the same thing.

Once it's running:

| URL | What |
| --- | --- |
| http://localhost:5062/ | Admin dashboard |
| http://localhost:5062/api/info | Server identity (JSON) |
| http://localhost:5062/api/docs | REST API docs, where you can try each endpoint |
| http://localhost:5062/overlays/serverIdentity/serverIdentity.html | Example overlay |

To build only the .NET side (without Node), pass `-p:SkipOverlays=true`.

## Running a published build

To run Riftcaster on a computer without the source code, such as the one running OBS, publish it:

```bash
dotnet publish src/Riftcaster.Server --configuration Release --output publish
```

It builds first, so the publish always has the current code. Don't add `--no-build` (as CI does, straight after its own Release build): it would publish whatever Release build is already in `bin/`, however old.

The `publish` folder then holds everything the server needs, overlays included. Copy it to the other computer, which needs the [ASP.NET Core Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0) (not the whole SDK), and start the server from it:

```bash
dotnet Riftcaster.Server.dll
```

It runs as Production, on http://localhost:5062 (change it with `--urls`, for example `--urls http://localhost:5070`), with the same options as in development, such as `--network on`. There's no `.exe` yet: an unsigned one is blocked by Smart App Control, so that waits for code signing ([#1](https://github.com/JasonDiBabbo/Riftcaster_VNext/issues/1)).

- **Everything lives in the folder,** wherever the server is started from: its settings, its overlays (`overlays/`) and its saved state (`data/`, created on first use). Your own `data` folder is never published, so a published build starts with no players, lower thirds or access code.
- **The access code's encryption keys** are kept in the Windows user's profile, not the folder, so moving the folder to another computer or Windows user means setting the code again. The server says so in its console, and carries on without one.
- **If the overlays are missing,** the admin dashboard's header shows **Overlay files missing** in red where it normally counts connected overlays, and the console says where it looked.

CI publishes on every run, then checks the result with [`tools/publish/check-publish.mjs`](tools/publish/check-publish.mjs): it starts the published server from another folder and fetches the admin dashboard and every overlay page, with their scripts and stylesheets. To check a publish yourself:

```bash
node tools/publish/check-publish.mjs publish
```

## Tests

```bash
dotnet test
```

Four test suites:

| Project | What it tests |
| --- | --- |
| `tests/Riftcaster.Core.Tests` | The services and their rules (players, match, timer, cards, access code and so on), with in-memory stores |
| `tests/Riftcaster.Server.Tests` | The real server, started in memory with `WebApplicationFactory` and exercised over HTTP and WebSockets |
| `tests/Riftcaster.Admin.Tests` | The admin dashboard's Blazor components, rendered with [bUnit](https://bunit.dev): clicks, typing and what each shows |
| `src/overlays` (Vitest) | The overlays' logic, run by the build's `npm test` step |

bUnit renders components in memory, without a browser, so it checks the HTML a component produces and how it reacts to events, not browser behaviour such as layout or how a `<select>` keeps its selection.

[CI](.github/workflows/ci.yml) runs the same `dotnet build` and `dotnet test` on every push to `main` and every pull request, then checks code coverage and a published build.

### Coverage

Every C# source file must keep a minimum line coverage, set in [`tools/coverage/minimums.mjs`](tools/coverage/minimums.mjs). Files are checked one by one, so well-tested files can't hide untested ones.

- **Each project has one minimum** (Core and Server 90%, Contracts 100%, Admin 80%), which every file in it must meet. New files need no entry: they're held to their project's minimum automatically.
- **A few files have their own, lower minimum,** each with the reason: code that's hard to test, or panels not tested yet. These are exceptions, to delete once a file's tests bring it up to its project's minimum.
- **CI fails** if a file drops below its minimum, if a new project has no minimum, or if an exception names a file that no longer exists.

To check locally:

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
node tools/coverage/check-coverage.mjs TestResults
```

Delete `TestResults` between runs (it's ignored by Git), or the check also reads older reports. When tests are added, raise the minimums they lift; never lower one without saying why.

## Pre-commit checks

The first build points Git at the repository's hooks in `.githooks/` (by setting `core.hooksPath` in your clone's own config). Before each commit, the `pre-commit` hook runs the fast checks on staged files: C# whitespace formatting, plus Oxlint and the Prettier check for overlay files. It takes a couple of seconds. The slower checks (naming and style rules, typechecking, tests) stay in the build and CI. Skip the hook in an emergency with `git commit --no-verify`.

## Adding an overlay to OBS

1. Start the server.
2. In OBS, add a **Browser Source** and set its URL to the overlay's page, for example `http://localhost:5062/overlays/serverIdentity/serverIdentity.html`.
3. Set the source's width and height to the size of the overlay element. Overlay pages have transparent backgrounds.

After rebuilding an overlay, click **Refresh cache of current page** in the source's properties to load the new version.

## OBS on another computer (network access)

By default only the computer running the server can reach it. To use the overlays from OBS on another computer on the same network:

1. Turn on network access: click the **Local only** pill in the admin dashboard's header and switch on **Let other devices connect**, or start the server with `--network on` (for example `dotnet run --project src/Riftcaster.Server -- --network on`).
2. The network panel (and the server's console) shows this computer's network address, such as `http://192.168.1.20:5062`. In OBS on the other computer, use it in place of `http://localhost:5062`, for example `http://192.168.1.20:5062/overlays/timer/timer.html`.
3. The first time, **Windows Firewall** asks whether to allow the server on the network. Allow it on **private networks**. If the venue's network is set to Public in Windows, either change it to Private (Settings > Network & internet > the network's properties) or allow the server on public networks too.

Network access is off again every time the server starts. Switching it on or off briefly reconnects the admin dashboard and every overlay.

The server only answers requests addressed to this computer: `localhost`, an IP address, or the computer's own name (`{computer-name}` or `{computer-name}.local`). Requests addressed to any other name get a 400. That stops a website from reaching the server through DNS rebinding, a trick where the site points its own domain at this computer. To reach the server by another name, such as one from your router or a reverse proxy, add it to `HostNames` in `appsettings.json`, separated by semicolons: `"HostNames": "riftcaster.lan;obs.example.com"`.

## The admin dashboard on other devices (access code)

Other devices can always open the overlays while network access is on. The admin dashboard, which controls the broadcast, needs an **access code**: without one, it's on the computer running the server only.

1. On the computer running the server, open the network panel (the pill in the admin dashboard's header), and under **Access code** type a code or press **Generate**, then **Set**. **Show** reveals it again later, to read out to someone.
2. Turn on network access. Operators open the admin dashboard address the panel shows, such as `http://192.168.1.20:5062/`, on their phone or laptop, and enter the code once. Case, spaces and dashes don't matter.
3. Their device stays signed in until the code changes. **Change** or **Remove** the code (for example after an event) to sign every device out at once, including admin dashboard pages they have open.

Only the computer running the server can switch network access or change the code; signed-in devices can use everything else, and sign out from the network panel. Wrong codes are limited to a few attempts a minute per device. The code is saved encrypted in `data/accessCode.json`.

The connection is plain HTTP, so the code stops casual tampering by others on the network, but not someone capturing its traffic. Changing the code after each event limits that.

## The REST API

Everything the admin dashboard does, except network access and the access code, can also be done over HTTP, for buttons on a Stream Deck or [Bitfocus Companion](https://bitfocus.io/companion), or scripts. The endpoints call the same services as the admin dashboard, so it and the overlays update together.

**[http://localhost:5062/api/docs](http://localhost:5062/api/docs)** lists every endpoint, with its fields and limits, and can send requests. It's built from the OpenAPI document at `/openapi/v1.json`, and loads nothing from the internet. Some examples:

| Button | Request |
| --- | --- |
| Start the timer | `POST /api/timer/start` |
| Take a minute off the timer | `POST /api/timer/adjust` with `{ "seconds": -60 }` |
| +1 point for player 1 | `POST /api/players/1/adjust` with `{ "points": 1 }` |
| +1 point for Team B (2v2) | `POST /api/teams/b/adjust` with `{ "points": 1 }` |
| Show a saved lower third | `POST /api/lower-third/messages/{id}/show` (ids are in `GET /api/lower-third`) |
| Hide the lower third | `POST /api/lower-third/hide` |
| Feature a card | `PUT /api/featured-card` with `{ "cardId": "..." }` (ids are in `GET /api/cards?search=jinx`) |

Bodies are JSON, sent with `Content-Type: application/json`. Seats are numbered 1 to 4 and teams are `a` and `b`, as in the admin dashboard. Changes answer with the new state, so a button can show it. Invalid requests get a 400 that names each problem field, and unknown seats and ids get a 404.

- **From the computer running the server,** no code is needed.
- **From another device,** network access must be on and an access code set, and every request sends the code: `Authorization: Bearer K7QM-4XPA`. Wrong codes are limited to a few a minute per device, like the sign-in page. A device signed in to the admin dashboard can also use the docs page.
- **Other websites can't use it.** A page open in a browser on this computer could otherwise send requests to `localhost`, so changes that come from another website's page are refused, as are requests addressed to names that aren't this computer's (see "OBS on another computer"). Stream Deck, Companion and scripts aren't affected, and nor are the server's own pages.

## Working on overlays

Each folder in `src/overlays/src/components/<name>/` is one overlay page:

- **`<name>.ts`** (required): the entry point. esbuild bundles it, along with everything it imports, into one `<name>.js`.
- **`<name>.css`**: the stylesheet, also bundled, including any `@import`s. Link it from the HTML; don't import it from the TypeScript.
- **`<name>.html`** and any other assets (images, fonts): copied into `dist/` as-is.

Code shared between overlays goes in `src/overlays/src/shared/`.

For fast iteration, run the watcher alongside the server:

```bash
cd src/overlays
npm run build:watch
```

It rebuilds TypeScript and CSS, and re-copies other assets, on every save. Refresh the page (or the OBS source) to see the change.

The watcher only bundles. To fix what the build's checks report, run these in `src/overlays`:

```bash
npm run lint:fix
```
```bash
npm run format
```

### Developing in Visual Studio

Choose the **Server + Overlay Watch** launch profile from the toolbar's startup dropdown, then press F5. It builds everything, starts the server under the debugger, and starts the overlay watcher in its own console window. Shift+F5 stops both.

The profile is defined in `Riftcaster.slnLaunch`. The watcher is started through `overlays.esproj` using `src/overlays/.vscode/launch.json`, which also works from VS Code's Run and Debug panel.

## Shared types (codegen)

Every public type in `Riftcaster.Contracts` becomes a TypeScript interface (or enum) in `src/overlays/src/generated/`, regenerated whenever Contracts changes. Static classes are skipped, since they can't be serialized (for example `ContractsAssembly`, the marker the generator uses to find the assembly). That folder is gitignored and must never be edited by hand. Import from it as types only:

```ts
import type { ServerIdentity } from '../../generated';
```

Two conventions to know:

- **Type names must be unique across Contracts.** TypeGen ignores C# namespaces and writes every type into one flat folder.
- **`DateTimeOffset` becomes `string`**, matching the ISO-8601 text the JSON actually contains. Parse it with `new Date(...)` where you display it.

## Windows development notes

Smart App Control blocks unsigned executables and DLLs, and a fresh build produces exactly those, so it can break development builds at random. Development here assumes it's turned off. Code signing for distributed builds is tracked in [#1](https://github.com/JasonDiBabbo/Riftcaster_VNext/issues/1).

## License

[MIT](LICENSE)

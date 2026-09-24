# Riftcaster

Live, data-driven stream overlays for Riftbound, controlled from a browser-based admin panel.

Riftcaster runs as a single server process on the same machine as OBS. It serves three things:

- **Overlay pages.** Each overlay element is its own page, added to OBS as a Browser Source, so OBS's own scene editor handles layout and positioning.
- **The admin panel.** A Blazor web app for controlling what the overlays show. It's meant to be opened from any device on the network (desktop, laptop, tablet); for now the server only listens on `localhost`.
- **An API** that the overlays use to get their data.

## Tech stack

| Layer | Technology |
| --- | --- |
| Server | ASP.NET Core (.NET 10), C# |
| Admin panel | Blazor, Interactive Server render mode (C#/Razor) |
| Overlays | Plain HTML, CSS, and TypeScript, bundled with esbuild |
| Shared types | C# records in `Riftcaster.Contracts`, turned into TypeScript interfaces by [TypeGen](https://github.com/jburzynski/TypeGen) on every build |

## Repository layout

```
src/
├── Riftcaster.Server/      ASP.NET Core host: API, admin, overlay files, and the overlays build
├── Riftcaster.Admin/       Blazor components for the admin panel (hosted by Server)
├── Riftcaster.Contracts/   Shared DTOs; the source of truth for server ↔ overlay data shapes
└── overlays/               npm project: one folder per overlay under src/components/
                            (overlays.esproj only lists it in Visual Studio; it builds nothing)
tests/
└── Riftcaster.Server.Tests/  xUnit tests against an in-memory server
tools/
└── Riftcaster.Codegen/     Build-time tool: Contracts → src/overlays/src/generated/*.ts
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
4. A typecheck and bundle of the overlays into `src/overlays/dist/`.

Each step is skipped when its inputs haven't changed. The steps are MSBuild targets in `Riftcaster.Server.csproj`, so building from Visual Studio, Rider, or VS Code (with the C# Dev Kit extension) does exactly the same thing.

Once it's running:

| URL | What |
| --- | --- |
| http://localhost:5062/ | Admin panel |
| http://localhost:5062/api/info | Server identity (JSON) |
| http://localhost:5062/overlays/serverIdentity/serverIdentity.html | Example overlay |

To build only the .NET side (without Node), pass `-p:SkipOverlays=true`.

## Tests

```bash
dotnet test
```

The tests start the real server in memory with `WebApplicationFactory` and exercise it over HTTP.

## Adding an overlay to OBS

1. Start the server.
2. In OBS, add a **Browser Source** and set its URL to the overlay's page, for example `http://localhost:5062/overlays/serverIdentity/serverIdentity.html`.
3. Set the source's width and height to the size of the overlay element. Overlay pages have transparent backgrounds.

After rebuilding an overlay, click **Refresh cache of current page** in the source's properties to load the new version.

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

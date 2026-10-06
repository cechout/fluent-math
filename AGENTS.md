# Fluent Math Development Guidelines

This project is a C#/.NET 8 WinUI 3 desktop app: a calculator for Windows that works the way a physical
pocket calculator does. The whole equation is typed first and evaluated on `=`, which is what guarantees
the correct order of operations, rather than being evaluated after every operator the way the built-in
Windows Calculator does. It also converts currencies from the European Central Bank daily reference
rates. It ships unpackaged and self-contained through an installer and as a portable zip, is packaged
as an MSIX for the Microsoft Store from the same project, and needs no elevation.

- Keep the work scoped to what was asked. Avoid opportunistic refactors, formatting churn, dependency
  bumps and drive-by renames.
- Read the surrounding code before adding an abstraction. Prefer the MVVM patterns that the file already
  uses: a view binds to a view model, and the view model holds no WinUI type it does not need.
- Preserve existing comments verbatim when you change the code around them.
- Everything is English: code, comments, commit messages, UI strings and release notes.
- Always follow `.editorconfig`. Text files are LF; `.gitattributes` pins the checkout, and the CI
  `format` job fails on CRLF.
- Build and publish `FluentMath/FluentMath.csproj`, never `FluentMath.slnx`. The solution
  also contains the frozen WPF version 1, which targets `net472` with a `packages.config` and does not
  build on a dotnet-only runner.
- `Calculator/` is that WPF version 1 and is kept for history only. It is not developed further and
  changes to it are not accepted.
- WinUI 3 has no `Any CPU` configuration, so every build and publish names a platform. `x64` is the one
  that ships.
- Never hardcode exchange rates or currency names. The rates come from the European Central Bank feed,
  and the display names come from `CultureInfo` with the raw ISO code as the fallback.
- Prefer targeted search over full file reads, and cap any command whose output could be large.

## Comment Style

A comment explains what a piece of code does and why it exists, not how the framework works underneath.
Weight scales with how non-obvious the reason is. A helper whose name already says everything needs no
comment at all.

The points below describe the style the code currently follows. They are recommendations, not entry
requirements: bring your own style if you prefer one, and expect these to shift over time.

- XML documentation comments are not used anywhere in this codebase. No `/// <summary>`, `/// <param>`,
  `/// <returns>` or `/// <inheritdoc/>`; plain `//` single-line comments throughout.
- Section headers inside a class are written as `// === section name ===`, lowercase, three equals signs,
  with a blank line above and below, rather than as multi-line banner dividers.
- Related fields are grouped under a `// topic name` or `// --- topic name ---` subheader, with short
  trailing comments on the individual lines.
- A comment that only restates the line below it, or that defends an option nobody took, is usually
  better left out.
- Comments are kept short: one or two lines for what the code does and the one thing that is not
  obvious, the unit once on a group line, a trailing comment in a few words, and no history of how the
  code got to where it is.

Four tags mark code that is not ordinary. Use each only for what it names:

- `// --- workaround: short name ---` for a bug in the platform or a third-party library, never for one
  of ours. State the problem, link the issue or the confirmed repro, then state the fix.
- `// --- memory leak: short name ---` for the WinUI 3 retained-instance pattern, where a window is
  hidden and reused because a real close never releases it.
- `// --- revisit: short name ---` for a deliberate temporary shape that has a named trigger for
  changing it.
- `// KNOWN UNRELIABLE:` for an external data source whose values cannot be trusted, paired with
  `(unreliable)` on the matching UI label.

XAML comments put two spaces inside the markers (`<!--  note  -->`). When the text does not fit on one
line it becomes a block with the markers on their own lines, rather than several stacked one-liners. A
double hyphen inside a comment is an XML parse error, so anomaly tags are written plainly there:
`<!--  workaround: short name  -->`.

## Project Structure

```text
FluentMath/
├── Assets/       the app icon, the package logos, and a banner per release under Releases/
├── Controls/     the formula display: CalculatorDisplay, MathPanel, XamlTextMeasurer;
│                 the converter layout every converter page shares: ConverterPanel;
│                 the release notes body: MarkdownRenderer
├── Distribution/ the channel the running build came from, installer, portable or store, and the
│                 update check for each: AppDistribution, UpdateService, UpdateInstaller,
│                 StoreUpdateSource; the release history: ReleaseCatalog
├── Engines/      the input model and the evaluator, both UI-free: MathInputManager, MathEvaluator
├── Models/       the token model, the exact values, the calculator setup and the currency logic:
│                 MathToken, NavigationMetadata, EvaluationMetadata, ExactValue, ResultFormatter,
│                 CalculatorSettings, GetCurrencyData, CurrencyHelper, RateTable
│   ├── Converters/ what each converter brings, UI-free: IUnitSource, UnitInfo, UnitFormat,
│   │             ConverterSettings, CurrencyUnitSource, LinearUnitSource, VolumeUnits, LengthUnits
│   └── Layout/   the formula layout, UI-free: MathBox, MathLayoutEngine, MathLayoutStyle,
│                 MathFit, MathHitTest, ITextMeasurer
├── Persistence/  what survives a restart, as json under %LocalAppData%\FluentMath, the package
│                 LocalState, or a Persistence folder next to the exe in the portable build:
│   ├── Models/   the file shapes, whose initial values are the defaults: AppSettingsData,
│   │             WindowState, PageStateData
│   └── Services/ PersistenceService (the disk), AppDataFolder, and the live stores
│                 SettingsService, WindowStateService, PageStateService
├── Properties/   PublishProfiles, launchSettings
├── ViewModels/   CalculatorViewModel, ConverterViewModel, RelayCommand
└── Views/        StandardPage, ScientificPage, CurrencyPage, VolumePage, LengthPage, SettingsPage,
                  UpdateDialog, ReleaseNotesDialog, ReleaseNotesPage, PadEntrance, ICompactPage,
                  SmallKeyLabels

FluentMath.Tests/   the engine tests, plain net8.0, no reference to the app
```

`MainWindow` holds the `NavigationView` and the `Frame` the pages are shown in, extends into the title
bar, and sizes the window through `WinUIEx.WindowManager`. The navigation groups the pages under two
headers, the calculators (Standard, Scientific) and the converters (Currency, Volume, Length). Both
calculators share `CalculatorViewModel` and `CalculatorDisplay`, every converter shares
`ConverterViewModel` and `ConverterPanel`, and every page but the settings is cached, so it keeps
its content across a navigation. "What's New" above the settings opens the release history in a dialog
and never selects. `MainWindow` also owns compact mode, a small window on top of every other one that
holds the page it was asked from; each page brings its own sizes through `ICompactPage`. The title bar
is a plain `Grid` rather than the WinUI `TitleBar`, which hides its title once it holds the update pill
in a narrow window.

`Setup/` holds the Inno Setup installer scripts and the `portable.txt` marker of the portable zip,
`Calculator/` the retired WPF version 1, and `.github/` the workflows, the issue and pull request
templates and the public README.

## Build

Build with the .NET CLI. This project has no COM references, so `dotnet build` resolves everything it
needs and none of the workflows carry an MSBuild setup step.

WinUI 3 has no `Any CPU` configuration, so always pass `-p:Platform=x64`.

```powershell
dotnet restore FluentMath/FluentMath.csproj -p:Platform=x64 -r win-x64 -p:SelfContained=true
dotnet build FluentMath/FluentMath.csproj --no-restore -c Release -p:Platform=x64 -r win-x64 -p:SelfContained=true
```

Publish only through a publish profile, and only after a full build pass. The XAML compiler resolves
`x:Bind` against project-local types in `MarkupCompilePass2`, which needs the `LocalAssembly` that a
build produces; publishing a fresh checkout without one fails with `WMC1509` or `WMC9999`.

```powershell
dotnet publish FluentMath/FluentMath.csproj --no-build -c Release -p:Platform=x64 -p:PublishProfile=win-x64
```

The `format` check runs `dotnet format whitespace --verify-no-changes` against the `.editorconfig`. It
only passes because `.gitattributes` pins the checkout to LF: Git for Windows sets `core.autocrlf=true`
in its system config and the GitHub runner has the same default, so without that pin the working tree is
CRLF and every line of every file is reported as a violation. Note that `dotnet format` takes no MSBuild
properties; passing `-p:Platform=x64` makes it print its usage help and exit non-zero, so the check would
silently never run.

The Microsoft Store package is the same project with `WindowsPackageType=MSIX` passed on the command line,
which overrides the `None` the `.csproj` sets, so the installer build needs no second project and no
edit. The release workflow runs it last, because it rebuilds into the same `bin` and `obj` folders the
installer is compiled from. The result is an unsigned `.msixupload`; the Store signs the package itself.

```powershell
dotnet publish FluentMath/FluentMath.csproj -c Release -p:Platform=x64 -p:PublishProfile=win-x64 -p:WindowsPackageType=MSIX -p:GenerateAppxPackageOnBuild=true -p:UapAppxPackageBuildMode=StoreUpload -p:AppxPackageSigningEnabled=false -p:AppxSymbolPackageEnabled=false
```

## Test

`FluentMath.Tests/` covers the input engine, the evaluator, the result formatter, the keypad
routing in `CalculatorViewModel`, the converters and the persistence layer. It targets plain `net8.0` and links the sources it tests rather than
referencing the app, which is a `WinExe` on a Windows target framework and cannot be referenced from a
plain library, so the suite runs on any dotnet runner.

```powershell
dotnet test FluentMath.Tests/FluentMath.Tests.csproj
```

The formula layout is covered too, under `Models/Layout/`: it is arithmetic over boxes and its text
metrics arrive through an interface, so a test hands it numbers it chose itself and asserts the result
rather than looking at a screenshot. Two files hold the layout and the input engine against each other,
which is where the bugs neither of them can see alone turn up: a click has to land the caret on the
point it was aimed at, and a press of an arrow key has to move the caret somewhere the eye can follow.

What is still not covered is the drawing: no test here opens a window, and nothing checks that a box
ends up on screen where the layout said it would. The bar for a change is that the tests stay green,
that the build stays green, and that the screen it touches was opened in a running app and looked at.
Report what you did not verify instead of implying it passed.

## Commit & Push

Run these before committing, and stage only what belongs to the change:

```powershell
git status --short
git diff --check
```

Branches are `feature/`, `fix/`, `chore/`, `refactor/` or `docs/` followed by a short name. Releases use
their own `update/` branch.

A one-line commit message is recommended. Pull requests are squash merged, so the individual commits are
collapsed into one anyway and only the pull request title survives on `main`. The issue reference is
appended in parentheses.

```text
feat: rework the formula display and the input model (#3)
fix: let every backspace delete something instead of stepping into the slot before (#3)
refactor: split the settings page into sections
chore: regenerate the package visual assets
build: v2.1.0
```

## Open a PR

Pull requests target `main` and are squash merged, so the pull request title becomes the commit message
on `main` and has to carry the same prefix a hand-written commit would.

Reference issues without closing them, as `Part of #N`. An issue is closed by hand once the change is
released, not by the merge.

Size the description by the diff, and check the length against this ladder before posting:

- A fix with one cause gets one or two bullets and nothing else. No paragraph.
- A small feature, or a fix whose behaviour change would surprise a reader, gets two sentences and up to
  three bullets.
- A branch that adds a subsystem or a distribution channel gets the full shape.

The opening paragraph carries context the diff cannot. When the title and two bullets already carry it,
the heading stands alone above the bullets. Do not argue the fix in the body: why a change is correct
belongs in the code comment, and if it seems worth saying in both places, the comment is what is missing.

**The prefix decides what reaches users.** Release notes are drafted from `feat:` and `fix:` pull
requests only. `chore:`, `build:`, `docs:` and `refactor:` are left out, because someone downloading the
app cannot notice them.

## Things That Must Not Change

These are load-bearing. Breaking one of them fails at runtime, on a user machine, or invisibly in a
repository setting, not in the build.

- **`PublishTrimmed=False` stays.** Trimming strips the WinRT and COM interop types the Windows App SDK
  resolves at runtime, and the app crashes on start. The `.csproj` defaults it to `False` as well, so a
  publish that bypasses a profile cannot hit the trap either.
- **Publish only through a profile in `FluentMath/Properties/PublishProfiles/`.** That is what
  makes a publish in CI apply the exact same settings as a local one, `PublishTrimmed` included.
- **Never bump `<Version>` in a feature branch.** The bump is a release activity and belongs on the same
  commit that carries the tag. The `.csproj` is the only place it is written: MSBuild derives
  `AssemblyVersion` and `FileVersion` from it, the release workflow reads it out and hands it to Inno
  Setup, and the `StampAppxManifestVersion` target writes it into the Store package, so neither the
  installer nor the package can drift out of sync. The version in `Package.appxmanifest` is a placeholder.
- **The package identity matches Partner Center.** `Name`, `Publisher` and `PublisherDisplayName` in
  `Package.appxmanifest` are dictated by the reserved app in Partner Center; an upload with any other
  value is rejected.
- **The release asset names are a contract.** `FluentMath_Installer.exe` is what the release workflow
  uploads, what the Inno Setup script produces, and what the release notes tell people to download.
  Renaming one of the three breaks the other two quietly. The same holds for
  `FluentMath_Portable_<version>.zip`, which keeps its top level folder (`Compress-Archive -Path $staging`,
  not `$staging/*`).
- **The portable marker stays out of the installer.** `portable.txt` next to the exe moves the state into a
  `Persistence` folder beside it; the `.iss` excludes both, so an installed build never writes into Program
  Files.
- **The release workflow creates a draft, and a human publishes it.** Publishing straight from CI would
  put every tag in front of users the moment the build finishes, and the update check would offer it to
  every running copy at once: `UpdateService` reads `releases/latest`, which only returns published releases.
- **The store build only names its update from GitHub.** Whether there is one is the Store's answer; the
  GitHub release only gives it a version. Publish the GitHub release once the Store version is live, or the
  store build shows an unnamed update until then.
- **Three `.gitignore` negations stay.** The stock template silently excludes files the build needs: the
  macOS `Icon` rule swallowed `FluentMath/Assets/Icon/`, so a fresh checkout failed with `CS7064`
  and the error named the icon rather than anything about git, `*.pubxml` swallowed all three publish
  profiles, and `[Rr]eleases/` would swallow the release banners in `FluentMath/Assets/Releases/`.
- **Every minor or major release ships a banner.** `FluentMath/Assets/Releases/v<x>-<y>-<z>.png`, the
  version label with dashes for dots, added before the tag. The release history shows it as the header
  of that release; without the file the release simply has none.
- **The CodeQL default setup stays switched off in the repository settings.** It collides with the
  advanced setup in `codeql.yml`, which builds manually because CodeQL autobuild cannot build a WinUI 3
  project.
- **Only `win-x64` ships.** The `win-x86` and `win-arm64` profiles are kept in the repository but are not
  released, and the installer is `ArchitecturesAllowed=x64compatible`.

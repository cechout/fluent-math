# Simple Calculator Development Guidelines

This project is a C#/.NET 8 WinUI 3 desktop app: a calculator for Windows that works the way a physical
pocket calculator does. The whole equation is typed first and evaluated on `=`, which is what guarantees
the correct order of operations, rather than being evaluated after every operator the way the built-in
Windows Calculator does. It also converts currencies from the European Central Bank daily reference
rates. It ships unpackaged and self-contained and needs no elevation.

- Keep the work scoped to what was asked. Avoid opportunistic refactors, formatting churn, dependency
  bumps and drive-by renames.
- Read the surrounding code before adding an abstraction. Prefer the MVVM patterns that the file already
  uses: a view binds to a view model, and the view model holds no WinUI type it does not need.
- Preserve existing comments verbatim when you change the code around them.
- Everything is English: code, comments, commit messages, UI strings and release notes.
- Always follow `.editorconfig`. Text files are LF; `.gitattributes` pins the checkout, and the CI
  `format` job fails on CRLF.
- Build and publish `Calculator_WinUI/Calculator_WinUI.csproj`, never `Calculator.slnx`. The solution
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
Calculator_WinUI/
├── Assets/       the app icon and the package logos
├── Engines/      the input model and the evaluator, both UI-free: MathInputManager, MathEvaluator
├── Models/       the token model and the currency logic: MathToken, NavigationMetadata,
│                 EvaluationMetadata, ResultFormatter, MathDisplayStyle, ConvertCurrency,
│                 GetCurrencyData, CurrencyHelper
├── Properties/   PublishProfiles, launchSettings
├── ViewModels/   StandardViewModel, CurrencyViewModel, RelayCommand
└── Views/        StandardPage, CurrencyPage, SettingsPage

Calculator_WinUI.Tests/   the engine tests, plain net8.0, no reference to the app
```

`MainWindow` holds the `NavigationView` and the `Frame` the pages are shown in, extends into the title
bar, and sizes the window through `WinUIEx.WindowManager`. `Setup/` holds the Inno Setup installer
scripts, `Calculator/` the retired WPF version 1, and `.github/` the workflows, the issue and pull
request templates and the public README.

## Build

Build with the .NET CLI. This project has no COM references, so `dotnet build` resolves everything it
needs and none of the workflows carry an MSBuild setup step.

WinUI 3 has no `Any CPU` configuration, so always pass `-p:Platform=x64`.

```powershell
dotnet restore Calculator_WinUI/Calculator_WinUI.csproj -p:Platform=x64 -r win-x64 -p:SelfContained=true
dotnet build Calculator_WinUI/Calculator_WinUI.csproj --no-restore -c Release -p:Platform=x64 -r win-x64 -p:SelfContained=true
```

Publish only through a publish profile, and only after a full build pass. The XAML compiler resolves
`x:Bind` against project-local types in `MarkupCompilePass2`, which needs the `LocalAssembly` that a
build produces; publishing a fresh checkout without one fails with `WMC1509` or `WMC9999`.

```powershell
dotnet publish Calculator_WinUI/Calculator_WinUI.csproj --no-build -c Release -p:Platform=x64 -p:PublishProfile=win-x64
```

The `format` check runs `dotnet format whitespace --verify-no-changes` against the `.editorconfig`. It
only passes because `.gitattributes` pins the checkout to LF: Git for Windows sets `core.autocrlf=true`
in its system config and the GitHub runner has the same default, so without that pin the working tree is
CRLF and every line of every file is reported as a violation. Note that `dotnet format` takes no MSBuild
properties; passing `-p:Platform=x64` makes it print its usage help and exit non-zero, so the check would
silently never run.

## Test

`Calculator_WinUI.Tests/` covers the input engine, the evaluator, the result formatter and the keypad
routing in `StandardViewModel`. It targets plain `net8.0` and links the sources it tests rather than
referencing the app, which is a `WinExe` on a Windows target framework and cannot be referenced from a
plain library, so the suite runs on any dotnet runner.

```powershell
dotnet test Calculator_WinUI.Tests/Calculator_WinUI.Tests.csproj
```

Nothing the user sees is covered by it: the formula is drawn by KaTeX inside a `WebView2`, and no test
here opens a window. The bar for a change is that the tests stay green, that the build stays green, and
that the screen it touches was opened in a running app and looked at. Report what you did not verify
instead of implying it passed.

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
- **Publish only through a profile in `Calculator_WinUI/Properties/PublishProfiles/`.** That is what
  makes a publish in CI apply the exact same settings as a local one, `PublishTrimmed` included.
- **Never bump `<Version>` in a feature branch.** The bump is a release activity and belongs on the same
  commit that carries the tag. The `.csproj` is the only place it is written: MSBuild derives
  `AssemblyVersion` and `FileVersion` from it, and the release workflow reads it out and hands it to Inno
  Setup, so the installer can never drift out of sync.
- **The release asset name is a contract.** `SimpleCalculator_Installer.exe` is what the release workflow
  uploads, what the Inno Setup script produces, and what the release notes tell people to download.
  Renaming one of the three breaks the other two quietly.
- **The release workflow creates a draft, and a human publishes it.** Publishing straight from CI would
  put every tag in front of users the moment the build finishes.
- **Two `.gitignore` negations stay.** The stock template silently excludes files the build needs: the
  macOS `Icon` rule swallowed `Calculator_WinUI/Assets/Icon/`, so a fresh checkout failed with `CS7064`
  and the error named the icon rather than anything about git, and `*.pubxml` swallowed all three publish
  profiles.
- **The CodeQL default setup stays switched off in the repository settings.** It collides with the
  advanced setup in `codeql.yml`, which builds manually because CodeQL autobuild cannot build a WinUI 3
  project.
- **Only `win-x64` ships.** The `win-x86` and `win-arm64` profiles are kept in the repository but are not
  released, and the installer is `ArchitecturesAllowed=x64compatible`.

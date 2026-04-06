# HoursBooking

HoursBooking is a cross-platform desktop app (Avalonia) for practical working-time tracking.
It combines daily stamping workflows with weekly progress visibility, editable segment history, and configurable rule-based calculations.

> [!NOTE]
> This project's source code was created with AI assistance.

## What It Does

- Clock in and clock out with live time updates.
- Show `Gross`, `Break deduction`, and `Net` worked time for today.
- Compute the expected target reach time for the current day.
- Track weekly totals and compare against a configurable weekly target (`Weekly desired hours`).
- Open a dedicated `Analysis` tab with visual weekly charts and trend insights.
- Add short comments to stamped segments (up to 280 characters).
- Edit or remove individual segments, or clear the entire list.
- Export all segments to CSV using a save dialog (choose destination and filename).
- Configure break deduction rules with multiple thresholds.
- Optionally count stamped-out gaps toward mandatory break deduction.
- Persist settings and segment history across restarts.
- Prevent accidental double starts (single-instance behavior with restore/activate).
- Support localized UI (`de`, `en`) and app themes (`System`, `Light`, `Dark`).

## Tech Stack

- .NET 10
- Avalonia 11
- CommunityToolkit.Mvvm
- NUnit

## Repository Layout

| Path | Purpose |
|---|---|
| `HoursBooking.App` | Avalonia desktop app, views, view models, app services |
| `HoursBooking.Core` | Core domain models and calculation logic |
| `HoursBooking.Tests` | Unit tests for core behavior |
| `HoursBooking.IntegrationTests` | Headless integration tests |

## Quick Start

### Prerequisites

- .NET 10 SDK

Check your SDK:

```bash
dotnet --version
```

### Run the App

```bash
dotnet run --project HoursBooking.App/HoursBooking.App.csproj
```

### Run Tests

```bash
dotnet test HoursBooking.slnx
```

## Daily Workflow

1. Click `Clock In` to start a segment.
2. Optionally enter a short comment for the next stamp action.
3. Click `Clock Out` to complete the active segment.
4. Repeat as needed throughout the day.
5. Use the segment list to edit, remove, export, or clear entries.

The app validates segment edits and prevents invalid overlaps.

## Configuration

### Working-Time Settings

- `Maximum net working time (hours)`
- `Desired net working time (hours)`
- `Desired weekly net working time (hours)`
- Alert thresholds for `Info`, `Warning`, and `Error`

### Break Model

- Custom break rules (`if worked >= X hours, deduct Y hours`)
- Optional setting to count stamped-out gaps toward mandatory breaks

### UI Settings

- Theme: `System`, `Light`, `Dark`
- Language: system/de/en
- Tray behavior options

### Break Rule Example

- Rule 1: from `0.0h` deduct `0.25h`
- Rule 2: from `6.0h` deduct `0.75h`

If stamped-out gaps are counted and there is already `0.50h` gap time,
only the remaining required break is deducted from net time.

## Calculation Model

### Daily Net Time

```text
Net = Gross - EffectiveBreakDeduction
```

`EffectiveBreakDeduction` is either:

- the required break from configured rules, or
- required break minus stamped-out gaps (if enabled)

### Weekly View

- Weekly totals are derived from segments in the current week.
- Weekly difference is calculated against `Desired weekly net working time (hours)`.
- Positive difference means overtime relative to target; negative means remaining deficit.

## Persistence

Settings and segment history are stored as JSON in the user profile.

Typical Linux location:

```text
~/.config/HoursBooking/settings.json
```

Exact path depends on `Environment.SpecialFolder.ApplicationData`.

## Flatpak (Linux)

You can build a Flatpak bundle locally without automatic installation.

### Prerequisites

- `flatpak`
- `flatpak-builder`
- `dotnet` SDK 10
- `magick` (ImageMagick)

Install runtime and SDK:

```bash
flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
flatpak install -y flathub org.freedesktop.Platform//24.08 org.freedesktop.Sdk//24.08
```

### Build Bundle

```bash
./scripts/build-flatpak.sh
```

Generated bundle:

```text
io.github.hoursbooking.HoursBooking.flatpak
```

Optional local install/reinstall:

```bash
flatpak install --user --reinstall --bundle ./io.github.hoursbooking.HoursBooking.flatpak
```

Run:

```bash
flatpak run io.github.hoursbooking.HoursBooking
```

Tray note:
Flatpak tray behavior relies on StatusNotifier over DBus. Depending on your desktop environment, a compatible tray or indicator host must be active.

Generated local packaging artifacts (for example `flatpak/publish/`, `flatpak/*.png`, and `flatpak/io.github.hoursbooking.HoursBooking.svg`) are intentionally ignored by `.gitignore` and should not be committed.

## Architecture

- MVVM pattern with CommunityToolkit.Mvvm
- Business logic isolated in `HoursBooking.Core`
- UI structured as modular tabs with dedicated views (`Booking`, `Analysis`, `Settings`)
- View-model composition in progress to avoid an oversized main view model
- Unit and integration test coverage with NUnit

## Test Coverage Status

Current automated tests include:

- Core calculation tests (`HoursBooking.Tests/UnitTest1.cs`)
- UI integration smoke tests (`HoursBooking.IntegrationTests/UnitTest1.cs`)
- Weekly analysis view model tests (`HoursBooking.IntegrationTests/WeeklyAnalysisViewModelIntegrationTests.cs`)
- Single-instance manager tests (`HoursBooking.Tests/AppSingleInstanceManagerTests.cs`)

Current known gap:

- Not every command and every branch in `MainWindowViewModel` has dedicated automated tests yet.
- The current suite focuses on high-risk behavior (core calculations, UI boot path, analysis calculations, and single-instance activation).

## Next Ideas

- Graphical analytics for hours and segment patterns
- Date-range filtering for exports
- Monthly target comparison
- Holiday and time-account integrations

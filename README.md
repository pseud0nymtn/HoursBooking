# HoursBooking

HoursBooking is a cross-platform desktop app (Avalonia) for tracking daily working time.
It supports clock in/out workflows, configurable break rules, net-time alerts, and inline correction of work segments.

> [!NOTE]
> This project's source code was created with AI assistance.

## Highlights

- Live clock in/out tracking with continuously updated totals.
- Gross, deducted break, and net worked time overview.
- Configurable alert thresholds (`Info`, `Warning`, `Error`) near maximum net work time.
- Desired work time target with estimated completion time.
- Flexible break rules with multiple tiers.
- Optional deduction of stamped-out gaps from required break time.
- Inline editing for existing work segments.
- Theme support: `System`, `Light`, `Dark`.
- Localization-ready UI.
- JSON settings persistence.

## Tech Stack

- .NET 10
- Avalonia 11
- CommunityToolkit.Mvvm
- NUnit

## Repository Layout

| Path | Purpose |
|---|---|
| `HoursBooking.App` | Avalonia desktop app, UI, view models, app services |
| `HoursBooking.Core` | Core business logic (calculation, rules, alerts) |
| `HoursBooking.Tests` | Unit tests for core logic |
| `HoursBooking.IntegrationTests` | Avalonia headless integration tests |

## Quick Start

### Prerequisites

- .NET 10 SDK

Check your SDK version:

```bash
dotnet --version
```

### Run the Application

From repository root:

```bash
dotnet run --project HoursBooking.App/HoursBooking.App.csproj
```

### Run Tests

```bash
dotnet test HoursBooking.slnx
```

## Usage

### Track Working Time

1. Click `Clock In` to start a segment.
2. Click `Clock Out` to finish the active segment.
3. Repeat as needed for multiple segments during the day.
4. Use the `Work Segments` list to review and update entries.

### Edit Existing Segments

1. Click `Edit` on a segment.
2. Adjust start/end time.
3. Click `Apply` to save.

The app prevents invalid and overlapping segment ranges.

## Configuration

Configurable settings include:

- Maximum net working time (hours)
- Desired net working time (hours)
- Alert thresholds for `Info`, `Warning`, and `Error`
- Custom break rules (`if worked >= X hours, deduct Y hours`)
- Option to count stamped-out time toward mandatory breaks
- Theme mode (`System`, `Light`, `Dark`)

### Break Rule Example

- Rule 1: from `0.0h` deduct `0.25h`
- Rule 2: from `6.0h` deduct `0.75h`

If stamped-out gaps are counted and there is already `0.50h` gap time,
only the remaining required break is deducted from net time.

## Persistence

Settings are stored as JSON in the user profile.

Typical Linux location:

```text
~/.config/HoursBooking/settings.json
```

Exact path depends on `Environment.SpecialFolder.ApplicationData`.

## Calculation Model

Net worked time:

```text
Net = Gross - EffectiveBreakDeduction
```

Effective break deduction is:

- Required break from configured rules, or
- Required break minus stamped-out gaps (if enabled)

Desired work time completion is computed dynamically and accounts for break-rule threshold jumps.

## Flatpak (Linux)

You can build a Flatpak bundle locally without auto-installation.

### Prerequisites

- `flatpak`
- `flatpak-builder`
- `dotnet` SDK 10
- `magick` (ImageMagick)

Install runtime/sdk:

```bash
flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
flatpak install -y flathub org.freedesktop.Platform//24.08 org.freedesktop.Sdk//24.08
```

### Build Bundle

```bash
./scripts/build-flatpak.sh
```

Output bundle:

```text
io.github.hoursbooking.HoursBooking.flatpak
```

Optional install:

```bash
flatpak install --user --bundle ./io.github.hoursbooking.HoursBooking.flatpak
```

Run:

```bash
flatpak run io.github.hoursbooking.HoursBooking
```

Tray note:
Tray behavior in Flatpak relies on StatusNotifier over DBus. Depending on your desktop environment, a compatible tray/indicator host must be active.

Generated local packaging artifacts (for example `flatpak/publish/`, `flatpak/*.png`, and `flatpak/io.github.hoursbooking.HoursBooking.svg`) are intentionally ignored by `.gitignore`.

## Architecture

- MVVM pattern with CommunityToolkit.Mvvm.
- Clear separation between UI layer and business logic.
- Testable core logic in `HoursBooking.Core`.

## Current Limitations

- Settings persist across restarts; time segments currently do not.
- Focus is currently day-based tracking (no built-in historical reporting yet).

## Roadmap Ideas

- Persistent daily booking history
- CSV/PDF export
- Weekly and monthly summaries
- Holiday and target-time models

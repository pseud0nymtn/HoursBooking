# HoursBooking

HoursBooking ist eine Desktop-Anwendung auf Basis von Avalonia zur Erfassung von Arbeitszeiten. Die App unterstuetzt Ein- und Ausstempeln, konfigurierbare Warnstufen fuer maximale Netto-Arbeitszeit, flexible Pausenregeln sowie die nachtraegliche Korrektur einzelner Arbeitsabschnitte.

## Funktionsumfang

- Ein- und Ausstempeln mit Live-Aktualisierung der Tageswerte
- Anzeige von Bruttozeit, Pausenabzug und Netto-Arbeitszeit
- Warnstufen `Info`, `Warning` und `Error` bei Annaeherung an die maximale Netto-Arbeitszeit
- Zusaetzliche Meldung bei Erreichen oder Ueberschreiten der maximalen Netto-Arbeitszeit
- Konfigurierbare Wunscharbeitszeit mit Anzeige, bis zu welcher Uhrzeit gearbeitet werden muss
- Frei definierbare Pausenregeln mit beliebig vielen Stufen
- Optionale Anrechnung ausgestempelter Zeitluecken auf die erforderliche Pause
- Nachtraegliche Bearbeitung von Ein- und Ausstempelzeiten einzelner Arbeitsabschnitte
- Theme-Umschaltung zwischen `System`, `Light` und `Dark`
- Persistente Speicherung der Einstellungen als JSON
- Unit-Tests und Avalonia-Headless-Integrationstests mit NUnit

## Technologie-Stack

- .NET 10
- Avalonia 11
- CommunityToolkit.Mvvm
- NUnit

## Projektstruktur

- [HoursBooking.App](HoursBooking.App): Avalonia-Desktop-Anwendung, UI, ViewModels und Infrastruktur
- [HoursBooking.Core](HoursBooking.Core): Fachlogik fuer Zeitberechnung, Warnungen und Pausenregeln
- [HoursBooking.Tests](HoursBooking.Tests): Unit-Tests fuer die Kernlogik
- [HoursBooking.IntegrationTests](HoursBooking.IntegrationTests): Headless-Integrationstests fuer UI und Interaktion

## Voraussetzungen

- Installiertes .NET 10 SDK

SDK-Version pruefen:

```bash
dotnet --version
```

## Anwendung starten

Aus dem Repository-Root:

```bash
dotnet run --project HoursBooking.App/HoursBooking.App.csproj
```

## Tests ausfuehren

Alle Tests starten:

```bash
dotnet test HoursBooking.slnx
```

## Bedienung

### Arbeitszeit buchen

1. Mit `Einstempeln` beginnt ein Arbeitsabschnitt.
2. Mit `Ausstempeln` wird der aktuelle Abschnitt beendet.
3. Mehrere Arbeitsabschnitte pro Tag sind moeglich.
4. Im Bereich `Arbeitsabschnitte` koennen vorhandene Eintraege nachtraeglich bearbeitet werden.

### Zeiten korrigieren

- Einen Abschnitt ueber `Bearbeiten` auswaehlen
- Start- und Endzeit im Format `HH:mm` anpassen
- Mit `Uebernehmen` speichern

Die Anwendung verhindert dabei ungueltige oder ueberlappende Arbeitsabschnitte.

## Konfiguration

Die Anwendung bietet folgende Einstellungen in der UI:

- Maximale Arbeitszeit Netto in Stunden
- Wunscharbeitszeit Netto in Stunden
- Restzeit-Schwellwerte fuer `Info`, `Warning` und `Error`
- Beliebig viele Pausenregeln der Form:
  `Wenn Arbeitszeit >= X Stunden, dann Y Stunden Pause abziehen`
- Option `Ausgestempelte Zeiten auf Pausenpflicht anrechnen`
- Theme-Auswahl `System`, `Light`, `Dark`

### Beispiel fuer Pausenregeln

- Ab `0` Stunden: `0.25` Stunden Pause
- Ab `6` Stunden: `0.75` Stunden Pause

Bei aktivierter Anrechnung ausgestempelter Zeit gilt:

- Wenn zwischen zwei Arbeitsabschnitten bereits `0.5` Stunden ausgestempelt wurde
- und laut Regel `0.75` Stunden Pause erforderlich sind
- werden nur noch `0.25` Stunden zusaetzlich von der Nettozeit abgezogen

## Persistenz

Die Einstellungen werden als JSON im Benutzerprofil gespeichert.

Typischer Speicherort unter Linux:

```text
~/.config/HoursBooking/settings.json
```

Hinweis: Der konkrete Pfad wird ueber `Environment.SpecialFolder.ApplicationData` bestimmt und kann je nach Umgebung leicht abweichen.

## Berechnungslogik

### Netto-Arbeitszeit

Die Netto-Arbeitszeit ergibt sich aus:

```text
Brutto-Arbeitszeit - effektiver Pausenabzug
```

Der effektive Pausenabzug ist entweder:

- die laut Pausenregel erforderliche Pause

oder, wenn die entsprechende Option aktiv ist:

- die erforderliche Pause minus bereits ausgestempelte Zeitluecken zwischen Arbeitsabschnitten

### Zielzeit fuer Wunscharbeitszeit

Die Anzeige `Wunscharbeitszeit erreicht um ca. HH:mm` wird auf Basis der aktuellen Nettozeit und der konfigurierten Pausenregeln berechnet. Dabei werden Spruenge in der Pausenlogik beruecksichtigt, zum Beispiel wenn ab einer bestimmten Arbeitszeit mehr Pause erforderlich wird.

## Design und Architektur

- MVVM mit CommunityToolkit.Mvvm
- Trennung von UI und Fachlogik
- Theme-faehige Oberflaeche mit Light/Dark/System
- Kernberechnungen in der Core-Bibliothek testbar isoliert gehalten

## Bekannte Grenzen

- Aktuell werden Einstellungen persistent gespeichert, Arbeitsabschnitte selbst jedoch noch nicht dauerhaft ueber App-Neustarts hinweg archiviert
- Die Anwendung arbeitet derzeit auf Tagesbasis ohne Historien- oder Reporting-Funktion

## Weiterfuehrende Ideen

- Persistente Speicherung taeglicher Buchungen
- Export nach CSV oder PDF
- Wochen- und Monatsauswertung
- Feiertags- und Sollzeitmodelle

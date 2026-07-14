# PAS (Portable App Sync)

**Ein tragbares Dienstprogramm zur Automatisierung der Sicherung und Wiederherstellung installierter Windows-Anwendungen**

- **Automatische Protokollierung** aller Vorgänge
- **Portabel**, keine Installation erforderlich, einzelne „.exe“-Datei
- **Moderne Fluent-Benutzeroberfläche** im Stil von WPF-UI 4.3.0, unterstützt Hell/Dunkel-Themen und System-Theme-Abgleich
- **Intelligente Filterung** indiziert standardmäßig nur Benutzeranwendungen (mit der Option, versteckte Systemkomponenten anzuzeigen)
- **Detaillierte Informationen** Laden offizieller Beschreibungen und für Menschen lesbarer Namen im Hintergrund. Wenn der App-Name noch nicht geladen ist, wird stattdessen die Paket-ID angezeigt (diese sind oft identisch).

## Dokumentation

Detaillierte Handbücher zur Verwendung der Anwendung:
- **[Schnellstartanleitung](QUICKSTART.md)**: Eine kurze Anleitung zum Starten und Verwenden der Anwendung.
- **[Benutzerhandbuch](GUIDE_DE.md)**: Ein umfassendes Referenzhandbuch, das die Filterung, die Exportmodi, den Hybrid-Fallback, die Wiederherstellungsschritte und die Fehlerbehebung erklärt.



## Technologien

- **.NET 10.0** moderne Entwicklungsplattform
- **WPF & WPF-UI (v4.3.0)** Fließendes Designsystem und moderne Steuerung
- **Winget** Windows Package Manager für die Paketverwaltung
- **MVVM**-Architekturmuster zur Trennung von Logik und Präsentation

## Systemanforderungen

- **Betriebssystem**: Windows 10 (Version 1809+) oder Windows 11
- **Winget**: Muss installiert sein (normalerweise vorinstalliert in Windows 11)
- **.NET Runtime**: Nicht erforderlich (eigenständiger Build)

## Verwendung

### Erstellen eines Backups

1. Führen Sie „PAS.exe“ aus
2. Warten Sie, bis der Systemscan abgeschlossen ist (nur Benutzer-Desktopanwendungen werden indiziert)
3. Wählen Sie die Anwendungen aus, die Sie speichern möchten
   - Der Filter **Alle sichtbar / Offline verfügbar / Online-Fallback / Standardmäßig ausgeschlossen** hilft dabei, schnell zu überprüfen, was in das Offline-Paket oder Fallback-Skript aufgenommen oder ausgeschlossen wird
4. Exportmodus auswählen:
   - **Online Script** erstellt eine „.bat“- oder „.ps1“-Datei für die automatische Installation über Winget (empfohlen)
   - **Offline Package** erstellt einen Hybridsatz: lädt verfügbare Distributionen herunter und fügt bei Bedarf ein Online-Fallback-Skript für nicht unterstützte Anwendungen hinzu
5. Klicken Sie auf die Schaltfläche „Exportieren“ und geben Sie den Speicherort an
6. Verwenden Sie nach Abschluss die Zusammenfassung des letzten Exports und die Schaltfläche **Exportordner öffnen**

### Anwendungen wiederherstellen

#### Modus „Online-Skript“
1. Kopieren Sie die erstellte Datei „RestoreApps.bat“ oder „RestoreApps.ps1“ auf das neue System
2. Führen Sie die Datei als Administrator aus
3. Warten Sie, bis alle Anwendungen automatisch installiert werden

#### Modus „Offline-Paket“
1. Kopieren Sie den Ordner mit den heruntergeladenen Distributionen auf das neue System
2. Führen Sie „install_all.bat“ als Administrator aus
3. Wenn „RestoreOnlineFallback.bat“ erstellt wurde, führen Sie es nach der Offline-Installation auf einem Computer mit Internetzugang aus
4. Offline verfügbare Anwendungen werden aus lokalen Dateien installiert, während nicht unterstützte Anwendungen über „winget install“ bereitgestellt werden

## Aufbau aus der Quelle

### Entwicklungsanforderungen

- .NET 10 SDK
- Visual Studio 2022 oder JetBrains Rider (optional)

### Build-Befehle

```powershell
# Clone the repository
git clone https://github.com/Almanex/Portable-App-Sync.git
cd Portable-App-Sync

# Build the project
dotnet build

# Run the application
dotnet run

# Create a portable executable (single file)
dotnet publish -c Release
```

Die endgültige „.exe“-Datei befindet sich unter „dist\PAS.exe“.

## Projektstruktur

```
PAS/
├── Models/              # Data models
│   ├── InstalledApp.cs
│   ├── ExportMode.cs
│   ├── ExportResult.cs
│   └── WingetExportModels.cs
├── Services/            # Business logic
│   ├── WingetService.cs
│   ├── SystemScanService.cs
│   ├── ScriptGeneratorService.cs
│   ├── DownloadService.cs
│   ├── LoggingService.cs
│   ├── CacheService.cs
│   └── OfflineCompatibilityService.cs
├── ViewModels/          # ViewModels MVVM
│   ├── MainViewModel.cs
│   └── RelayCommand.cs
├── Views/               # UI-Ansichten (WPF)
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── HelpWindow.xaml
│   └── HelpWindow.xaml.cs
├── Converters/          # XAML-Konverter
│   └── ValueConverters.cs
├── App.xaml             # Einstiegspunkt
├── App.xaml.cs
├── AssemblyInfo.cs
└── icon.ico             # Anwendungs-Icon
```

## Wichtige Hinweise

### Anwendungsfilterung

Standardmäßig verbirgt die Anwendung Folgendes:
- **Microsoft Store Apps** (MSIX-Pakete: Rechner, Fotos, Xbox usw.)
- **Systemkomponenten** (Visual C++ Redistributable, .NET Runtime, Treiber)
- **Technische Abhängigkeiten** (WindowsAppRuntime, VCLibs usw.)

In der Liste werden nur **Benutzer-Desktopanwendungen** angezeigt, die über „.exe“- oder „.msi“-Installationsprogramme installiert wurden.
**Wenn Sie versteckte Pakete benötigen:** Sie können diesen Filter in der Benutzeroberfläche sofort deaktivieren, indem Sie *"System und versteckte Anwendungen anzeigen"* aktivieren.

Dienstkomponenten und Updater-Pakete wie „Microsoft Edge Update“ sind standardmäßig vom Export ausgeschlossen. Sie verschwinden nicht vollständig: Aktivieren Sie System-/versteckte Apps und wählen Sie den Filter **Standardmäßig ausgeschlossen**, um zu sehen, was genau ausgeschlossen wurde.

### Winget-Check

In älteren Windows 10-Builds fehlt Winget möglicherweise. Skripte prüfen automatisch, ob es vorhanden ist, und geben Anweisungen für die Installation des App Installers aus dem Microsoft Store.

### Einschränkungen im Offline-Modus

**Nicht alle Anwendungen unterstützen das Herunterladen von Distributionen** über „winget download“. Beispiele:
- **Visual Studio Code** (`Microsoft.VisualStudioCode`)
- **Git** (`Git.Git`)
- **Android Studio** (`Google.AndroidStudio`)
- Und andere Anwendungen, bei denen die Hersteller das direkte Herunterladen eingeschränkt haben

Für solche Anwendungen verwendet PAS jetzt einen **hybriden Offline-Modus**:
- Unterstützte Pakete werden lokal in den ausgewählten Ordner heruntergeladen
- Für nicht unterstützte Pakete wird automatisch eine „RestoreOnlineFallback.bat“ erstellt
- Dadurch schlägt der Export aufgrund dieser Apps nicht fehl: Sie werden als „Übersprungen“ markiert und in das Fallback-Skript verschoben

Empfohlene Wiederherstellungsreihenfolge:
1. Führen Sie „install_all.bat“ aus
2. Wenn es dann erstellt wurde, führen Sie „RestoreOnlineFallback.bat“ auf einem Computer mit Internet aus

**Empfehlung**: Verwenden Sie den Modus „Online-Skript“, wenn Sie ein einziges universelles Szenario ohne Trennung in Offline und Fallback wünschen.

### Sicherheit und Zuverlässigkeit

Das Projekt ist mit dem Schwerpunkt auf Stabilität und Sicherheit auf Industrieniveau konzipiert:

- **[Sicherheit] Injektionsschutz**: Alle Interaktionen mit der Winget-CLI durchlaufen eine strenge Paket-ID-Validierung („SafeIdPattern“). Dadurch wird die Möglichkeit der Ausführung beliebiger Befehle durch Manipulation des App-Namens ausgeschlossen.
- **[Stabilität] Prozessstabilität**: Für alle externen Aufrufe sind harte Timeouts (120s) implemetiert. Wenn ein Winget-Prozess hängt, beendet die Anwendung korrekt ihren Prozessbaum und hinterlässt keine „Zombie“-Prozesse.
- **[Zuverlässigkeit] Datenintegrität**: Das Caching-System ist vor beschädigten Daten und Speicherüberlauf geschützt (50 MB-Limit pro Datei). Die JSON-Struktur wird vor dem Lesen validiert.
- **[Einschränkung] Protokollrotation**: Die automatische Verwaltung der Protokolldateigröße (5 MB-Grenze) verhindert ein unkontrolliertes Datenwachstum auf der Festplatte.
- **[Architektur] DI-Architektur (Abhängigkeitsinjektion)**: Die Verwendung eines Abhängigkeitscontainers gewährleistet die Komponentenisolation, vereinfacht das Testen und eliminiert Fehler bei der Dienstinitialisierung.
- **[Threads] Thread-Sicherheit**: Alle Schnittstellenaktualisierungen und Hintergrundaufgaben werden synchronisiert, wodurch Abstürze beim parallelen Laden von Beschreibungen oder bei der App-Installation vermieden werden.

Alle Vorgänge sind transparent und werden in Echtzeit in „PAS.log“ protokolliert.

## Protokollierung

Alle Vorgänge werden in der Datei „PAS.log“ im Ordner „%LocalAppData%\PAS\“ aufgezeichnet. Das Protokoll enthält:
- Informationsmeldungen zum Fortschritt
- Warnungen zu Problemen
- Fehler mit Full-Stack-Traces
- Fatale Ausnahmen (wenn die Anwendung abgestürzt ist)

## Mitwirken

Alle Verbesserungen sind willkommen! Bitte:
1. Forken Sie das Projekt
2. Erstellen Sie einen Zweig für Ihre Änderungen
3. Senden Sie eine Pull-Anfrage

## Lizenz

Dieses Projekt wird unter der MIT-Lizenz vertrieben. Einzelheiten finden Sie in der Datei „LICENSE“.

## Danksagungen

- Microsoft für [Winget](https://github.com/microsoft/winget-cli)
- .NET-Community für hervorragende Entwicklungstools

---

**Hinweis**: Diese Anwendung verwendet den offiziellen Windows-Paketmanager (Winget) und enthält keinen Schadcode. Alle Vorgänge sind transparent und können im Quellcode überprüft werden.

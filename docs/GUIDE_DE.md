# PAS (Portable App Sync) — Umfassendes Benutzerhandbuch

Willkommen zum umfassenden Benutzerhandbuch für **PAS (Portable App Sync)**! Diese Anleitung führt Sie durch die Funktionsweise der Anwendung, hilft Ihnen, die Sicherungs- und Wiederherstellungsprozesse zu verstehen, erklärt die Filtersysteme und zeigt Ihnen, wie Sie häufig auftretende Probleme lösen können.

---

## Anwendungs-Workflow

Das folgende Diagramm zeigt den allgemeinen Ablauf beim Sichern und Wiederherstellen Ihrer Anwendungen mit PAS.

```mermaid
flowchart TD
    Start([PAS.exe starten]) --> Scan[System über Winget scannen]
    Scan --> Populate[Interaktive UI-Liste füllen]
    
    subgraph Filterung & Auswahl
        Populate --> Filter{Filter anwenden}
        Filter -- "System-/Versteckte anzeigen" --> ShowAll[Alle Pakete anzeigen]
        Filter -- "Nur Benutzer-Apps (Standard)" --> ShowUser[Desktop-Apps anzeigen]
        ShowAll & ShowUser --> Select[Apps über Kontrollkästchen auswählen]
    end
    
    Select --> Mode{Exportmodus wählen}
    
    subgraph Exportieren
        Mode -- "Online-Skript" --> GenOnline[RestoreApps.bat / .ps1 generieren]
        Mode -- "Offline-Paket" --> DownLocal[Installationsprogramme in lokalen Ordner herunterladen]
        DownLocal --> CheckComp{Von Winget-Download unterstützt?}
        CheckComp -- Ja --> SaveInst[Installationsdatei speichern]
        CheckComp -- Nein --> MarkSkip[Als übersprungen markieren & zum Fallback hinzufügen]
        SaveInst & MarkSkip --> GenOffline[install_all.bat & RestoreOnlineFallback.bat generieren]
    end

    GenOnline & GenOffline --> End([Bereit zur Wiederherstellung auf neuem System])
```

---

## Anwendungsfilterung verstehen

Standardmäßig scannt PAS alle installierten Anwendungen auf Ihrem Computer mithilfe des Windows-Paketmanagers (Winget). Um Ihre Sicherungsliste nicht mit Systempaketen oder Laufzeitbibliotheken zu überfrachten, wendet die Anwendung intelligente Filter an:

- **Benutzer-Desktopanwendungen (Standard)**: Zeigt Standardanwendungen an, die vom Benutzer installiert wurden (z. B. Google Chrome, VS Code, Discord, WinRAR).
- **Microsoft Store Apps**: Standardmäßig ausgeblendet. Umfasst vorinstallierte Windows-Pakete (UWP/MSIX) wie Rechner, Xbox, Fotos usw.
- **Systemkomponenten**: Standardmäßig ausgeblendet. Umfasst Compiler, Treiber, Konfigurationspakete und gemeinsam genutzte Laufzeiten (z. B. Visual C++ Redistributables, .NET Runtimes).
- **Technische Abhängigkeiten**: Standardmäßig ausgeblendet. Framework-Laufzeiten und -Bibliotheken wie `VCLibs` oder `WindowsAppRuntime`.

### So schalten Sie Filter um
Wenn Sie eine ausgeblendete Systemkomponente (z. B. eine bestimmte Laufzeit- oder Store-App) sichern möchten:
1. Aktivieren Sie das Kontrollkästchen **"System und versteckte Anwendungen anzeigen"** oben auf der Benutzeroberfläche.
2. Die Liste wird sofort aktualisiert, um alle gescannten Pakete anzuzeigen.
3. Verwenden Sie den Ansichtsfilter:
   - **Alle sichtbar**: Zeigt alle derzeit ungefilterten Programme an.
   - **Offline verfügbar**: Zeigt nur Anwendungen an, die das Herunterladen ihrer Installationsprogramme über Winget unterstützen.
   - **Online-Fallback**: Zeigt Anwendungen an, die während der Wiederherstellung aus dem Internet heruntergeladen werden müssen, da sie keinen direkten Download unterstützen.
   - **Standardmäßig ausgeschlossen**: Zeigt Updater-Komponenten oder System-Dienstprogramme an, die standardmäßig von Backups ausgeschlossen sind (z. B. `Microsoft Edge Update`), um Konflikte zu vermeiden.

---

## Exportmodi erklärt

PAS bietet zwei verschiedene Methoden zur Sicherung Ihrer Anwendungskonfiguration. Die Wahl des richtigen Modus hängt von der Umgebung Ihres Zielgeräts ab.

### 1. Online-Skript (Empfohlen)
Dieser Modus generiert ein leichtgewichtiges Befehlsskript, das die Installationsprogramme während des Wiederherstellungsprozesses direkt aus den offiziellen Winget-Repositorys von Microsoft herunterlädt.

- **Generierte Dateien**: `RestoreApps.bat` (und/oder `RestoreApps.ps1`).
- **Ideal für**: Wiederherstellung von Anwendungen auf einem Gerät mit einer stabilen Internetverbindung.
- **Vorteile**:
  - Extrem kleine Sicherungsgröße (nur wenige Kilobyte).
  - Installiert zum Zeitpunkt der Wiederherstellung immer die **neuesten Versionen** der Apps.
  - Sehr zuverlässig.
- **Nachteile**: Erfordert eine aktive Internetverbindung während der Wiederherstellung.

### 2. Offline-Paket
Dieser Modus versucht, alle Installationsdateien (`.exe`, `.msi`, `.msix`) in einen lokalen Ordner herunterzuladen, damit sie ohne Internetzugang installiert werden können.

- **Generierte Dateien**: Ein Ordner mit Installationsdateien, `install_all.bat` (Skript zur Offline-Installation) und möglicherweise `RestoreOnlineFallback.bat` (Skript für Online-Fallback).
- **Ideal für**: Offline-Systeme, Unternehmensnetzwerke mit eingeschränktem Internet oder zur Bandbreitenschonung.
- **Vorteile**: Installiert Anwendungen schnell und ohne Download während des Setups.
- **Nachteile**: Große Sicherungsgröße (mehrere Gigabyte, je nach ausgewählten Apps).

> [!IMPORTANT]
> **Der Hybrid-Fallback-Mechanismus**: Viele Herausgeber (z. B. Microsoft für VS Code, Git, Android Studio) beschränken das direkte Herunterladen von Installationsprogrammen über Winget.
> 
> Um Sicherungsfehler zu vermeiden, erkennt PAS diese nicht unterstützten Pakete automatisch, überspringt das Herunterladen im Offline-Modus und fügt sie der Datei `RestoreOnlineFallback.bat` hinzu.
> 
> Führen Sie bei der Wiederherstellung zuerst `install_all.bat` aus, um die Offline-Pakete zu installieren, und führen Sie dann `RestoreOnlineFallback.bat` aus, sobald eine Internetverbindung besteht, um die verbleibenden Anwendungen zu installieren.

---

## Wiederherstellungsschritte

Führen Sie die folgenden Schritte aus, um Ihre Anwendungen auf einer frischen Windows-Installation wiederherzustellen.

### So führen Sie das Online-Skript aus
1. Kopieren Sie `RestoreApps.bat` oder `RestoreApps.ps1` auf den Zielcomputer.
2. Klicken Sie mit der rechten Maustaste auf das Skript und wählen Sie **"Als Administrator ausführen"**.
3. Ein Eingabeaufforderungsfenster öffnet sich. Wenn Winget nicht installiert ist, weist Sie das Skript darauf hin und bietet Anweisungen zur Installation.
4. Warten Sie, bis die automatische Installation abgeschlossen ist.

### So führen Sie das Offline-Paket aus
1. Kopieren Sie den exportierten Ordner mit `install_all.bat` und den Installationsprogrammen auf den Zielcomputer.
2. Klicken Sie mit der rechten Maustaste auf `install_all.bat` und wählen Sie **"Als Administrator ausführen"**.
3. Alle lokalen Installationsprogramme werden im Hintergrund oder mit einfachen Fortschrittsanzeigen ausgeführt.
4. Wenn eine `RestoreOnlineFallback.bat` erstellt wurde, führen Sie diese als Administrator aus, nachdem Sie den Computer mit dem Internet verbunden haben, um die verbleibenden Anwendungen herunterzuladen.

---

## Fehlerbehebung bei häufigen Problemen

### 1. Winget fehlt auf dem Zielcomputer
**Symptom**: Das Wiederherstellungsskript warnt, dass der Befehl `winget` nicht gefunden wurde.
- **Lösung**: Winget ist auf Windows 11 und neueren Builds von Windows 10 vorinstalliert. Falls es fehlt:
  1. Öffnen Sie den Microsoft Store und suchen Sie nach **"App-Installer"**.
  2. Klicken Sie auf **"Aktualisieren"** oder **"Installieren"**.
  3. Alternativ können Sie das neueste `.msixbundle` aus dem offiziellen [GitHub-Repository](https://github.com/microsoft/winget-cli/releases) herunterladen und ausführen.

### 2. Apps werden beim Offline-Download übersprungen
**Symptom**: Das PAS-Protokoll warnt vor übersprungenen Downloads, und einige Apps befinden sich nicht im Offline-Ordner.
- **Erklärung**: Dies ist ein normales Verhalten aufgrund von Lizenz- oder Herausgeberbeschränkungen. Das Installationsprogramm kann nicht vorab heruntergeladen werden. Diese Anwendungen werden automatisch in das Online-Fallback-Skript verschoben.

### 3. Berechtigungsfehler
**Symptom**: Skripte lassen sich nicht ausführen oder brechen mit Fehlern ab.
- **Lösung**: Stellen Sie sicher, dass Sie die Skripte **als Administrator ausführen**. Viele Desktop-Anwendungen erfordern Administratorrechte, um in `C:\Program Files` zu schreiben und Systemdienste zu registrieren.

### 4. Protokollierung und Diagnose
Wenn die Anwendung abstürzt oder ein Vorgang fehlschlägt, finden Sie die Protokolle hier:
- **Speicherort**: `%LocalAppData%\PAS\PAS.log` (in die Adressleiste des Windows-Explorers einfügen).
- **Eigenschaften**:
  - Wird automatisch rotiert, wenn die Größe **5 MB** überschreitet.
  - Enthält vollständige Details zu Ausnahmen und Ausgaben der Winget-CLI zur schnellen Fehlerdiagnose.

# User Guide for Portable App Sync — How to Back Up and Restore Installed Windows Applications

> [!NOTE]
> **Quick Summary**
> - Back up all your installed desktop programs to a lightweight script or offline package before reinstalling Windows.
> - Restore your setup with a single click using Windows Package Manager (Winget).
> - Smart filtering hides system files, runtimes, and dependencies by default.
> - Automatically handles non-downloadable programs using online fallback scripts.

---

## Introduction to Portable App Sync

Reinstalling Windows or setting up a new PC is often a painful process because you need to manually find, download, and install all of your applications. **Portable App Sync (PAS)** is a free, lightweight, and portable utility designed to automate the backup and restore of Windows applications. Leveraging the official Microsoft Windows Package Manager (Winget), PAS helps you scan your system, filter out junk libraries, and export your application list into executable scripts or hybrid offline installers. By using PAS, you can save hours of setup time and ensure that your new system has exactly the programs you need.

### Application Workflow

The diagram below outlines the overall workflow of backing up and restoring your applications using PAS:

```mermaid
flowchart TD
    Start([Launch PAS.exe]) --> Scan[Scan System via Winget]
    Scan --> Populate[Populate Interactive UI List]
    
    subgraph Filtering & Selection
        Populate --> Filter{Apply Filters}
        Filter -- "Show System/Hidden" --> ShowAll[Show All Packages]
        Filter -- "User Apps Only (Default)" --> ShowUser[Show Desktop Apps]
        ShowAll & ShowUser --> Select[Select Apps via Checkboxes]
    end
    
    Select --> Mode{Choose Export Mode}
    
    subgraph Exporting
        Mode -- "Online Script" --> GenOnline[Generate RestoreApps.bat / .ps1]
        Mode -- "Offline Package" --> DownLocal[Download installers to local folder]
        DownLocal --> CheckComp{Supported by Winget Download?}
        CheckComp -- Yes --> SaveInst[Save installer file]
        CheckComp -- No --> MarkSkip[Mark as Skipped & Add to Fallback]
        SaveInst & MarkSkip --> GenOffline[Generate install_all.bat & RestoreOnlineFallback.bat]
    end

    GenOnline & GenOffline --> End([Ready to restore on new system])
```

---

## Key Features and Capabilities

### Application Discovery and Scan
Upon startup, Portable App Sync performs a fast system-wide scan to detect all installed software. It fetches the official human-readable name, the package identifier (Package ID), and description details in the background.

### Smart Filtering System
To keep your backup clean, PAS differentiates between user-installed programs and system overhead:
- **User Desktop Applications**: This covers your primary software like browsers, editors, and players.
- **Microsoft Store Apps**: Pre-installed UWP/MSIX packages (Xbox, Calculator, Photos) that are normally handled by the system account.
- **System Components and Runtimes**: Drivers, SDKs, and Visual C++ runtimes that are typically reinstalled automatically or bundled.
- **Technical Dependencies**: Low-level framework runtimes like `WindowsAppRuntime`.

### Multiple Export Modes
- **Online Script**: Generates a compact command-line batch (`.bat`) or PowerShell (`.ps1`) script. When run on the new system, it instructs Winget to fetch and install the latest versions of your selected programs directly from the official repositories.
- **Offline Package**: Downloads the offline installers of all compatible applications into a local directory. For apps that restrict direct installer downloading (e.g. Visual Studio Code, Git, Android Studio), PAS automatically falls back to an online backup script, creating a hybrid installer set.

---

## Step-by-Step Setup Guide

Follow these simple steps to successfully back up and restore your Windows programs:

1. **Step 1: Launch the application** — Copy `PAS.exe` to a convenient location (like your Desktop or a USB drive) and run it. No installation is required.
2. **Step 2: Filter and select applications** — Review the list of scanned applications. Check the boxes next to the software you want to save. If you need system runtimes or Store apps, check the **"Show system and hidden applications"** option.
3. **Step 3: Select export mode** — Choose between **Online Script** (lightweight, requires internet during restore) or **Offline Package** (downloads installer files locally).
4. **Step 4: Export your backup** — Click the export button, select the directory where you want to save the backup, and wait for the operation to complete.
5. **Step 5: Run restoration on the new system** — Copy the exported files to the target machine. Right-click the restore script (such as `RestoreApps.bat` or `install_all.bat`) and select **"Run as Administrator"** to begin automated installation.

---

## Keyboard Shortcuts and Operation Tips

Although Portable App Sync features a clean graphical user interface, you can navigate it easily using standard Windows keyboard controls:

| Shortcut / Command | Action / Purpose |
| --- | --- |
| `Tab` | Move keyboard focus between search bars, the application table, filters, and export buttons. |
| `Space` | Select or deselect the application check box currently in focus. |
| `Arrow Up / Down` | Navigate through the table list of scanned desktop applications. |
| `Alt + F4` | Instantly close the Portable App Sync application. |
| `Enter` | Trigger the selected filter button or execute the export command. |

### Pro Tips for Efficiency
- **Column Sorting**: Click any column header (such as Name, Source, or Package ID) to sort the list and quickly find specific tools.
- **Search Filtering**: Type inside the search box at the top to instantly filter programs by name or package ID.
- **Admin Execution**: Always run your exported recovery scripts as Administrator to prevent third-party installers from prompting or failing due to permissions.

---

## Frequently Asked Questions and Troubleshooting

### What should I do if Winget is missing on the target computer?
Winget is included by default in Windows 11 and recent builds of Windows 10. If it is missing, the restore script will automatically warn you. To install it manually, open the Microsoft Store, search for **"App Installer"**, and update it. Alternatively, download the latest package from the official [GitHub repository](https://github.com/microsoft/winget-cli/releases).

### Why are some applications skipped during the offline package export?
Some software publishers (such as Microsoft for Visual Studio Code, Git, or Google for Android Studio) prohibit direct downloading of their installers via the Winget API. When this happens, PAS skips downloading them and adds them to `RestoreOnlineFallback.bat`. To recover these, run `install_all.bat` first, connect to the internet, and then run `RestoreOnlineFallback.bat`.

### Why does the restoration script fail or prompt for permission?
Most standard Windows applications write files to `C:\Program Files` and register system services, which require local administrative privileges. Ensure that you right-click the script and select **"Run as Administrator"**.

### Where can I check log files if an export fails?
Portable App Sync logs all background operations in a text file. You can open it by pasting `%LocalAppData%\PAS\PAS.log` into the Windows Explorer address bar. The file is automatically rotated when it exceeds 5 MB.

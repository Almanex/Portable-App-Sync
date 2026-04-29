# Quick Start — PAS (Portable App Sync)

🇷🇺 **[Russian version / Русская версия](QUICKSTART_RU.md)**

## For Users

### Where to find the ready-to-use application?

The portable executable is located here:
```
bin\Release\net10.0-windows\win-x64\publish\PAS.exe
```

Just copy this file anywhere and run it — no installation required!

### How to use?

1. **Run** `PAS.exe`
2. **Wait** for automatic scanning (only user desktop applications are indexed)
3. **Select** the applications you want to save (checkboxes)
   - Use the view filter to quickly show only offline-ready apps, online fallback, or excluded items
4. **Select mode**:
   - 📄 **Online Script** — if the new system will have internet (recommended)
   - 💾 **Offline Package** — if you need a hybrid set: local installers + fallback script for apps that cannot be downloaded offline
5. **Click** the export button

> **💡 Tip**: Hidden applications (Microsoft Store, drivers, system libraries) are filtered out by default. You can enable them at any time by checking **"Show system and hidden applications"**. You can also hover over any program in the table to read its official description.

### After Windows Reinstallation

**If you chose "Online Script":**
- Run the created `.bat` or `.ps1` file
- All applications will be installed automatically

**If you chose "Offline Package":**
- Copy the folder with files to the new system
- Run `install_all.bat`
- If `RestoreOnlineFallback.bat` is present, run it after offline installation on a machine with internet

After export, PAS shows a summary and a button to open the export folder.

## For Developers

### Requirements

- .NET 10 SDK
- Windows 10/11

### Installing .NET 10 SDK

```powershell
winget install Microsoft.DotNet.SDK.10
```

### Development Commands

```powershell
# Cloning (if from Git)
git clone https://github.com/Almanex/Almanex-PAS_Portable-App-Sync.git
cd PAS

# Build
dotnet build

# Run for testing
dotnet run

# Create portable version (single exe file)
dotnet publish -c Release
```

Ready file: `dist\PAS.exe`

### Project Structure

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
├── ViewModels/          # MVVM ViewModels
│   ├── MainViewModel.cs
│   └── RelayCommand.cs
├── Views/               # WPF UI
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── HelpWindow.xaml
│   └── HelpWindow.xaml.cs
├── Converters/          # XAML converters
└── README.md            # Full documentation
```

### Architecture

- **Pattern:** MVVM (Model-View-ViewModel)
- **UI:** WPF with data binding
- **Integration:** Winget CLI via Process API
- **Async:** All operations are asynchronous (async/await)

## Useful Links

- [README.md](README.md) — Full documentation
- [README_RU.md](README_RU.md) — Russian version

## Support

If something doesn't work:
1. Check Winget presence: `winget --version`
2. Check log: `%LocalAppData%\PAS\PAS.log`
3. Ensure you have Windows 10 (1809+) or Windows 11

### Offline Download Errors

Some applications (VS Code, Git, Android Studio) **do not support downloading distributions** via `winget download`. This is normal: PAS will mark them as skipped and automatically create `RestoreOnlineFallback.bat` for subsequent online installation.

Service components like `Microsoft Edge Update` are excluded from export by default. To check what's excluded, enable system/hidden apps and select the "Excluded by Default" filter.

## License

MIT License — use freely!

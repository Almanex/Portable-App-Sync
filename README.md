# PAS (Portable App Sync)

**A portable utility to automate backup and restore of installed Windows applications**

- 📝 **Automatic logging** of all operations
- 🚀 **Portable** — no installation required, single `.exe` file
- 🎯 **Smart filtering** — by default indexes only user applications (with option to show hidden system components)
- ℹ️ **Detailed info** — background loading of official descriptions and human-readable names. If the app name is not yet loaded, the package ID is displayed instead (they are often identical).

🇷🇺 **[Russian version of README / Русская версия](README_RU.md)**

## 🛠️ Technologies

- **.NET 10.0** — modern development platform
- **WPF** — Windows Presentation Foundation for UI
- **Winget** — Windows Package Manager for package management
- **MVVM** — architectural pattern for separation of logic and presentation

## 📥 System Requirements

- **OS**: Windows 10 (version 1809+) or Windows 11
- **Winget**: Must be installed (usually pre-installed in Windows 11)
- **.NET Runtime**: Not required (self-contained build)

## 🚀 Usage

### Creating a Backup

1. Run `PAS.exe`
2. Wait for system scanning to complete (only user desktop applications are indexed)
3. Select the applications you want to save
   - The filter **All Visible / Available Offline / Online Fallback / Excluded by Default** helps quickly check what will go into the offline package, fallback script, or be excluded
4. Select export mode:
   - **Online Script** — creates a `.bat` or `.ps1` file for auto-installation via winget (recommended)
   - **Offline Package** — creates a hybrid set: downloads available distributions and adds an online fallback script for unsupported applications if necessary
5. Click the export button and specify the save location
6. After completion, use the summary of the last export and the **Open Export Folder** button

### Restoring Applications

#### "Online Script" Mode
1. Copy the created `RestoreApps.bat` or `RestoreApps.ps1` file to the new system
2. Run the file as administrator
3. Wait for all applications to be installed automatically

#### "Offline Package" Mode
1. Copy the folder with downloaded distributions to the new system
2. Run `install_all.bat` as administrator
3. If `RestoreOnlineFallback.bat` was created, run it after offline installation on a machine with internet access
4. Applications available for offline will be installed from local files, while unsupported ones will be delivered via `winget install`

## 🏗️ Building from Source

### Development Requirements

- .NET 10 SDK
- Visual Studio 2022 or JetBrains Rider (optional)

### Build Commands

```powershell
# Clone the repository
git clone https://github.com/Almanex/Almanex-PAS_Portable-App-Sync.git
cd PAS

# Build the project
dotnet build

# Run the application
dotnet run

# Create a portable executable (single file)
dotnet publish -c Release
```

The final `.exe` file will be located in `dist\PAS.exe`

## 📂 Project Structure

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
├── Views/               # UI views
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── HelpWindow.xaml
│   └── HelpWindow.xaml.cs
├── Converters/          # XAML converters
│   └── ValueConverters.cs
├── App.xaml             # Entry point
├── App.xaml.cs
├── AssemblyInfo.cs
└── icon.ico             # Application icon
```

## ⚠️ Important Notes

### Application Filtering

By default, the application hides:
- **Microsoft Store Apps** (MSIX packages: Calculator, Photos, Xbox, etc.)
- **System Components** (Visual C++ Redistributable, .NET Runtime, drivers)
- **Technical Dependencies** (WindowsAppRuntime, VCLibs, etc.)

Only **user desktop applications** installed via `.exe` or `.msi` installers are displayed in the list.
💡 **If you need hidden packages:** you can instantly disable this filter in the interface by checking *"Show system and hidden applications"*.

Service components and updater packages, such as `Microsoft Edge Update`, are excluded from export by default. They don't disappear completely: enable system/hidden apps and select the **Excluded by Default** filter to see what exactly was excluded.

### Winget Check

In older Windows 10 builds, Winget might be missing. Scripts automatically check for its presence and provide instructions for installing App Installer from the Microsoft Store.

### Offline Mode Limitations

**Not all applications support downloading distributions** via `winget download`. Examples:
- **Visual Studio Code** (`Microsoft.VisualStudioCode`)
- **Git** (`Git.Git`)
- **Android Studio** (`Google.AndroidStudio`)
- And other applications where manufacturers restricted direct downloading

For such applications, PAS now uses a **hybrid offline mode**:
- ✅ Supported packages are downloaded locally to the selected folder
- ✅ An `RestoreOnlineFallback.bat` is automatically created for unsupported packages
- ✅ As a result, export doesn't fail due to these apps: they are marked as `Skipped` and moved to the fallback script

Recommended restore order:
1. Run `install_all.bat`
2. Then, if it was created, run `RestoreOnlineFallback.bat` on a machine with internet

**Recommendation**: Use the "📄 Online Script" mode if you want a single universal scenario without separating into offline and fallback.

### 🛡️ Security and Reliability

The project is designed with a focus on industrial-grade stability and security:

- 🛡️ **Injection Protection**: All interaction with Winget CLI passes through strict package ID validation (`SafeIdPattern`). This eliminates the possibility of executing arbitrary commands through app name manipulation.
- ⚙️ **Process Stability**: Hard timeouts (120s) are implemented for all external calls. If a Winget process hangs, the application correctly terminates its process tree, leaving no "zombie" processes.
- 💾 **Data Integrity**: The caching system is protected from corrupted data and memory overflow (50 MB limit per file). JSON structure is validated before reading.
- 🔄 **Log Rotation**: Automatic log file size management (5 MB limit) prevents uncontrolled disk data growth.
- 🏗️ **DI Architecture (Dependency Injection)**: Use of a dependency container ensures component isolation, simplifies testing, and eliminates service initialization errors.
- 🚦 **Thread Safety**: All interface updates and background tasks are synchronized, eliminating crashes during parallel description loading or app installation.

All operations are transparent and logged in `PAS.log` in real-time.

## 📝 Logging

All operations are recorded in the `PAS.log` file in the `%LocalAppData%\PAS\` folder. The log contains:
- Information messages on progress
- Warnings about issues
- Errors with full stack traces
- Fatal exceptions (if the application crashed)

## 🤝 Contributing

Any improvements are welcome! Please:
1. Fork the project
2. Create a branch for your changes
3. Submit a Pull Request

## 📄 License

This project is distributed under the MIT License. See the `LICENSE` file for details.

## 🙏 Acknowledgments

- Microsoft for [Winget](https://github.com/microsoft/winget-cli)
- .NET Community for excellent development tools

---

**Note**: This application uses the official Windows Package Manager (Winget) and contains no malicious code. All operations are transparent and can be verified in the source code.

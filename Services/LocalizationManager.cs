using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PAS.Services;

/// <summary>
/// Менеджер локализации — синглтон с поддержкой EN / RU / DE.
/// Строки хранятся прямо в коде, без внешних RESX-файлов.
/// При смене языка поднимает PropertyChanged, и весь UI обновляется через биндинг.
/// </summary>
public class LocalizationManager : INotifyPropertyChanged
{
    // ─── Singleton ──────────────────────────────────────────────────────────
    private static LocalizationManager? _instance;
    public static LocalizationManager Instance => _instance ??= new LocalizationManager();

    // ─── State ──────────────────────────────────────────────────────────────
    private string _currentLanguage = "en";
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value) return;
            _currentLanguage = value;
            // Notify every localised property
            OnPropertyChanged(string.Empty);
        }
    }

    public static readonly List<string> SupportedLanguages = new() { "en", "ru", "de" };
    public static readonly Dictionary<string, string> LanguageDisplayNames = new()
    {
        ["en"] = "English",
        ["ru"] = "Русский",
        ["de"] = "Deutsch"
    };

    // ─── Translation table ───────────────────────────────────────────────────
    private static readonly Dictionary<string, Dictionary<string, string>> T = new()
    {
        ["WindowTitle"] = new()
        {
            ["en"] = "PAS — Portable App Sync",
            ["ru"] = "PAS — Portable App Sync",
            ["de"] = "PAS — Portable App Sync"
        },
        ["HelpButton"] = new()
        {
            ["en"] = "📖 Help",
            ["ru"] = "📖 Справка",
            ["de"] = "📖 Hilfe"
        },
        ["AboutButton"] = new()
        {
            ["en"] = "About",
            ["ru"] = "О программе",
            ["de"] = "Über"
        },
        ["AboutWindowTitle"] = new()
        {
            ["en"] = "About PAS",
            ["ru"] = "О программе PAS",
            ["de"] = "Über PAS"
        },
        ["AboutAppTitle"] = new()
        {
            ["en"] = "Portable App Sync (PAS)",
            ["ru"] = "Portable App Sync (PAS)",
            ["de"] = "Portable App Sync (PAS)"
        },
        ["AboutAppDescription"] = new()
        {
            ["en"] = "Portable utility for backup and restore of Windows applications.",
            ["ru"] = "Portable-утилита для автоматизации резервного копирования и восстановления приложений Windows.",
            ["de"] = "Portable Dienstprogramm zur Sicherung und Wiederherstellung von Windows-Anwendungen."
        },
        ["AboutVersion"] = new()
        {
            ["en"] = "Version 1.0.0",
            ["ru"] = "Версия 1.0.0",
            ["de"] = "Version 1.0.0"
        },
        ["AboutDeveloper"] = new()
        {
            ["en"] = "Developer: Almanex",
            ["ru"] = "Разработчик: Almanex",
            ["de"] = "Entwickler: Almanex"
        },
        ["AboutGithub"] = new()
        {
            ["en"] = "GitHub Repository",
            ["ru"] = "Репозиторий проекта",
            ["de"] = "GitHub Repository"
        },
        ["AboutLicense"] = new()
        {
            ["en"] = "License: MIT",
            ["ru"] = "Лицензия: MIT",
            ["de"] = "Lizenz: MIT"
        },
        ["SearchPlaceholder"] = new()
        {
            ["en"] = "Search applications...",
            ["ru"] = "Поиск приложений...",
            ["de"] = "Anwendungen suchen..."
        },
        ["RefreshButton"] = new()
        {
            ["en"] = "🔄 Refresh",
            ["ru"] = "🔄 Обновить",
            ["de"] = "🔄 Aktualisieren"
        },
        ["SelectAll"] = new()
        {
            ["en"] = "✓ Select All",
            ["ru"] = "✓ Выбрать все",
            ["de"] = "✓ Alle auswählen"
        },
        ["UnselectAll"] = new()
        {
            ["en"] = "✗ Deselect All",
            ["ru"] = "✗ Снять все",
            ["de"] = "✗ Alle abwählen"
        },
        ["ShowSystemApps"] = new()
        {
            ["en"] = "Show system and hidden apps",
            ["ru"] = "Показывать системные и скрытые приложения",
            ["de"] = "System- und versteckte Apps anzeigen"
        },
        ["AppFilterLabel"] = new()
        {
            ["en"] = "View:",
            ["ru"] = "Вид:",
            ["de"] = "Ansicht:"
        },
        ["AppFilterAll"] = new()
        {
            ["en"] = "All visible",
            ["ru"] = "Все видимые",
            ["de"] = "Alle sichtbaren"
        },
        ["AppFilterOfflineReady"] = new()
        {
            ["en"] = "Offline-ready",
            ["ru"] = "Доступны оффлайн",
            ["de"] = "Offline bereit"
        },
        ["AppFilterOnlineFallback"] = new()
        {
            ["en"] = "Online fallback",
            ["ru"] = "Online fallback",
            ["de"] = "Online-Fallback"
        },
        ["AppFilterExcluded"] = new()
        {
            ["en"] = "Excluded by default",
            ["ru"] = "Исключены по умолчанию",
            ["de"] = "Standardmäßig ausgeschlossen"
        },
        ["SelectedCountFormat"] = new()
        {
            ["en"] = "Selected: {0}",
            ["ru"] = "Выбрано: {0}",
            ["de"] = "Ausgewählt: {0}"
        },
        // ── DataGrid columns ──
        ["ColName"] = new()
        {
            ["en"] = "Name",
            ["ru"] = "Название",
            ["de"] = "Name"
        },
        ["ColPackageId"] = new()
        {
            ["en"] = "Package ID",
            ["ru"] = "ID пакета",
            ["de"] = "Paket-ID"
        },
        ["ColVersion"] = new()
        {
            ["en"] = "Version",
            ["ru"] = "Версия",
            ["de"] = "Version"
        },
        ["ColStatus"] = new()
        {
            ["en"] = "Status",
            ["ru"] = "Статус",
            ["de"] = "Status"
        },
        ["OfflineWarningTooltip"] = new()
        {
            ["en"] = "This app cannot be downloaded offline and will be added to the online fallback script",
            ["ru"] = "Это приложение нельзя скачать оффлайн, оно будет добавлено в online fallback-скрипт",
            ["de"] = "Diese App kann nicht offline heruntergeladen werden und wird zum Online-Fallback-Skript hinzugefügt"
        },
        // ── Export section ──
        ["ExportGroupHeader"] = new()
        {
            ["en"] = "Export mode",
            ["ru"] = "Режим экспорта",
            ["de"] = "Exportmodus"
        },
        ["ExportOnlineOption"] = new()
        {
            ["en"] = "📄 Online script (create auto-install file)",
            ["ru"] = "📄 Онлайн-скрипт (создать файл автоустановки)",
            ["de"] = "📄 Online-Skript (Auto-Installationsdatei erstellen)"
        },
        ["ExportOfflineOption"] = new()
        {
            ["en"] = "💾 Offline package + online fallback",
            ["ru"] = "💾 Оффлайн-пакет + online fallback",
            ["de"] = "💾 Offline-Paket + Online-Fallback"
        },
        ["ExportButtonOnline"] = new()
        {
            ["en"] = "📄 Create Script",
            ["ru"] = "📄 Создать скрипт",
            ["de"] = "📄 Skript erstellen"
        },
        ["ExportButtonOffline"] = new()
        {
            ["en"] = "💾 Download Package",
            ["ru"] = "💾 Скачать пакет",
            ["de"] = "💾 Paket herunterladen"
        },
        ["CancelButton"] = new()
        {
            ["en"] = "✕ Cancel",
            ["ru"] = "✕ Отменить",
            ["de"] = "✕ Abbrechen"
        },
        ["LanguageLabel"] = new()
        {
            ["en"] = "Language:",
            ["ru"] = "Язык:",
            ["de"] = "Sprache:"
        },
        // ── Status messages (used in ViewModel) ──
        ["StatusReady"] = new()
        {
            ["en"] = "Ready",
            ["ru"] = "Готов к работе",
            ["de"] = "Bereit"
        },
        ["StatusScanning"] = new()
        {
            ["en"] = "Scanning system...",
            ["ru"] = "Сканирование системы...",
            ["de"] = "System wird gescannt..."
        },
        ["StatusWingetNotFound"] = new()
        {
            ["en"] = "Winget not found",
            ["ru"] = "Winget не найден",
            ["de"] = "Winget nicht gefunden"
        },
        ["StatusAppsFound"] = new()
        {
            ["en"] = "Found: {0} apps",
            ["ru"] = "Найдено: {0} приложений",
            ["de"] = "Gefunden: {0} Apps"
        },
        ["StatusExporting"] = new()
        {
            ["en"] = "Exporting...",
            ["ru"] = "Экспорт...",
            ["de"] = "Wird exportiert..."
        },
        ["StatusExportDone"] = new()
        {
            ["en"] = "Export complete!",
            ["ru"] = "Экспорт завершён!",
            ["de"] = "Export abgeschlossen!"
        },
        ["StatusExportError"] = new()
        {
            ["en"] = "Export error",
            ["ru"] = "Ошибка экспорта",
            ["de"] = "Exportfehler"
        },
        ["AppStatusReady"] = new()
        {
            ["en"] = "Ready",
            ["ru"] = "Готово",
            ["de"] = "Bereit"
        },
        ["AppStatusExcluded"] = new()
        {
            ["en"] = "Excluded",
            ["ru"] = "Исключено",
            ["de"] = "Ausgeschlossen"
        },
        ["DescriptionLoading"] = new()
        {
            ["en"] = "Loading description...",
            ["ru"] = "Загрузка описания...",
            ["de"] = "Beschreibung wird geladen..."
        },
        ["DescriptionUnavailable"] = new()
        {
            ["en"] = "No description available",
            ["ru"] = "Описание отсутствует",
            ["de"] = "Keine Beschreibung vorhanden"
        },
        // ── Winget missing dialog ──
        ["WingetMissingTitle"] = new()
        {
            ["en"] = "Winget not found",
            ["ru"] = "Winget не найден",
            ["de"] = "Winget nicht gefunden"
        },
        ["WingetMissingBody"] = new()
        {
            ["en"] = "Windows Package Manager (Winget) was not found.\n\nInstall App Installer from Microsoft Store:\nhttps://www.microsoft.com/p/app-installer/9nblggh4nns1",
            ["ru"] = "Windows Package Manager (Winget) не найден в системе.\n\nУстановите App Installer из Microsoft Store:\nhttps://www.microsoft.com/p/app-installer/9nblggh4nns1",
            ["de"] = "Windows Package Manager (Winget) wurde nicht gefunden.\n\nInstallieren Sie App Installer aus dem Microsoft Store:\nhttps://www.microsoft.com/p/app-installer/9nblggh4nns1"
        },
        ["ExportSelectFolder"] = new()
        {
            ["en"] = "Select export folder",
            ["ru"] = "Выберите папку для экспорта",
            ["de"] = "Exportordner auswählen"
        },
        ["ExportSuccessTitle"] = new()
        {
            ["en"] = "Export complete",
            ["ru"] = "Экспорт завершён",
            ["de"] = "Export abgeschlossen"
        },
        ["ExportErrorTitle"] = new()
        {
            ["en"] = "Export error",
            ["ru"] = "Ошибка экспорта",
            ["de"] = "Exportfehler"
        },
        ["ExportSaveScriptTitle"] = new()
        {
            ["en"] = "Save auto-installation script",
            ["ru"] = "Сохранить скрипт автоустановки",
            ["de"] = "Auto-Installationsskript speichern"
        },
        ["StatusCreatingScript"] = new()
        {
            ["en"] = "Creating script...",
            ["ru"] = "Создание скрипта...",
            ["de"] = "Skript wird erstellt..."
        },
        ["StatusDownloading"] = new()
        {
            ["en"] = "Downloading installers...",
            ["ru"] = "Загрузка дистрибутивов...",
            ["de"] = "Installationsdateien werden heruntergeladen..."
        },
        ["ExportDownloadProgress"] = new()
        {
            ["en"] = "Downloading {0} of {1}: {2}",
            ["ru"] = "Загрузка {0} из {1}: {2}",
            ["de"] = "Herunterladen {0} von {1}: {2}"
        },
        ["ExportCancelByUser"] = new()
        {
            ["en"] = "Cancelled by user",
            ["ru"] = "Отменено пользователем",
            ["de"] = "Vom Benutzer abgebrochen"
        },
        ["ExportSelectOne"] = new()
        {
            ["en"] = "Please select at least one application",
            ["ru"] = "Выберите хотя бы одно приложение",
            ["de"] = "Bitte wählen Sie mindestens eine Anwendung aus"
        },
        ["ExportOnlyExcludedSelected"] = new()
        {
            ["en"] = "Only excluded service/system items are selected ({0}). They are shown for transparency, but are not exported by default.",
            ["ru"] = "Выбраны только исключенные служебные/системные элементы ({0}). Они показаны для прозрачности, но по умолчанию не экспортируются.",
            ["de"] = "Es sind nur ausgeschlossene Dienst-/Systemelemente ausgewählt ({0}). Sie werden transparent angezeigt, aber standardmäßig nicht exportiert."
        },
        ["ExportExcludedIgnored"] = new()
        {
            ["en"] = "Excluded from export by default: {0}",
            ["ru"] = "Исключено из экспорта по умолчанию: {0}",
            ["de"] = "Standardmäßig vom Export ausgeschlossen: {0}"
        },
        ["ExportWarning"] = new()
        {
            ["en"] = "Warning",
            ["ru"] = "Предупреждение",
            ["de"] = "Warnung"
        },
        ["ExportSuccess"] = new()
        {
            ["en"] = "Success",
            ["ru"] = "Успешно",
            ["de"] = "Erfolgreich"
        },
        ["ExportFailedToProcess"] = new()
        {
            ["en"] = "Failed to process {0} applications",
            ["ru"] = "Не удалось обработать {0} приложений",
            ["de"] = "{0} Anwendungen konnten nicht verarbeitet werden"
        },
        ["ExportDoneStatus"] = new()
        {
            ["en"] = "Export complete",
            ["ru"] = "Экспорт завершен",
            ["de"] = "Export abgeschlossen"
        },
        ["ExportCompleteSummary"] = new()
        {
            ["en"] = "Export complete. Successful: {0}, Errors: {1}, Skipped: {2}",
            ["ru"] = "Экспорт завершен. Успешно: {0}, Ошибок: {1}, Пропущено: {2}",
            ["de"] = "Export abgeschlossen. Erfolgreich: {0}, Fehler: {1}, Übersprungen: {2}"
        },
        ["ExportCriticalDownloadError"] = new()
        {
            ["en"] = "Critical download error: {0}",
            ["ru"] = "Критическая ошибка при загрузке: {0}",
            ["de"] = "Kritischer Downloadfehler: {0}"
        },
        ["ExportSkippedStoreReason"] = new()
        {
            ["en"] = "{0} (Microsoft Store apps do not support offline download)",
            ["ru"] = "{0} (приложения Microsoft Store не поддерживают оффлайн-загрузку)",
            ["de"] = "{0} (Microsoft Store-Apps unterstützen keinen Offline-Download)"
        },
        ["ExportSkippedFallbackReason"] = new()
        {
            ["en"] = "{0} (will be added to the online fallback script)",
            ["ru"] = "{0} (будет добавлено в online fallback script)",
            ["de"] = "{0} (wird dem Online-Fallback-Skript hinzugefügt)"
        },
        ["AppStatusDownloading"] = new()
        {
            ["en"] = "Downloading...",
            ["ru"] = "Загрузка...",
            ["de"] = "Wird heruntergeladen..."
        },
        ["AppStatusDownloaded"] = new()
        {
            ["en"] = "Downloaded",
            ["ru"] = "Загружено",
            ["de"] = "Heruntergeladen"
        },
        ["AppStatusSkipped"] = new()
        {
            ["en"] = "Skipped",
            ["ru"] = "Пропущено",
            ["de"] = "Übersprungen"
        },
        ["AppStatusSkippedStore"] = new()
        {
            ["en"] = "Skipped (Store)",
            ["ru"] = "Пропущено (Store)",
            ["de"] = "Übersprungen (Store)"
        },
        ["AppStatusDownloadError"] = new()
        {
            ["en"] = "Download error",
            ["ru"] = "Ошибка загрузки",
            ["de"] = "Downloadfehler"
        },
        ["AppStatusError"] = new()
        {
            ["en"] = "Error",
            ["ru"] = "Ошибка",
            ["de"] = "Fehler"
        },
        ["OpenExportFolder"] = new()
        {
            ["en"] = "Open export folder",
            ["ru"] = "Открыть папку экспорта",
            ["de"] = "Exportordner öffnen"
        },
        ["LastExportSummaryHeader"] = new()
        {
            ["en"] = "Last export",
            ["ru"] = "Последний экспорт",
            ["de"] = "Letzter Export"
        },
        ["ExportSummarySuccess"] = new()
        {
            ["en"] = "Downloaded/created: {0}",
            ["ru"] = "Скачано/создано: {0}",
            ["de"] = "Heruntergeladen/erstellt: {0}"
        },
        ["ExportSummaryFallback"] = new()
        {
            ["en"] = "Added to online fallback: {0}",
            ["ru"] = "Добавлено в online fallback: {0}",
            ["de"] = "Zum Online-Fallback hinzugefügt: {0}"
        },
        ["ExportSummarySkipped"] = new()
        {
            ["en"] = "Skipped: {0}",
            ["ru"] = "Пропущено: {0}",
            ["de"] = "Übersprungen: {0}"
        },
        ["ExportSummaryErrors"] = new()
        {
            ["en"] = "Errors: {0}",
            ["ru"] = "Ошибок: {0}",
            ["de"] = "Fehler: {0}"
        },
        ["ExportSummaryExcluded"] = new()
        {
            ["en"] = "Excluded service/system items: {0}",
            ["ru"] = "Исключено служебных/системных элементов: {0}",
            ["de"] = "Ausgeschlossene Dienst-/Systemelemente: {0}"
        },
        ["ExportSummaryFallbackPath"] = new()
        {
            ["en"] = "Fallback script: {0}",
            ["ru"] = "Fallback-скрипт: {0}",
            ["de"] = "Fallback-Skript: {0}"
        },
        ["ExportSummaryOutputPath"] = new()
        {
            ["en"] = "Output: {0}",
            ["ru"] = "Результат: {0}",
            ["de"] = "Ausgabe: {0}"
        },
        ["ScriptInstallTitle"] = new()
        {
            ["en"] = "Installing downloaded applications",
            ["ru"] = "Установка загруженных приложений",
            ["de"] = "Installation der heruntergeladenen Anwendungen"
        },
        ["ScriptInstallingApp"] = new()
        {
            ["en"] = "Installing: {0}",
            ["ru"] = "Установка: {0}",
            ["de"] = "Installation: {0}"
        },
        ["ScriptInstallDone"] = new()
        {
            ["en"] = "Installation complete!",
            ["ru"] = "Установка завершена!",
            ["de"] = "Installation abgeschlossen!"
        },
        ["ScriptCreated"] = new()
        {
            ["en"] = "Script created successfully: {0}",
            ["ru"] = "Скрипт успешно создан: {0}",
            ["de"] = "Skript erfolgreich erstellt: {0}"
        },
        ["PowerShellScriptCreated"] = new()
        {
            ["en"] = "PowerShell script created successfully: {0}",
            ["ru"] = "PowerShell скрипт успешно создан: {0}",
            ["de"] = "PowerShell-Skript erfolgreich erstellt: {0}"
        },
        ["ScriptCreateError"] = new()
        {
            ["en"] = "Error creating script: {0}",
            ["ru"] = "Ошибка при создании скрипта: {0}",
            ["de"] = "Fehler beim Erstellen des Skripts: {0}"
        },

        // ── HelpWindow ──────────────────────────────────────────────────────
        ["HelpWindowTitle"] = new()
        {
            ["en"] = "Help — PAS (Portable App Sync)",
            ["ru"] = "Справка — PAS (Portable App Sync)",
            ["de"] = "Hilfe — PAS (Portable App Sync)"
        },
        ["HelpMainTitle"] = new()
        {
            ["en"] = "📖 User Guide",
            ["ru"] = "📖 Справка по использованию",
            ["de"] = "📖 Benutzerhandbuch"
        },
        ["HelpClose"] = new()
        {
            ["en"] = "Close",
            ["ru"] = "Закрыть",
            ["de"] = "Schließen"
        },
        // Tab headers
        ["HelpTabOverview"] = new()
        {
            ["en"] = "📋 Overview",
            ["ru"] = "📋 Обзор",
            ["de"] = "📋 Übersicht"
        },
        ["HelpTabScan"] = new()
        {
            ["en"] = "🔍 Scanning",
            ["ru"] = "🔍 Сканирование",
            ["de"] = "🔍 Scannen"
        },
        ["HelpTabExport"] = new()
        {
            ["en"] = "💾 Export",
            ["ru"] = "💾 Экспорт",
            ["de"] = "💾 Export"
        },
        ["HelpTabInstall"] = new()
        {
            ["en"] = "⚙️ Installation",
            ["ru"] = "⚙️ Установка",
            ["de"] = "⚙️ Installation"
        },
        ["HelpTabTips"] = new()
        {
            ["en"] = "💡 Tips",
            ["ru"] = "💡 Советы",
            ["de"] = "💡 Tipps"
        },
        // ── Overview tab ──
        ["HelpOverviewHeader"] = new()
        {
            ["en"] = "About",
            ["ru"] = "О программе",
            ["de"] = "Über die App"
        },
        ["HelpOverviewDesc"] = new()
        {
            ["en"] = "PAS (Portable App Sync) is a utility for backing up and restoring installed Windows applications using Windows Package Manager (winget).",
            ["ru"] = "PAS (Portable App Sync) — утилита для резервного копирования и восстановления установленных приложений Windows с использованием Windows Package Manager (winget).",
            ["de"] = "PAS (Portable App Sync) ist ein Werkzeug zur Sicherung und Wiederherstellung installierter Windows-Anwendungen mithilfe des Windows Package Managers (winget)."
        },
        ["HelpOverviewFeaturesHeader"] = new()
        {
            ["en"] = "Key features:",
            ["ru"] = "Основные возможности:",
            ["de"] = "Hauptfunktionen:"
        },
        ["HelpOverviewFeatures"] = new()
        {
            ["en"] = "• Automatic scanning of installed applications\n• Script generation for automated installation\n• Hybrid offline packages with online fallback script\n• Result caching for fast access\n• Warnings for incompatible applications\n• Numbered list with hover descriptions\n• Switch between system and user-only app views",
            ["ru"] = "• Автоматическое сканирование установленных приложений\n• Генерация скриптов для автоматической установки\n• Гибридные оффлайн-пакеты с online fallback-скриптом\n• Кэширование результатов для быстрого доступа\n• Предупреждения о несовместимых приложениях\n• Пронумерованный список с всплывающими описаниями\n• Переключение между пользовательскими и системными приложениями",
            ["de"] = "• Automatisches Scannen installierter Anwendungen\n• Skripterstellung für die automatische Installation\n• Hybride Offline-Pakete mit Online-Fallback-Skript\n• Ergebnis-Caching für schnellen Zugriff\n• Warnungen für inkompatible Anwendungen\n• Nummerierte Liste mit Hover-Beschreibungen\n• Umschalten zwischen System- und Benutzer-Apps"
        },
        ["HelpOverviewReqHeader"] = new()
        {
            ["en"] = "System requirements:",
            ["ru"] = "Системные требования:",
            ["de"] = "Systemanforderungen:"
        },
        ["HelpOverviewReq"] = new()
        {
            ["en"] = "• Windows 10/11\n• Windows Package Manager (winget) v1.0 or higher\n• .NET 10.0 Runtime (bundled)",
            ["ru"] = "• Windows 10/11\n• Windows Package Manager (winget) версии 1.0 или выше\n• .NET 10.0 Runtime (встроен в приложение)",
            ["de"] = "• Windows 10/11\n• Windows Package Manager (winget) Version 1.0 oder höher\n• .NET 10.0 Runtime (enthalten)"
        },
        // ── Scan tab ──
        ["HelpScanHeader"] = new()
        {
            ["en"] = "System Scanning",
            ["ru"] = "Сканирование системы",
            ["de"] = "Systemscan"
        },
        ["HelpScanAutoHeader"] = new()
        {
            ["en"] = "Automatic scan",
            ["ru"] = "Автоматическое сканирование",
            ["de"] = "Automatischer Scan"
        },
        ["HelpScanAutoDesc"] = new()
        {
            ["en"] = "On startup, the application automatically scans the system and displays a list of installed applications available through winget.",
            ["ru"] = "При запуске приложение автоматически сканирует систему и отображает список установленных приложений, доступных через winget.",
            ["de"] = "Beim Start scannt die App automatisch das System und zeigt eine Liste der über winget verfügbaren installierten Anwendungen."
        },
        ["HelpScanCacheHeader"] = new()
        {
            ["en"] = "Result caching",
            ["ru"] = "Кэширование результатов",
            ["de"] = "Ergebnis-Caching"
        },
        ["HelpScanCacheDesc"] = new()
        {
            ["en"] = "Scan results are cached for 24 hours. On the next launch the app loads data from cache instantly (under 1 second) instead of rescanning (20–30 seconds).",
            ["ru"] = "Результаты сканирования кэшируются на 24 часа. При повторном запуске приложение загружает данные из кэша мгновенно (менее 1 секунды), вместо повторного сканирования (20–30 секунд).",
            ["de"] = "Scan-Ergebnisse werden 24 Stunden lang zwischengespeichert. Beim nächsten Start lädt die App die Daten sofort aus dem Cache (unter 1 Sekunde) statt erneut zu scannen (20–30 Sekunden)."
        },
        ["HelpScanRefreshHeader"] = new()
        {
            ["en"] = "Refreshing the list",
            ["ru"] = "Обновление списка",
            ["de"] = "Liste aktualisieren"
        },
        ["HelpScanRefreshDesc"] = new()
        {
            ["en"] = "Click the 🔄 Refresh button to force a rescan and update the cache.",
            ["ru"] = "Нажмите кнопку 🔄 Обновить для принудительного пересканирования системы и обновления кэша.",
            ["de"] = "Klicken Sie auf 🔄 Aktualisieren, um einen erneuten Scan zu erzwingen und den Cache zu aktualisieren."
        },
        ["HelpScanDetailsDesc"] = new()
        {
            ["en"] = "The app shows a numbered list of found applications. Generally, package IDs match or are very similar to application names. For a faster interface, names and descriptions are loaded in the background — simply hover over any row to read the tooltip.\n\nHidden apps: By default, system components are hidden. Enable the checkbox 'Show system and hidden apps' to reveal Store apps, runtime libraries, built-in Windows components, and service/updater items excluded from export by default.\n\nUse the view filter to switch between all visible apps, offline-ready apps, online fallback apps, and excluded items. Use the search box to quickly find an app by name or ID.",
            ["ru"] = "Программа показывает пронумерованный список найденных приложений. Как правило, ID пакета совпадает с названием приложения или очень на него похож. Для ускорения работы интерфейса полные названия и описания подгружаются в фоне — наведите мышь на любую строчку, чтобы прочитать подробности.\n\nСкрытые приложения: по умолчанию технические элементы скрыты. Включите галочку 'Показывать системные и скрытые приложения', чтобы отобразить Store-приложения, runtime-библиотеки, встроенные компоненты Windows и служебные updater-элементы, исключенные из экспорта по умолчанию.\n\nИспользуйте фильтр вида, чтобы переключаться между всеми видимыми приложениями, offline-ready приложениями, online fallback приложениями и исключенными элементами. Строка поиска помогает быстро найти приложение по имени или ID.",
            ["de"] = "Die App zeigt eine nummerierte Liste gefundener Anwendungen. In der Regel entsprechen die Paket-IDs den Anwendungsnamen oder sind diesen sehr ähnlich. Für eine schnellere Benutzeroberfläche werden Namen und Beschreibungen im Hintergrund geladen — fahren Sie einfach mit der Maus über eine Zeile, um den Tooltip zu lesen.\n\nVersteckte Apps: Standardmäßig sind Systemkomponenten ausgeblendet. Aktivieren Sie die Checkbox 'System- und versteckte Apps anzeigen', um Store-Apps, Runtime-Bibliotheken, eingebaute Windows-Komponenten und standardmäßig vom Export ausgeschlossene Service-/Updater-Elemente anzuzeigen.\n\nVerwenden Sie den Ansichtsfilter, um zwischen allen sichtbaren Apps, offline bereiten Apps, Online-Fallback-Apps und ausgeschlossenen Elementen zu wechseln. Verwenden Sie die Suche, um Apps schnell nach Name oder ID zu finden."
        },
        ["HelpScanWarningHeader"] = new()
        {
            ["en"] = "Warnings ⚠️",
            ["ru"] = "Предупреждения ⚠️",
            ["de"] = "Warnungen ⚠️"
        },
        ["HelpScanWarningDesc"] = new()
        {
            ["en"] = "Some apps are marked with ⚠️ — this means they do not support offline download via winget download. During offline export, PAS skips them for local download and adds them to RestoreOnlineFallback.bat when possible.",
            ["ru"] = "Некоторые приложения помечены иконкой ⚠️. Это означает, что они не поддерживают оффлайн-загрузку через winget download. При оффлайн-экспорте PAS пропускает их для локальной загрузки и по возможности добавляет в RestoreOnlineFallback.bat.",
            ["de"] = "Einige Apps sind mit ⚠️ gekennzeichnet — das bedeutet, sie unterstützen keinen Offline-Download über winget download. Beim Offline-Export überspringt PAS sie für den lokalen Download und fügt sie nach Möglichkeit zu RestoreOnlineFallback.bat hinzu."
        },
        // ── Export tab ──
        ["HelpExportHeader"] = new()
        {
            ["en"] = "Export modes",
            ["ru"] = "Режимы экспорта",
            ["de"] = "Exportmodi"
        },
        ["HelpExportOnlineHeader"] = new()
        {
            ["en"] = "1. Online script",
            ["ru"] = "1. Онлайн-скрипт",
            ["de"] = "1. Online-Skript"
        },
        ["HelpExportOnlineDesc"] = new()
        {
            ["en"] = "Creates a batch script (.bat) that automatically installs selected apps via the internet.\n\nAdvantages:\n• Minimal size (a few KB)\n• Always installs latest versions\n• Works for all apps\n\nRequirements:\n• Active internet connection\n• Access to winget repositories",
            ["ru"] = "Создает batch-скрипт (.bat), который автоматически устанавливает выбранные приложения через интернет.\n\nПреимущества:\n• Минимальный размер (несколько КБ)\n• Всегда устанавливает последние версии\n• Работает для всех приложений\n\nТребования:\n• Активное интернет-соединение\n• Доступ к репозиториям winget",
            ["de"] = "Erstellt ein Batch-Skript (.bat), das ausgewählte Apps automatisch über das Internet installiert.\n\nVorteile:\n• Minimale Größe (wenige KB)\n• Installiert immer neueste Versionen\n• Funktioniert für alle Apps\n\nAnforderungen:\n• Aktive Internetverbindung\n• Zugriff auf winget-Repositories"
        },
        ["HelpExportOfflineHeader"] = new()
        {
            ["en"] = "2. Offline package",
            ["ru"] = "2. Оффлайн-пакет",
            ["de"] = "2. Offline-Paket"
        },
        ["HelpExportOfflineDesc"] = new()
        {
            ["en"] = "Downloads supported installer files (.exe, .msi, .msix, .appx, .zip) and creates install_all.bat. Apps that cannot be downloaded offline are added to RestoreOnlineFallback.bat.\n\nAdvantages:\n• Installs supported apps without internet\n• Keeps downloaded app versions\n• Portable folder for transfer\n\nLimitations:\n• Large size (depends on apps)\n• Some apps require online fallback (see ⚠️)\n• Requires internet while creating the package",
            ["ru"] = "Загружает поддерживаемые установочные файлы (.exe, .msi, .msix, .appx, .zip) и создает install_all.bat. Приложения, которые нельзя скачать оффлайн, добавляются в RestoreOnlineFallback.bat.\n\nПреимущества:\n• Установка поддерживаемых приложений без интернета\n• Сохранение скачанных версий приложений\n• Портативная папка для переноса\n\nОграничения:\n• Большой размер (зависит от приложений)\n• Часть приложений требует online fallback (см. ⚠️)\n• Требует интернет при создании пакета",
            ["de"] = "Lädt unterstützte Installationsdateien (.exe, .msi, .msix, .appx, .zip) herunter und erstellt install_all.bat. Apps, die nicht offline heruntergeladen werden können, werden zu RestoreOnlineFallback.bat hinzugefügt.\n\nVorteile:\n• Unterstützte Apps ohne Internet installieren\n• Heruntergeladene App-Versionen behalten\n• Portabler Ordner zum Übertragen\n\nEinschränkungen:\n• Große Größe (abhängig von den Apps)\n• Einige Apps benötigen den Online-Fallback (siehe ⚠️)\n• Internet beim Erstellen des Pakets erforderlich"
        },
        ["HelpExportProcessHeader"] = new()
        {
            ["en"] = "Export process",
            ["ru"] = "Процесс экспорта",
            ["de"] = "Exportvorgang"
        },
        ["HelpExportProcessDesc"] = new()
        {
            ["en"] = "1. Select the desired apps (checkboxes)\n2. Use the view filter if you need offline-ready, fallback, or excluded items\n3. Choose the export mode\n4. Click Export\n5. Choose the save location\n6. Review the export summary and open the export folder if needed",
            ["ru"] = "1. Выберите нужные приложения (галочки)\n2. При необходимости используйте фильтр вида: offline-ready, fallback или исключенные элементы\n3. Выберите режим экспорта\n4. Нажмите Экспортировать\n5. Выберите место сохранения\n6. Проверьте сводку экспорта и при необходимости откройте папку результата",
            ["de"] = "1. Gewünschte Apps auswählen (Checkboxen)\n2. Bei Bedarf den Ansichtsfilter für offline bereite, Fallback- oder ausgeschlossene Elemente nutzen\n3. Exportmodus wählen\n4. Export starten\n5. Speicherort wählen\n6. Exportzusammenfassung prüfen und bei Bedarf den Exportordner öffnen"
        },
        // ── Installation tab ──
        ["HelpInstallHeader"] = new()
        {
            ["en"] = "Restoring applications",
            ["ru"] = "Восстановление приложений",
            ["de"] = "Apps wiederherstellen"
        },
        ["HelpInstallRunHeader"] = new()
        {
            ["en"] = "Running the script",
            ["ru"] = "Запуск скрипта",
            ["de"] = "Skript ausführen"
        },
        ["HelpInstallRunDesc"] = new()
        {
            ["en"] = "Online script: run the generated .bat or .ps1 file as administrator.\n\nOffline package: run install_all.bat first. If RestoreOnlineFallback.bat exists, run it afterwards on a machine with internet access.",
            ["ru"] = "Онлайн-скрипт: запустите созданный .bat или .ps1 файл от имени администратора.\n\nОффлайн-пакет: сначала запустите install_all.bat. Если рядом есть RestoreOnlineFallback.bat, затем запустите его на машине с доступом в интернет.",
            ["de"] = "Online-Skript: Führen Sie die erzeugte .bat- oder .ps1-Datei als Administrator aus.\n\nOffline-Paket: Führen Sie zuerst install_all.bat aus. Falls RestoreOnlineFallback.bat vorhanden ist, führen Sie es danach auf einem Gerät mit Internetzugang aus."
        },
        ["HelpInstallProcessHeader"] = new()
        {
            ["en"] = "Installation process",
            ["ru"] = "Процесс установки",
            ["de"] = "Installationsvorgang"
        },
        ["HelpInstallProcessDesc"] = new()
        {
            ["en"] = "The scripts automatically:\n• Check required tools where needed\n• Install apps one by one\n• Use local files in install_all.bat\n• Use winget install in online and fallback scripts\n• Show progress and results",
            ["ru"] = "Скрипты автоматически:\n• Проверяют нужные инструменты там, где это требуется\n• Устанавливают приложения по очереди\n• Используют локальные файлы в install_all.bat\n• Используют winget install в online и fallback-скриптах\n• Показывают прогресс и результаты",
            ["de"] = "Die Skripte:\n• Prüfen benötigte Werkzeuge, falls erforderlich\n• Installieren Apps nacheinander\n• Verwenden lokale Dateien in install_all.bat\n• Verwenden winget install in Online- und Fallback-Skripten\n• Zeigen Fortschritt und Ergebnisse"
        },
        ["HelpInstallProblemsHeader"] = new()
        {
            ["en"] = "Possible issues",
            ["ru"] = "Возможные проблемы",
            ["de"] = "Mögliche Probleme"
        },
        ["HelpInstallProblemsDesc"] = new()
        {
            ["en"] = "App won't install:\n• Check internet connection (for online mode)\n• Make sure winget is installed and updated\n• Try manually: winget install --id [ID]\n\nPermission error:\n• Run the script as administrator\n• Check antivirus (may block installation)",
            ["ru"] = "Приложение не устанавливается:\n• Проверьте интернет-соединение (для онлайн-режима)\n• Убедитесь, что winget установлен и обновлен\n• Попробуйте установить вручную: winget install --id [ID]\n\nОшибка прав доступа:\n• Запустите скрипт от имени администратора\n• Проверьте антивирус (может блокировать установку)",
            ["de"] = "App lässt sich nicht installieren:\n• Internetverbindung prüfen (Online-Modus)\n• Sicherstellen, dass winget installiert und aktuell ist\n• Manuell versuchen: winget install --id [ID]\n\nZugriffsrechtsfehler:\n• Skript als Administrator ausführen\n• Antivirus prüfen (kann die Installation blockieren)"
        },
        // ── Tips tab ──
        ["HelpTipsHeader"] = new()
        {
            ["en"] = "Useful tips",
            ["ru"] = "Полезные советы",
            ["de"] = "Nützliche Tipps"
        },
        ["HelpTipsPerfHeader"] = new()
        {
            ["en"] = "Performance optimisation",
            ["ru"] = "Оптимизация производительности",
            ["de"] = "Leistungsoptimierung"
        },
        ["HelpTipsPerfDesc"] = new()
        {
            ["en"] = "• Cache auto-updates every 24 hours\n• Use 🔄 Refresh to force an update\n• Cache is stored in %LocalAppData%\\PAS\\",
            ["ru"] = "• Кэш обновляется автоматически раз в 24 часа\n• Для принудительного обновления используйте кнопку 🔄 Обновить\n• Кэш хранится в %LocalAppData%\\PAS\\",
            ["de"] = "• Cache wird alle 24 Stunden automatisch aktualisiert\n• Verwenden Sie 🔄 Aktualisieren für eine manuelle Aktualisierung\n• Cache-Speicherort: %LocalAppData%\\PAS\\"
        },
        ["HelpTipsModeHeader"] = new()
        {
            ["en"] = "Choosing the export mode",
            ["ru"] = "Выбор режима экспорта",
            ["de"] = "Exportmodus wählen"
        },
        ["HelpTipsModeDesc"] = new()
        {
            ["en"] = "Use online script if:\n• The target machine has internet\n• You want the latest app versions\n• You want one compact restore file\n\nUse offline package if:\n• You need local installers for supported apps\n• You deploy to multiple computers\n• You accept that some apps may be moved to RestoreOnlineFallback.bat",
            ["ru"] = "Используйте онлайн-скрипт если:\n• На целевой машине есть интернет\n• Нужны последние версии приложений\n• Нужен один компактный файл восстановления\n\nИспользуйте оффлайн-пакет если:\n• Нужны локальные установщики для поддерживаемых приложений\n• Устанавливаете на несколько компьютеров\n• Готовы, что часть приложений попадет в RestoreOnlineFallback.bat",
            ["de"] = "Online-Skript verwenden wenn:\n• Das Zielgerät Internet hat\n• Neueste App-Versionen gewünscht sind\n• Eine kompakte Wiederherstellungsdatei gewünscht ist\n\nOffline-Paket verwenden wenn:\n• Lokale Installer für unterstützte Apps benötigt werden\n• Auf mehreren Computern installiert wird\n• Einige Apps nach RestoreOnlineFallback.bat verschoben werden dürfen"
        },
        ["HelpTipsManyAppsHeader"] = new()
        {
            ["en"] = "Working with many apps",
            ["ru"] = "Работа с большим количеством приложений",
            ["de"] = "Viele Apps verwalten"
        },
        ["HelpTipsManyAppsDesc"] = new()
        {
            ["en"] = "• Use search to find apps quickly\n• Use ✓ Select All / ✗ Deselect All for bulk selection\n• The counter shows how many apps are selected",
            ["ru"] = "• Используйте поиск для быстрого нахождения\n• Кнопки ✓ Выбрать все / ✗ Снять все для массового выбора\n• Счетчик показывает количество выбранных приложений",
            ["de"] = "• Suchfunktion für schnelles Auffinden nutzen\n• ✓ Alle auswählen / ✗ Alle abwählen für Massenauswahl\n• Zähler zeigt Anzahl ausgewählter Apps"
        },
        ["HelpTipsBackupHeader"] = new()
        {
            ["en"] = "Backup strategy",
            ["ru"] = "Резервное копирование",
            ["de"] = "Backup-Strategie"
        },
        ["HelpTipsBackupDesc"] = new()
        {
            ["en"] = "Recommended backup schedule:\n• Before reinstalling Windows\n• After installing new software\n• Monthly to stay current",
            ["ru"] = "Рекомендуется регулярно создавать резервные копии:\n• Перед переустановкой Windows\n• После установки нового ПО\n• Раз в месяц для актуальности",
            ["de"] = "Empfohlener Backup-Zeitplan:\n• Vor der Windows-Neuinstallation\n• Nach der Installation neuer Software\n• Monatlich für Aktualität"
        }
    };

    // ─── String accessor ─────────────────────────────────────────────────────
    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (T.TryGetValue(key, out var langs))
        {
            if (langs.TryGetValue(_currentLanguage, out var val) && !string.IsNullOrEmpty(val))
                return val;
            if (langs.TryGetValue("en", out var fallback))
                return fallback;
        }
        return $"[{key}]";
    }

    public string Format(string key, params object[] args)
    {
        try { return string.Format(Get(key), args); }
        catch { return Get(key); }
    }

    // ─── Convenience properties for XAML binding ─────────────────────────────
    public string WindowTitle        => Get("WindowTitle");
    public string HelpButton         => Get("HelpButton");
    public string SearchPlaceholder  => Get("SearchPlaceholder");
    public string RefreshButton      => Get("RefreshButton");
    public string SelectAll          => Get("SelectAll");
    public string UnselectAll        => Get("UnselectAll");
    public string ShowSystemApps     => Get("ShowSystemApps");
    public string AppFilterLabel     => Get("AppFilterLabel");
    public string AppFilterAll       => Get("AppFilterAll");
    public string AppFilterOfflineReady => Get("AppFilterOfflineReady");
    public string AppFilterOnlineFallback => Get("AppFilterOnlineFallback");
    public string AppFilterExcluded  => Get("AppFilterExcluded");
    public string SelectedCountFormat => Get("SelectedCountFormat");
    public string ColName            => Get("ColName");
    public string ColPackageId       => Get("ColPackageId");
    public string ColVersion         => Get("ColVersion");
    public string ColStatus          => Get("ColStatus");
    public string OfflineWarningTooltip => Get("OfflineWarningTooltip");
    public string OpenExportFolder   => Get("OpenExportFolder");
    public string LastExportSummaryHeader => Get("LastExportSummaryHeader");
    public string ExportGroupHeader  => Get("ExportGroupHeader");
    public string ExportOnlineOption => Get("ExportOnlineOption");
    public string ExportOfflineOption => Get("ExportOfflineOption");
    public string CancelButton       => Get("CancelButton");
    public string LanguageLabel      => Get("LanguageLabel");
    // HelpWindow — structural
    public string HelpWindowTitle    => Get("HelpWindowTitle");
    public string HelpMainTitle      => Get("HelpMainTitle");
    public string HelpClose          => Get("HelpClose");
    public string HelpTabOverview    => Get("HelpTabOverview");
    public string HelpTabScan        => Get("HelpTabScan");
    public string HelpTabExport      => Get("HelpTabExport");
    public string HelpTabInstall     => Get("HelpTabInstall");
    public string HelpTabTips        => Get("HelpTabTips");
    // HelpWindow — Overview tab
    public string HelpOverviewHeader          => Get("HelpOverviewHeader");
    public string HelpOverviewDesc            => Get("HelpOverviewDesc");
    public string HelpOverviewFeaturesHeader  => Get("HelpOverviewFeaturesHeader");
    public string HelpOverviewFeatures        => Get("HelpOverviewFeatures");
    public string HelpOverviewReqHeader       => Get("HelpOverviewReqHeader");
    public string HelpOverviewReq             => Get("HelpOverviewReq");
    // HelpWindow — Scan tab
    public string HelpScanHeader        => Get("HelpScanHeader");
    public string HelpScanAutoHeader    => Get("HelpScanAutoHeader");
    public string HelpScanAutoDesc      => Get("HelpScanAutoDesc");
    public string HelpScanCacheHeader   => Get("HelpScanCacheHeader");
    public string HelpScanCacheDesc     => Get("HelpScanCacheDesc");
    public string HelpScanRefreshHeader => Get("HelpScanRefreshHeader");
    public string HelpScanRefreshDesc   => Get("HelpScanRefreshDesc");
    public string HelpScanDetailsDesc   => Get("HelpScanDetailsDesc");
    public string HelpScanWarningHeader => Get("HelpScanWarningHeader");
    public string HelpScanWarningDesc   => Get("HelpScanWarningDesc");
    // HelpWindow — Export tab
    public string HelpExportHeader        => Get("HelpExportHeader");
    public string HelpExportOnlineHeader  => Get("HelpExportOnlineHeader");
    public string HelpExportOnlineDesc    => Get("HelpExportOnlineDesc");
    public string HelpExportOfflineHeader => Get("HelpExportOfflineHeader");
    public string HelpExportOfflineDesc   => Get("HelpExportOfflineDesc");
    public string HelpExportProcessHeader => Get("HelpExportProcessHeader");
    public string HelpExportProcessDesc   => Get("HelpExportProcessDesc");
    // HelpWindow — Install tab
    public string HelpInstallHeader         => Get("HelpInstallHeader");
    public string HelpInstallRunHeader      => Get("HelpInstallRunHeader");
    public string HelpInstallRunDesc        => Get("HelpInstallRunDesc");
    public string HelpInstallProcessHeader  => Get("HelpInstallProcessHeader");
    public string HelpInstallProcessDesc    => Get("HelpInstallProcessDesc");
    public string HelpInstallProblemsHeader => Get("HelpInstallProblemsHeader");
    public string HelpInstallProblemsDesc   => Get("HelpInstallProblemsDesc");
    // HelpWindow — Tips tab
    public string HelpTipsHeader        => Get("HelpTipsHeader");
    public string HelpTipsPerfHeader    => Get("HelpTipsPerfHeader");
    public string HelpTipsPerfDesc      => Get("HelpTipsPerfDesc");
    public string HelpTipsModeHeader    => Get("HelpTipsModeHeader");
    public string HelpTipsModeDesc      => Get("HelpTipsModeDesc");
    public string HelpTipsManyAppsHeader => Get("HelpTipsManyAppsHeader");
    public string HelpTipsManyAppsDesc  => Get("HelpTipsManyAppsDesc");
    public string HelpTipsBackupHeader  => Get("HelpTipsBackupHeader");
    public string HelpTipsBackupDesc    => Get("HelpTipsBackupDesc");

    // AboutWindow
    public string AboutButton => Get("AboutButton");
    public string AboutWindowTitle => Get("AboutWindowTitle");
    public string AboutAppTitle => Get("AboutAppTitle");
    public string AboutAppDescription => Get("AboutAppDescription");
    public string AboutVersion => Get("AboutVersion");
    public string AboutDeveloper => Get("AboutDeveloper");
    public string AboutGithub => Get("AboutGithub");
    public string AboutLicense => Get("AboutLicense");

    // ─── INotifyPropertyChanged ───────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

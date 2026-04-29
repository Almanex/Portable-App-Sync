using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PAS.Models;

namespace PAS.Services;

/// <summary>
/// Сервис генерации скриптов автоустановки
/// </summary>
public class ScriptGeneratorService
{
    private readonly LoggingService _logger;
    private readonly WingetService _wingetService;

    public ScriptGeneratorService(LoggingService logger, WingetService wingetService)
    {
        _logger = logger;
        _wingetService = wingetService;
    }

    /// <summary>
    /// Генерация .bat скрипта для автоустановки
    /// </summary>
    public async Task<ExportResult> GenerateBatchScript(List<InstalledApp> apps, string outputFilePath)
    {
        var result = new ExportResult();
        
        try
        {
            var l = LocalizationManager.Instance;
            _logger.LogInfo($"Генерация .bat скрипта для {apps.Count} приложений");

            var scriptContent = new StringBuilder();
            
            // Заголовок скрипта
            scriptContent.AppendLine("@echo off");
            scriptContent.AppendLine("chcp 65001 >nul");
            scriptContent.AppendLine("echo ========================================");
            scriptContent.AppendLine("echo AppBackup ^& Restore - Автоустановка");
            scriptContent.AppendLine("echo ========================================");
            scriptContent.AppendLine("echo.");
            scriptContent.AppendLine();
            
            // Проверка наличия Winget
            scriptContent.AppendLine("echo Проверка наличия Winget...");
            scriptContent.AppendLine("winget --version >nul 2>&1");
            scriptContent.AppendLine("if %errorlevel% neq 0 (");
            scriptContent.AppendLine("    echo ОШИБКА: Winget не найден!");
            scriptContent.AppendLine("    echo Установите App Installer из Microsoft Store:");
            scriptContent.AppendLine("    echo https://www.microsoft.com/p/app-installer/9nblggh4nns1");
            scriptContent.AppendLine("    pause");
            scriptContent.AppendLine("    exit /b 1");
            scriptContent.AppendLine(")");
            scriptContent.AppendLine("echo Winget найден. Начинаем установку...");
            scriptContent.AppendLine("echo.");
            scriptContent.AppendLine();
            
            // Команды установки для каждого приложения
            foreach (var app in apps.Where(a => a.IsSelected && a.IsAvailable))
            {
                // Escape special characters for batch file echo
                string safeName = app.Name;
                string installId = app.WingetId;
                string cleanId = installId;
                string installSource = app.Source;
                bool useNameFallback = false;
                
                if (app.IsStoreApp || OfflineCompatibilityService.IsMicrosoftStoreProductId(app.WingetId))
                {
                    installId = await _wingetService.ResolveStoreAppId(app.WingetId);
                    installSource = "msstore";
                    
                    // Try to get "clean" ID (without hash) for winget source
                    // Example: Microsoft.MicrosoftSolitaireCollection_8wekyb3d8bbwe -> Microsoft.MicrosoftSolitaireCollection
                    int underscoreIndex = installId.LastIndexOf('_');
                    if (underscoreIndex > 0)
                    {
                        cleanId = installId.Substring(0, underscoreIndex);
                    }
                    else
                    {
                        cleanId = installId;
                    }
                }
                else if (!_wingetService.CanUseWingetCommands(app.WingetId))
                {
                    var resolved = await _wingetService.ResolveRepositoryPackageByName(app.Name);
                    if (resolved.HasValue)
                    {
                        safeName = resolved.Value.Name;
                        installId = resolved.Value.Id;
                        cleanId = installId;
                        installSource = resolved.Value.Source;
                    }
                    else
                    {
                        useNameFallback = true;
                        safeName = app.Name;
                    }
                }

                string installCommandWinget = useNameFallback
                    ? $"winget install --name {EscapeForCmdArgument(safeName)} --source winget --silent --accept-package-agreements --accept-source-agreements"
                    : $"winget install --id {EscapeForCmdArgument(cleanId)} --source {installSource} --silent --accept-package-agreements --accept-source-agreements";

                string installCommandStoreId = useNameFallback
                    ? $"winget install --name {EscapeForCmdArgument(safeName)} --source msstore --silent --accept-package-agreements --accept-source-agreements"
                    : $"winget install --id {EscapeForCmdArgument(installId)} --source msstore --silent --accept-package-agreements --accept-source-agreements";

                string installCommandStoreName = $"winget install --name {EscapeForCmdArgument(safeName)} --source msstore --silent --accept-package-agreements --accept-source-agreements";

                // Use goto to avoid issues with parentheses in if blocks
                string labelDone = $"done_{Guid.NewGuid().ToString("N")}";
                string labelStep2 = $"step2_{Guid.NewGuid().ToString("N")}";
                string labelStep3 = $"step3_{Guid.NewGuid().ToString("N")}";

                scriptContent.AppendLine($"echo {EscapeForBatchEcho(useNameFallback ? $"Попытка 1 (Winget Source Name: {safeName})" : $"Попытка 1 (Winget Source ID: {cleanId})")}");
                scriptContent.AppendLine(installCommandWinget);
                scriptContent.AppendLine($"if %errorlevel% equ 0 goto {labelDone}");
                
                scriptContent.AppendLine($":{labelStep2}");
                scriptContent.AppendLine($"echo {EscapeForBatchEcho(useNameFallback ? $"Попытка 2 (MS Store Source Name: {safeName})" : $"Попытка 2 (MS Store Source ID: {installId})")}");
                scriptContent.AppendLine(installCommandStoreId);
                scriptContent.AppendLine($"if %errorlevel% equ 0 goto {labelDone}");

                scriptContent.AppendLine($":{labelStep3}");
                scriptContent.AppendLine($"echo {EscapeForBatchEcho($"Попытка 3 (MS Store Source Name: {safeName})")}");
                scriptContent.AppendLine(installCommandStoreName);
                
                scriptContent.AppendLine($":{labelDone}");
                scriptContent.AppendLine("echo.");
                result.SuccessCount++;
            }
            
            // Завершение
            scriptContent.AppendLine("echo ========================================");
            scriptContent.AppendLine("echo Установка завершена!");
            scriptContent.AppendLine($"echo Установлено приложений: {result.SuccessCount}");
            scriptContent.AppendLine("echo ========================================");
            scriptContent.AppendLine("pause");
            
            // Сохранение файла (без BOM, иначе cmd.exe может не понять первую строку)
            var outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await File.WriteAllTextAsync(outputFilePath, scriptContent.ToString(), new UTF8Encoding(false));
            
            result.Success = true;
            result.OutputPath = outputFilePath;
            result.Message = l.Format("ScriptCreated", outputFilePath);
            
            _logger.LogInfo(result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = LocalizationManager.Instance.Format("ScriptCreateError", ex.Message);
            _logger.LogError(result.Message, ex);
        }
        
        return result;
    }

    /// <summary>
    /// Генерация PowerShell скрипта для автоустановки
    /// </summary>
    public async Task<ExportResult> GeneratePowerShellScript(List<InstalledApp> apps, string outputFilePath)
    {
        var result = new ExportResult();
        
        try
        {
            var l = LocalizationManager.Instance;
            _logger.LogInfo($"Генерация .ps1 скрипта для {apps.Count} приложений");

            var scriptContent = new StringBuilder();
            
            // Заголовок скрипта
            scriptContent.AppendLine("# PAS (Portable App Sync) - Автоустановка");
            scriptContent.AppendLine("# Сгенерировано: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            scriptContent.AppendLine();
            scriptContent.AppendLine("Write-Host \"========================================\" -ForegroundColor Cyan");
            scriptContent.AppendLine("Write-Host \"PAS (Portable App Sync) - Автоустановка\" -ForegroundColor Cyan");
            scriptContent.AppendLine("Write-Host \"========================================\" -ForegroundColor Cyan");
            scriptContent.AppendLine("Write-Host \"\"");
            scriptContent.AppendLine();
            
            // Проверка наличия Winget
            scriptContent.AppendLine("# Проверка наличия Winget");
            scriptContent.AppendLine("Write-Host \"Проверка наличия Winget...\" -ForegroundColor Yellow");
            scriptContent.AppendLine("try {");
            scriptContent.AppendLine("    $wingetVersion = winget --version");
            scriptContent.AppendLine("    Write-Host \"Winget найден: $wingetVersion\" -ForegroundColor Green");
            scriptContent.AppendLine("} catch {");
            scriptContent.AppendLine("    Write-Host \"ОШИБКА: Winget не найден!\" -ForegroundColor Red");
            scriptContent.AppendLine("    Write-Host \"Установите App Installer из Microsoft Store\" -ForegroundColor Red");
            scriptContent.AppendLine("    Read-Host \"Нажмите Enter для выхода\"");
            scriptContent.AppendLine("    exit 1");
            scriptContent.AppendLine("}");
            scriptContent.AppendLine("Write-Host \"\"");
            scriptContent.AppendLine();
            
            // Команды установки
            scriptContent.AppendLine("# Установка приложений");
            scriptContent.AppendLine($"$apps = @(");
            
            foreach (var app in apps.Where(a => a.IsSelected && a.IsAvailable))
            {
                string installId = app.WingetId;
                string source = "winget";
                bool useNameFallback = false;
                
                if (app.IsStoreApp || OfflineCompatibilityService.IsMicrosoftStoreProductId(app.WingetId))
                {
                    installId = await _wingetService.ResolveStoreAppId(app.WingetId);
                    source = "msstore";
                }
                else if (!_wingetService.CanUseWingetCommands(app.WingetId))
                {
                    var resolved = await _wingetService.ResolveRepositoryPackageByName(app.Name);
                    if (resolved.HasValue)
                    {
                        installId = resolved.Value.Id;
                        source = resolved.Value.Source;
                    }
                    else
                    {
                        installId = app.Name;
                        source = "winget";
                        useNameFallback = true;
                    }
                }

                scriptContent.AppendLine($"    @{{ Name = '{EscapeForPowerShellSingleQuotedString(app.Name)}'; Id = '{EscapeForPowerShellSingleQuotedString(installId)}'; Source = '{EscapeForPowerShellSingleQuotedString(source)}'; UseName = ${useNameFallback.ToString().ToLowerInvariant()} }},");
                result.SuccessCount++;
            }
            
            scriptContent.AppendLine(")");
            scriptContent.AppendLine();
            scriptContent.AppendLine("foreach ($app in $apps) {");
            scriptContent.AppendLine("    Write-Host \"Установка: $($app.Name)\" -ForegroundColor Yellow");
            scriptContent.AppendLine("    if ($app.UseName) {");
            scriptContent.AppendLine("        winget install --name $($app.Name) --source winget --silent --accept-package-agreements --accept-source-agreements");
            scriptContent.AppendLine("        if ($LASTEXITCODE -ne 0) {");
            scriptContent.AppendLine("            winget install --name $($app.Name) --source msstore --silent --accept-package-agreements --accept-source-agreements");
            scriptContent.AppendLine("        }");
            scriptContent.AppendLine("    } else {");
            scriptContent.AppendLine("        winget install --id $($app.Id) -e --source $($app.Source) --silent --accept-package-agreements --accept-source-agreements");
            scriptContent.AppendLine("    }");
            scriptContent.AppendLine("    Write-Host \"\"");
            scriptContent.AppendLine("}");
            scriptContent.AppendLine();
            
            // Завершение
            scriptContent.AppendLine("Write-Host \"========================================\" -ForegroundColor Cyan");
            scriptContent.AppendLine("Write-Host \"Установка завершена!\" -ForegroundColor Green");
            scriptContent.AppendLine($"Write-Host \"Установлено приложений: {result.SuccessCount}\" -ForegroundColor Green");
            scriptContent.AppendLine("Write-Host \"========================================\" -ForegroundColor Cyan");
            scriptContent.AppendLine("Read-Host \"Нажмите Enter для выхода\"");
            
            // Сохранение файла
            var outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await File.WriteAllTextAsync(outputFilePath, scriptContent.ToString(), new UTF8Encoding(false));
            
            result.Success = true;
            result.OutputPath = outputFilePath;
            result.Message = l.Format("PowerShellScriptCreated", outputFilePath);
            
            _logger.LogInfo(result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = LocalizationManager.Instance.Format("ScriptCreateError", ex.Message);
            _logger.LogError(result.Message, ex);
        }
        
        return result;
    }

    private static string EscapeForBatchEcho(string value)
    {
        return value
            .Replace("^", "^^")
            .Replace("&", "^&")
            .Replace("<", "^<")
            .Replace(">", "^>")
            .Replace("|", "^|");
    }

    private static string EscapeForCmdArgument(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string EscapeForPowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''");
    }

}

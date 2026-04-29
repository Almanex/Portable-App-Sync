using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PAS.Models;

/// <summary>
/// Модель для десериализации winget export JSON
/// </summary>
public class WingetExportRoot
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    
    [JsonPropertyName("CreationDate")]
    public string? CreationDate { get; set; }
    
    [JsonPropertyName("Sources")]
    public List<WingetSource> Sources { get; set; } = new();
    
    [JsonPropertyName("WinGetVersion")]
    public string? WinGetVersion { get; set; }
}

public class WingetSource
{
    [JsonPropertyName("Packages")]
    public List<WingetPackage> Packages { get; set; } = new();
    
    [JsonPropertyName("SourceDetails")]
    public WingetSourceDetails? SourceDetails { get; set; }
}

public class WingetPackage
{
    [JsonPropertyName("PackageIdentifier")]
    public string PackageIdentifier { get; set; } = string.Empty;
    
    [JsonPropertyName("Version")]
    public string Version { get; set; } = string.Empty;
}

public class WingetSourceDetails
{
    [JsonPropertyName("Argument")]
    public string? Argument { get; set; }
    
    [JsonPropertyName("Identifier")]
    public string? Identifier { get; set; }
    
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("Type")]
    public string? Type { get; set; }
}

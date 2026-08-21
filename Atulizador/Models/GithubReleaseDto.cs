using System.Text.Json.Serialization;

namespace Atulizador.Models;

/// <summary>DTOs mínimos para desserializar a resposta da API de Releases do GitHub.</summary>
public sealed class GithubReleaseDto
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("assets")]
    public List<GithubAssetDto> Assets { get; set; } = new();
}

public sealed class GithubAssetDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}

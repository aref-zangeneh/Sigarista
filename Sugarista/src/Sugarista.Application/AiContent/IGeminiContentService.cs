using System.Text.Json;

namespace Sugarista.Application.AiContent;

/// <summary>
/// Strictly read-only, stateless draft content generator powered by Google Gemini.
/// Has ZERO write access to the database and ZERO tool/action execution privileges:
/// it only generates structured Persian draft text that admins review and save manually.
/// </summary>
public interface IGeminiContentService
{
    Task<GeminiSettingsStatusDto> GetSettingsStatusAsync(CancellationToken cancellationToken = default);

    Task<GeminiSettingsStatusDto> UpdateSettingsAsync(
        string? apiKey,
        bool clearApiKey,
        string? model,
        CancellationToken cancellationToken = default);

    Task<GeminiTestResultDto> TestConnectionAsync(CancellationToken cancellationToken = default);

    Task<GeminiDraftResultDto> GenerateDraftAsync(
        GeminiDraftRequest request,
        CancellationToken cancellationToken = default);
}

public record GeminiSettingsStatusDto(
    bool IsConfigured,
    string Source,
    string MaskedKey,
    string Model,
    IReadOnlyList<string> AvailableModels);

public record GeminiTestResultDto(
    bool Success,
    string Message,
    string Model);

public record GeminiDraftRequest(
    string Section,
    string Title,
    string? Context = null,
    string? Tone = null,
    string? ExtraNotes = null);

public record GeminiDraftResultDto(
    string Section,
    string Model,
    JsonElement Draft);

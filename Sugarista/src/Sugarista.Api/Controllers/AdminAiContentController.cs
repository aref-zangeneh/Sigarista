using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.AiContent;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Admin endpoints for Gemini draft content generation.
/// Strictly draft-only: generating content NEVER mutates the database.
/// </summary>
[Authorize]
[Route("api/admin/ai-content")]
public class AdminAiContentController : ApiControllerBase
{
    private readonly IGeminiContentService _geminiService;

    public AdminAiContentController(IGeminiContentService geminiService)
    {
        _geminiService = geminiService;
    }

    [HttpGet("settings")]
    [RequirePermission(AdminPermissions.SiteSettings.View)]
    public async Task<ActionResult<GeminiSettingsStatusDto>> GetSettings(CancellationToken cancellationToken)
    {
        return Ok(await _geminiService.GetSettingsStatusAsync(cancellationToken));
    }

    [HttpPut("settings")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<GeminiSettingsStatusDto>> UpdateSettings(
        [FromBody] UpdateGeminiSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _geminiService.UpdateSettingsAsync(
                request.ApiKey,
                request.ClearApiKey,
                request.Model,
                cancellationToken);
            return Ok(updated);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("test")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<GeminiTestResultDto>> TestConnection(CancellationToken cancellationToken)
    {
        return Ok(await _geminiService.TestConnectionAsync(cancellationToken));
    }

    [HttpPost("generate")]
    public async Task<ActionResult<GeminiDraftResultDto>> GenerateDraft(
        [FromBody] GeminiDraftRequest request,
        CancellationToken cancellationToken)
    {
        var section = (request.Section ?? string.Empty).Trim().ToLowerInvariant();
        if (!HasSectionPermission(section))
        {
            return Forbid();
        }

        try
        {
            var result = await _geminiService.GenerateDraftAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool HasSectionPermission(string section)
    {
        if (User.HasClaim(AdminPermissions.SuperAdminClaimType, "true"))
        {
            return true;
        }

        var requiredAny = section switch
        {
            "blog" => new[] { AdminPermissions.Blog.Create, AdminPermissions.Blog.Update },
            "menu-item" => new[] { AdminPermissions.MenuItems.Create, AdminPermissions.MenuItems.Update },
            "category" => new[] { AdminPermissions.Categories.Create, AdminPermissions.Categories.Update },
            "product-wiki" => new[] { AdminPermissions.ProductWiki.Update },
            "recipe" => new[] { AdminPermissions.Recipes.Create, AdminPermissions.Recipes.Update },
            _ => Array.Empty<string>(),
        };

        if (requiredAny.Length == 0)
        {
            return false;
        }

        return requiredAny.Any(p => User.HasClaim(AdminPermissions.ClaimType, p));
    }
}

public record UpdateGeminiSettingsRequest(
    string? ApiKey = null,
    bool ClearApiKey = false,
    string? Model = null);

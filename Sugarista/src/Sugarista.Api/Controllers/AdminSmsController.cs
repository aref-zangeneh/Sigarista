using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Sms;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Admin surface for the «مدیریت پیامک» module: dashboard, single/bulk send,
/// contacts & groups, templates, history, providers and global settings.
/// Provider credentials are encrypted at rest and NEVER returned here —
/// responses only carry "has credential" flags and masked previews.
/// </summary>
[Authorize]
[Route("api/admin/sms")]
public class AdminSmsController : ApiControllerBase
{
    // ── Dashboard ──
    [HttpGet("dashboard")]
    [RequirePermission(AdminPermissions.Sms.ViewDashboard)]
    public async Task<ActionResult<SmsDashboardDto>> GetDashboard(
        [FromQuery] string range = "7d",
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] Guid? providerId = null,
        [FromQuery] string? kind = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetSmsDashboardQuery(range, from, to, providerId, kind), cancellationToken));
    }

    // ── Single send ──
    [HttpPost("send")]
    [RequirePermission(AdminPermissions.Sms.SendSingle)]
    public async Task<ActionResult<SmsSendOutcomeDto>> Send(
        [FromBody] SendSmsCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    // ── Bulk ──
    [HttpPost("send-bulk/preview")]
    [RequirePermission(AdminPermissions.Sms.SendBulk)]
    public async Task<ActionResult<SmsBulkPreview>> PreviewBulk(
        [FromBody] PreviewSmsBulkCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpPost("send-bulk")]
    [RequirePermission(AdminPermissions.Sms.SendBulk)]
    public async Task<ActionResult<SmsCampaignDto>> SendBulk(
        [FromBody] CreateSmsCampaignCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpGet("campaigns")]
    [RequirePermission(AdminPermissions.Sms.ViewHistory)]
    public async Task<ActionResult<SmsPagedResult<SmsCampaignDto>>> GetCampaigns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetSmsCampaignsQuery(page, pageSize), cancellationToken));
    }

    [HttpGet("campaigns/{id:guid}")]
    [RequirePermission(AdminPermissions.Sms.ViewHistory)]
    public async Task<ActionResult<SmsCampaignDetailDto>> GetCampaign(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetSmsCampaignQuery(id), cancellationToken));
    }

    [HttpPost("campaigns/{id:guid}/cancel")]
    [RequirePermission(AdminPermissions.Sms.CancelCampaign)]
    public async Task<IActionResult> CancelCampaign(
        Guid id,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new CancelSmsCampaignCommand(id), cancellationToken);
        return Ok(new { message = "درخواست لغو ثبت شد؛ ارسال‌های در صف متوقف می‌شوند." });
    }

    // ── History ──
    [HttpGet("messages")]
    [RequirePermission(AdminPermissions.Sms.ViewHistory)]
    public async Task<ActionResult<SmsPagedResult<SmsMessageDto>>> GetMessages(
        [FromQuery] string? mobile = null,
        [FromQuery] Guid? providerId = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? campaignId = null,
        [FromQuery] string? kind = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetSmsMessagesQuery(mobile, providerId, status, campaignId, kind, from, to, page, pageSize),
            cancellationToken));
    }

    [HttpGet("messages/{id:guid}")]
    [RequirePermission(AdminPermissions.Sms.ViewHistory)]
    public async Task<ActionResult<SmsMessageDto>> GetMessage(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetSmsMessageQuery(id), cancellationToken));
    }

    [HttpPost("messages/{id:guid}/refresh-status")]
    [RequirePermission(AdminPermissions.Sms.ViewHistory)]
    public async Task<ActionResult<SmsMessageDto>> RefreshMessageStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new RefreshSmsMessageStatusCommand(id), cancellationToken));
    }

    [HttpGet("messages/export")]
    [RequirePermission(AdminPermissions.Sms.ExportReports)]
    public async Task<IActionResult> ExportMessages(
        [FromQuery] string? mobile = null,
        [FromQuery] Guid? providerId = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? campaignId = null,
        [FromQuery] string? kind = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        // Export reads the same filters through the paged query (large page).
        var result = await Mediator.Send(
            new GetSmsMessagesQuery(mobile, providerId, status, campaignId, kind, from, to, 1, 200),
            cancellationToken);

        var lines = new List<string>
        {
            "id,mobile,status,kind,sendType,provider,cost,requestedAt,sentAt,error"
        };
        lines.AddRange(result.Items.Select(message =>
            $"{message.Id},{message.Mobile},{message.Status},{message.Kind},{message.SendType}," +
            $"{message.ProviderName},{message.Cost},{message.RequestedAt:O},{message.SentAt:O}," +
            $"\"{(message.ErrorMessage ?? string.Empty).Replace('"', '\'')}\""));

        return File(
            System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines)),
            "text/csv",
            $"sms-messages-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    // ── Contacts ──
    [HttpGet("contacts")]
    [RequirePermission(AdminPermissions.Sms.ManageContacts)]
    public async Task<ActionResult<SmsPagedResult<SmsContactDto>>> GetContacts(
        [FromQuery] string? search = null,
        [FromQuery] Guid? groupId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(
            new GetSmsContactsQuery(search, groupId, isActive, page, pageSize), cancellationToken));
    }

    [HttpPost("contacts")]
    [RequirePermission(AdminPermissions.Sms.ManageContacts)]
    public async Task<ActionResult<Guid>> CreateContact(
        [FromBody] UpsertSmsContactCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = Guid.Empty }, cancellationToken));
    }

    [HttpPut("contacts/{id:guid}")]
    [RequirePermission(AdminPermissions.Sms.ManageContacts)]
    public async Task<ActionResult<Guid>> UpdateContact(
        Guid id,
        [FromBody] UpsertSmsContactCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("contacts/{id:guid}")]
    [RequirePermission(AdminPermissions.Sms.ManageContacts)]
    public async Task<IActionResult> DeleteContact(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteSmsContactCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("contacts/import")]
    [RequirePermission(AdminPermissions.Sms.ManageContacts)]
    public async Task<ActionResult<SmsContactImportReport>> ImportContacts(
        [FromBody] ImportSmsContactsCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpGet("contacts/export")]
    [RequirePermission(AdminPermissions.Sms.ExportReports)]
    public async Task<IActionResult> ExportContacts(CancellationToken cancellationToken)
    {
        var csv = await Mediator.Send(new ExportSmsContactsQuery(), cancellationToken);
        return File(
            System.Text.Encoding.UTF8.GetBytes(csv),
            "text/csv",
            $"sms-contacts-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    [HttpPost("contacts/add-to-groups")]
    [RequirePermission(AdminPermissions.Sms.ManageContacts)]
    public async Task<ActionResult<int>> AddContactsToGroups(
        [FromBody] AddContactsToGroupsCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpPost("contacts/remove-from-groups")]
    [RequirePermission(AdminPermissions.Sms.ManageContacts)]
    public async Task<ActionResult<int>> RemoveContactsFromGroups(
        [FromBody] RemoveContactsFromGroupsCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    // ── Opt-outs ──
    [HttpGet("opt-outs")]
    [RequirePermission(AdminPermissions.Sms.ManageContacts)]
    public async Task<ActionResult<SmsPagedResult<SmsOptOutDto>>> GetOptOuts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetSmsOptOutsQuery(page, pageSize), cancellationToken));
    }

    [HttpPost("opt-outs")]
    [RequirePermission(AdminPermissions.Sms.ManageContacts)]
    public async Task<ActionResult<Guid>> AddOptOut(
        [FromBody] AddSmsOptOutCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpDelete("opt-outs/{id:guid}")]
    [RequirePermission(AdminPermissions.Sms.ManageContacts)]
    public async Task<IActionResult> RemoveOptOut(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new RemoveSmsOptOutCommand(id), cancellationToken);
        return NoContent();
    }

    // ── Contact groups ──
    [HttpGet("groups")]
    [RequirePermission(AdminPermissions.Sms.ManageGroups)]
    public async Task<ActionResult<IReadOnlyList<SmsContactGroupDto>>> GetGroups(
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetSmsContactGroupsQuery(), cancellationToken));
    }

    [HttpPost("groups")]
    [RequirePermission(AdminPermissions.Sms.ManageGroups)]
    public async Task<ActionResult<Guid>> CreateGroup(
        [FromBody] UpsertSmsContactGroupCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = Guid.Empty }, cancellationToken));
    }

    [HttpPut("groups/{id:guid}")]
    [RequirePermission(AdminPermissions.Sms.ManageGroups)]
    public async Task<ActionResult<Guid>> UpdateGroup(
        Guid id,
        [FromBody] UpsertSmsContactGroupCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("groups/{id:guid}")]
    [RequirePermission(AdminPermissions.Sms.ManageGroups)]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteSmsContactGroupCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("groups/{id:guid}/members")]
    [RequirePermission(AdminPermissions.Sms.ManageGroups)]
    public async Task<ActionResult<SmsPagedResult<SmsGroupMemberDto>>> GetGroupMembers(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Mediator.Send(new GetSmsContactGroupMembersQuery(id, page, pageSize), cancellationToken));
    }

    // ── Templates ──
    [HttpGet("templates")]
    [RequirePermission(AdminPermissions.Sms.ManageTemplates)]
    public async Task<ActionResult<IReadOnlyList<SmsTemplateDto>>> GetTemplates(
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetSmsTemplatesQuery(), cancellationToken));
    }

    [HttpPost("templates")]
    [RequirePermission(AdminPermissions.Sms.ManageTemplates)]
    public async Task<ActionResult<Guid>> CreateTemplate(
        [FromBody] UpsertSmsTemplateCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = Guid.Empty }, cancellationToken));
    }

    [HttpPut("templates/{id:guid}")]
    [RequirePermission(AdminPermissions.Sms.ManageTemplates)]
    public async Task<ActionResult<Guid>> UpdateTemplate(
        Guid id,
        [FromBody] UpsertSmsTemplateCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("templates/{id:guid}")]
    [RequirePermission(AdminPermissions.Sms.ManageTemplates)]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteSmsTemplateCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("templates/{id:guid}/duplicate")]
    [RequirePermission(AdminPermissions.Sms.ManageTemplates)]
    public async Task<ActionResult<Guid>> DuplicateTemplate(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new DuplicateSmsTemplateCommand(id), cancellationToken));
    }

    [HttpPost("templates/preview")]
    [RequirePermission(AdminPermissions.Sms.SendSingle)]
    public async Task<ActionResult<SmsTemplatePreviewDto>> PreviewTemplate(
        [FromBody] PreviewSmsTemplateQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(query, cancellationToken));
    }

    // ── Providers ──
    [HttpGet("providers")]
    [RequirePermission(AdminPermissions.Sms.ManageProviders)]
    public async Task<ActionResult<IReadOnlyList<SmsProviderDto>>> GetProviders(
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetSmsProvidersQuery(), cancellationToken));
    }

    [HttpPost("providers")]
    [RequirePermission(AdminPermissions.Sms.ManageProviders)]
    public async Task<ActionResult<Guid>> CreateProvider(
        [FromBody] UpsertSmsProviderCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = Guid.Empty }, cancellationToken));
    }

    [HttpPut("providers/{id:guid}")]
    [RequirePermission(AdminPermissions.Sms.ManageProviders)]
    public async Task<ActionResult<Guid>> UpdateProvider(
        Guid id,
        [FromBody] UpsertSmsProviderCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpDelete("providers/{id:guid}")]
    [RequirePermission(AdminPermissions.Sms.ManageProviders)]
    public async Task<IActionResult> DeleteProvider(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteSmsProviderCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("providers/{id:guid}/test")]
    [RequirePermission(AdminPermissions.Sms.ManageProviders)]
    public async Task<ActionResult<SmsConnectionTestResult>> TestProvider(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new TestSmsProviderConnectionCommand(id), cancellationToken));
    }

    [HttpPost("providers/{id:guid}/check-credit")]
    [RequirePermission(AdminPermissions.Sms.ManageProviders)]
    public async Task<ActionResult<SmsCreditResult>> CheckProviderCredit(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new CheckSmsProviderCreditCommand(id), cancellationToken));
    }

    // ── Settings ──
    [HttpGet("settings")]
    [RequirePermission(AdminPermissions.Sms.ManageSettings)]
    public async Task<ActionResult<SmsGlobalSettings>> GetSettings(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetSmsSettingsQuery(), cancellationToken));
    }

    [HttpPut("settings")]
    [RequirePermission(AdminPermissions.Sms.ManageSettings)]
    public async Task<ActionResult<SmsGlobalSettings>> UpdateSettings(
        [FromBody] UpdateSmsSettingsCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpPost("history/prune")]
    [RequirePermission(AdminPermissions.Sms.ManageSettings)]
    public async Task<ActionResult<int>> PruneHistory(CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new PruneSmsHistoryCommand(), cancellationToken));
    }
}

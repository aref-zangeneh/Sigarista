using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Api.Authorization;
using Sugarista.Application.Accounting.Commands.DraftProfitDistribution;
using Sugarista.Application.Accounting.Commands.FinalizeProfitDistribution;
using Sugarista.Application.Accounting.Commands.RecordManualExpense;
using Sugarista.Application.Accounting.Commands.RecordPartnerCapital;
using Sugarista.Application.Accounting.Commands.UpsertPartner;
using Sugarista.Application.Accounting.Queries.GetAccountingOverview;
using Sugarista.Application.Accounting.Queries.GetBalanceSheet;
using Sugarista.Application.Accounting.Queries.GetIncomeStatement;
using Sugarista.Application.Accounting.Queries.GetTrialBalance;
using Sugarista.Application.Accounting.Queries.ListJournalEntries;
using Sugarista.Application.Accounting.Queries.ListPartners;
using Sugarista.Domain.Constants;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/accounting")]
public class AdminAccountingController : ApiControllerBase
{
    [HttpGet("overview")]
    [RequirePermission(AdminPermissions.Accounting.View)]
    public async Task<ActionResult<AccountingOverviewDto>> GetOverview(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetAccountingOverviewQuery(fromDate, toDate), cancellationToken));
    }

    [HttpGet("partners")]
    [RequirePermission(AdminPermissions.Accounting.View)]
    public async Task<ActionResult<IReadOnlyCollection<PartnerDto>>> ListPartners(
        [FromQuery] bool activeOnly,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new ListPartnersQuery(activeOnly), cancellationToken));
    }

    [HttpPost("partners")]
    [RequirePermission(AdminPermissions.Accounting.Create)]
    public async Task<ActionResult<Guid>> CreatePartner(
        [FromBody] UpsertPartnerCommand command,
        CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with { Id = null }, cancellationToken);
        return Ok(id);
    }

    [HttpPut("partners/{id:guid}")]
    [RequirePermission(AdminPermissions.Accounting.Update)]
    public async Task<ActionResult<Guid>> UpdatePartner(
        Guid id,
        [FromBody] UpsertPartnerCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
    }

    [HttpPost("partner-capital")]
    [RequirePermission(AdminPermissions.Accounting.Create)]
    public async Task<ActionResult<Guid>> RecordPartnerCapital(
        [FromBody] RecordPartnerCapitalCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpPost("expenses")]
    [RequirePermission(AdminPermissions.Accounting.Create)]
    public async Task<ActionResult<Guid>> RecordExpense(
        [FromBody] RecordManualExpenseCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpGet("journal")]
    [RequirePermission(AdminPermissions.Accounting.View)]
    public async Task<ActionResult<IReadOnlyCollection<JournalEntryDto>>> ListJournal(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new ListJournalEntriesQuery(fromDate, toDate, limit), cancellationToken));
    }

    [HttpGet("reports/trial-balance")]
    [RequirePermission(AdminPermissions.Accounting.View)]
    public async Task<ActionResult<TrialBalanceDto>> GetTrialBalance(
        [FromQuery] DateOnly? asOfDate,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetTrialBalanceQuery(asOfDate), cancellationToken));
    }

    [HttpGet("reports/income-statement")]
    [RequirePermission(AdminPermissions.Accounting.View)]
    public async Task<ActionResult<IncomeStatementDto>> GetIncomeStatement(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetIncomeStatementQuery(fromDate, toDate), cancellationToken));
    }

    [HttpGet("reports/balance-sheet")]
    [RequirePermission(AdminPermissions.Accounting.View)]
    public async Task<ActionResult<BalanceSheetDto>> GetBalanceSheet(
        [FromQuery] DateOnly? asOfDate,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetBalanceSheetQuery(asOfDate), cancellationToken));
    }

    [HttpPost("profit-distribution/draft")]
    [RequirePermission(AdminPermissions.Accounting.View)]
    public async Task<ActionResult<ProfitDistributionDraftDto>> DraftProfitDistribution(
        [FromBody] DraftProfitDistributionCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }

    [HttpPost("profit-distribution/finalize")]
    [RequirePermission(AdminPermissions.Accounting.Update)]
    public async Task<ActionResult<Guid>> FinalizeProfitDistribution(
        [FromBody] FinalizeProfitDistributionCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(command, cancellationToken));
    }
}

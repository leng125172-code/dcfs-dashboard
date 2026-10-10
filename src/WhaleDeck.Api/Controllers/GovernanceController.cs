using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Api.Security;
using WhaleDeck.Application.Models;
using WhaleDeck.Application.Services;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Authorize(Policy = "Administrator")]
[Route("api/v1")]
public sealed class GovernanceController(GovernanceService governance) : ControllerBase
{
    [HttpGet("schedules")]
    public async Task<IActionResult> Schedules(CancellationToken cancellationToken) => Ok(await governance.ListSchedulesAsync(cancellationToken));

    [HttpPost("schedules")]
    public async Task<IActionResult> CreateSchedule([FromBody] SaveScheduledTaskCommand command, CancellationToken cancellationToken) =>
        Ok(await governance.SaveScheduleAsync(User.RequireSubject(), command with { Id = null }, cancellationToken));

    [HttpPut("schedules/{id:guid}")]
    public async Task<IActionResult> UpdateSchedule(Guid id, [FromBody] SaveScheduledTaskCommand command, CancellationToken cancellationToken) =>
        Ok(await governance.SaveScheduleAsync(User.RequireSubject(), command with { Id = id }, cancellationToken));

    [HttpDelete("schedules/{id:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid id, [FromQuery] long version, CancellationToken cancellationToken)
    {
        await governance.DeleteScheduleAsync(id, version, cancellationToken);
        return NoContent();
    }

    [HttpGet("backups/policies")]
    public async Task<IActionResult> BackupPolicies(CancellationToken cancellationToken) => Ok(await governance.ListBackupPoliciesAsync(cancellationToken));

    [HttpPost("backups/policies")]
    public async Task<IActionResult> SaveBackupPolicy([FromBody] SaveBackupPolicyCommand command, CancellationToken cancellationToken) =>
        Ok(await governance.SaveBackupPolicyAsync(command, cancellationToken));

    [HttpGet("alerts/rules")]
    public async Task<IActionResult> AlertRules(CancellationToken cancellationToken) => Ok(await governance.ListAlertRulesAsync(cancellationToken));

    [HttpPost("alerts/rules")]
    public async Task<IActionResult> SaveAlertRule([FromBody] SaveAlertRuleCommand command, CancellationToken cancellationToken) =>
        Ok(await governance.SaveAlertRuleAsync(command, cancellationToken));

    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts([FromQuery] bool includeRecovered = false, CancellationToken cancellationToken = default) =>
        Ok(await governance.ListAlertsAsync(includeRecovered, cancellationToken));

    [HttpPost("alerts/{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken cancellationToken) =>
        Ok(await governance.UpdateAlertAsync(id, User.RequireSubject(), null, cancellationToken));

    [HttpPost("alerts/{id:guid}/silence")]
    public async Task<IActionResult> Silence(Guid id, [FromBody] SilenceAlertRequest request, CancellationToken cancellationToken) =>
        Ok(await governance.UpdateAlertAsync(id, User.RequireSubject(), request.UntilUtc, cancellationToken));

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken) => Ok(await governance.ListSettingsAsync(cancellationToken));

    [HttpPut("settings/{key}")]
    public async Task<IActionResult> SaveSetting(string key, [FromBody] SavePlatformSettingCommand command, CancellationToken cancellationToken) =>
        Ok(await governance.SaveSettingAsync(key, User.RequireSubject(), command, cancellationToken));

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] int take = 100, CancellationToken cancellationToken = default) =>
        Ok(await governance.ListAuditAsync(take, cancellationToken));

    public sealed record SilenceAlertRequest(DateTimeOffset UntilUtc);
}

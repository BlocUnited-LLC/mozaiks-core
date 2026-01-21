using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;
using Notification.API.Email.Models;
using Notification.API.Email.Services;

namespace Notification.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TransactionalEmailController : ControllerBase
{
    private readonly ITransactionalEmailService _emailService;
    private readonly IEmailTemplateService _templateService;

    public TransactionalEmailController(
        ITransactionalEmailService emailService,
        IEmailTemplateService templateService)
    {
        _emailService = emailService;
        _templateService = templateService;
    }

    /// <summary>
    /// Send a transactional email using a template
    /// </summary>
    [HttpPost("send")]
    [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
    public async Task<ActionResult<EmailSendResult>> SendEmail([FromBody] EmailSendRequest request)
    {
        if (string.IsNullOrEmpty(request.AppId) || string.IsNullOrEmpty(request.TemplateKey))
        {
            return BadRequest("AppId and TemplateKey are required");
        }

        if (string.IsNullOrEmpty(request.Recipient.Email))
        {
            return BadRequest("Recipient email is required");
        }

        var result = await _emailService.SendEmailAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Send welcome email
    /// </summary>
    [HttpPost("send/welcome")]
    [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
    public async Task<ActionResult<EmailSendResult>> SendWelcomeEmail([FromBody] WelcomeEmailRequest request)
    {
        var result = await _emailService.SendWelcomeEmailAsync(
            request.AppId, 
            request.Email, 
            request.UserName, 
            request.ActionUrl);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Send email verification
    /// </summary>
    [HttpPost("send/verification")]
    [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
    public async Task<ActionResult<EmailSendResult>> SendVerificationEmail([FromBody] VerificationEmailRequest request)
    {
        var result = await _emailService.SendVerificationEmailAsync(
            request.AppId, 
            request.Email, 
            request.UserName, 
            request.VerificationUrl);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Send password reset email
    /// </summary>
    [HttpPost("send/password-reset")]
    [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
    public async Task<ActionResult<EmailSendResult>> SendPasswordResetEmail([FromBody] PasswordResetEmailRequest request)
    {
        var result = await _emailService.SendPasswordResetEmailAsync(
            request.AppId, 
            request.Email, 
            request.UserName, 
            request.ResetUrl);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Send team invite email
    /// </summary>
    [HttpPost("send/team-invite")]
    [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
    public async Task<ActionResult<EmailSendResult>> SendTeamInviteEmail([FromBody] TeamInviteEmailRequest request)
    {
        var result = await _emailService.SendTeamInviteEmailAsync(
            request.AppId,
            request.Email,
            request.UserName,
            request.InviterName,
            request.Role,
            request.InviteUrl);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // Template Management

    /// <summary>
    /// Get all email templates for an app
    /// </summary>
    [HttpGet("templates/{appId}")]
    [Authorize]
    public async Task<ActionResult<List<EmailTemplate>>> GetTemplates(string appId)
    {
        var templates = await _templateService.GetAllTemplatesAsync(appId);
        return Ok(templates);
    }

    /// <summary>
    /// Get a specific template
    /// </summary>
    [HttpGet("templates/{appId}/{templateKey}")]
    [Authorize]
    public async Task<ActionResult<EmailTemplate>> GetTemplate(string appId, string templateKey)
    {
        var template = await _templateService.GetTemplateAsync(appId, templateKey);
        return template != null ? Ok(template) : NotFound();
    }

    /// <summary>
    /// Create a custom email template
    /// </summary>
    [HttpPost("templates")]
    [Authorize]
    public async Task<ActionResult<EmailTemplate>> CreateTemplate([FromBody] EmailTemplate template)
    {
        var created = await _templateService.CreateTemplateAsync(template);
        return CreatedAtAction(nameof(GetTemplate), new { appId = created.AppId, templateKey = created.TemplateKey }, created);
    }

    /// <summary>
    /// Update an email template
    /// </summary>
    [HttpPut("templates/{id}")]
    [Authorize]
    public async Task<ActionResult<EmailTemplate>> UpdateTemplate(string id, [FromBody] EmailTemplate template)
    {
        template.Id = id;
        var updated = await _templateService.UpdateTemplateAsync(template);
        return Ok(updated);
    }

    /// <summary>
    /// Seed default templates for an app
    /// </summary>
    [HttpPost("templates/{appId}/seed")]
    [Authorize]
    public async Task<IActionResult> SeedDefaultTemplates(string appId)
    {
        await _templateService.SeedDefaultTemplatesAsync(appId);
        return Ok(new { message = "Default templates seeded successfully" });
    }
}

// Request DTOs
public record WelcomeEmailRequest(string AppId, string Email, string UserName, string ActionUrl);
public record VerificationEmailRequest(string AppId, string Email, string UserName, string VerificationUrl);
public record PasswordResetEmailRequest(string AppId, string Email, string UserName, string ResetUrl);
public record TeamInviteEmailRequest(string AppId, string Email, string UserName, string InviterName, string Role, string InviteUrl);

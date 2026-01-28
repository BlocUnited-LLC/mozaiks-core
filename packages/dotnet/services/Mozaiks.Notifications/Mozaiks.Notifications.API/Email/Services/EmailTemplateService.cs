using MongoDB.Driver;
using Notification.API.Email.Models;

namespace Notification.API.Email.Services;

public interface IEmailTemplateService
{
    Task<EmailTemplate?> GetTemplateAsync(string appId, string templateKey);
    Task<EmailTemplate> CreateTemplateAsync(EmailTemplate template);
    Task<EmailTemplate> UpdateTemplateAsync(EmailTemplate template);
    Task<List<EmailTemplate>> GetAllTemplatesAsync(string appId);
    Task SeedDefaultTemplatesAsync(string appId);
}

public class EmailTemplateService : IEmailTemplateService
{
    private readonly IMongoCollection<EmailTemplate> _templates;
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(IMongoDatabase database, ILogger<EmailTemplateService> logger)
    {
        _templates = database.GetCollection<EmailTemplate>("EmailTemplates");
        _logger = logger;
    }

    public async Task<EmailTemplate?> GetTemplateAsync(string appId, string templateKey)
    {
        return await _templates.Find(t => t.AppId == appId && t.TemplateKey == templateKey && t.IsActive)
            .FirstOrDefaultAsync();
    }

    public async Task<EmailTemplate> CreateTemplateAsync(EmailTemplate template)
    {
        template.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        template.CreatedAt = DateTime.UtcNow;
        await _templates.InsertOneAsync(template);
        return template;
    }

    public async Task<EmailTemplate> UpdateTemplateAsync(EmailTemplate template)
    {
        template.UpdatedAt = DateTime.UtcNow;
        await _templates.ReplaceOneAsync(t => t.Id == template.Id, template);
        return template;
    }

    public async Task<List<EmailTemplate>> GetAllTemplatesAsync(string appId)
    {
        return await _templates.Find(t => t.AppId == appId).ToListAsync();
    }

    public async Task SeedDefaultTemplatesAsync(string appId)
    {
        var existingTemplates = await GetAllTemplatesAsync(appId);
        if (existingTemplates.Any()) return;

        var defaultTemplates = GetDefaultTemplates(appId);
        foreach (var template in defaultTemplates)
        {
            await CreateTemplateAsync(template);
        }
        _logger.LogInformation("Seeded {Count} default email templates for app {AppId}", defaultTemplates.Count, appId);
    }

    private static List<EmailTemplate> GetDefaultTemplates(string appId) => new()
    {
        new EmailTemplate
        {
            AppId = appId,
            Name = "Welcome Email",
            TemplateKey = "welcome",
            Subject = "Welcome to {{app_name}}!",
            HtmlBody = DefaultTemplates.WelcomeHtml,
            TextBody = DefaultTemplates.WelcomeText
        },
        new EmailTemplate
        {
            AppId = appId,
            Name = "Email Verification",
            TemplateKey = "email_verification",
            Subject = "Verify your email for {{app_name}}",
            HtmlBody = DefaultTemplates.EmailVerificationHtml,
            TextBody = DefaultTemplates.EmailVerificationText
        },
        new EmailTemplate
        {
            AppId = appId,
            Name = "Password Reset",
            TemplateKey = "password_reset",
            Subject = "Reset your password for {{app_name}}",
            HtmlBody = DefaultTemplates.PasswordResetHtml,
            TextBody = DefaultTemplates.PasswordResetText
        },
        new EmailTemplate
        {
            AppId = appId,
            Name = "Team Invite",
            TemplateKey = "team_invite",
            Subject = "You've been invited to join {{app_name}}",
            HtmlBody = DefaultTemplates.TeamInviteHtml,
            TextBody = DefaultTemplates.TeamInviteText
        },
        new EmailTemplate
        {
            AppId = appId,
            Name = "Subscription Created",
            TemplateKey = "subscription_created",
            Subject = "Welcome to {{plan_name}} - {{app_name}}",
            HtmlBody = DefaultTemplates.SubscriptionCreatedHtml,
            TextBody = DefaultTemplates.SubscriptionCreatedText
        },
        new EmailTemplate
        {
            AppId = appId,
            Name = "Payment Receipt",
            TemplateKey = "payment_receipt",
            Subject = "Payment Receipt from {{app_name}}",
            HtmlBody = DefaultTemplates.PaymentReceiptHtml,
            TextBody = DefaultTemplates.PaymentReceiptText
        }
    };
}

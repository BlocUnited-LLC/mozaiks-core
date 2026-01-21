using System.Text.RegularExpressions;
using MongoDB.Driver;
using Notification.API.Email.Models;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Notification.API.Email.Services;

public interface ITransactionalEmailService
{
    Task<EmailSendResult> SendEmailAsync(EmailSendRequest request);
    Task<EmailSendResult> SendWelcomeEmailAsync(string appId, string email, string userName, string actionUrl);
    Task<EmailSendResult> SendVerificationEmailAsync(string appId, string email, string userName, string verificationUrl);
    Task<EmailSendResult> SendPasswordResetEmailAsync(string appId, string email, string userName, string resetUrl);
    Task<EmailSendResult> SendTeamInviteEmailAsync(string appId, string email, string userName, string inviterName, string role, string inviteUrl);
    Task<EmailSendResult> SendSubscriptionCreatedEmailAsync(string appId, string email, string userName, string planName, string amount, string interval, string nextBillingDate, string dashboardUrl);
    Task<EmailSendResult> SendPaymentReceiptEmailAsync(string appId, string email, string userName, string invoiceNumber, string amount, string paymentDate, string paymentMethod, string receiptUrl);
}

public class TransactionalEmailService : ITransactionalEmailService
{
    private readonly IEmailTemplateService _templateService;
    private readonly IMongoCollection<EmailLog> _emailLogs;
    private readonly ILogger<TransactionalEmailService> _logger;
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public TransactionalEmailService(
        IEmailTemplateService templateService,
        IMongoDatabase database,
        ILogger<TransactionalEmailService> logger,
        IConfiguration configuration)
    {
        _templateService = templateService;
        _emailLogs = database.GetCollection<EmailLog>("EmailLogs");
        _logger = logger;
        
        _apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY") 
            ?? configuration["SendGrid:ApiKey"]
            ?? throw new InvalidOperationException("SENDGRID_API_KEY not configured");
        
        _fromEmail = Environment.GetEnvironmentVariable("SENDGRID_FROM_EMAIL") 
            ?? configuration["SendGrid:FromEmail"]
            ?? throw new InvalidOperationException("SENDGRID_FROM_EMAIL not configured");
        
        _fromName = Environment.GetEnvironmentVariable("SENDGRID_FROM_NAME") 
            ?? configuration["SendGrid:FromName"]
            ?? "Mozaiks";
    }

    public async Task<EmailSendResult> SendEmailAsync(EmailSendRequest request)
    {
        var template = await _templateService.GetTemplateAsync(request.AppId, request.TemplateKey);
        if (template == null)
        {
            // Try to seed defaults first
            await _templateService.SeedDefaultTemplatesAsync(request.AppId);
            template = await _templateService.GetTemplateAsync(request.AppId, request.TemplateKey);
            
            if (template == null)
            {
                return new EmailSendResult
                {
                    Success = false,
                    Error = $"Email template '{request.TemplateKey}' not found for app {request.AppId}"
                };
            }
        }

        // Merge variables
        var variables = new Dictionary<string, string>(request.GlobalVariables);
        foreach (var kv in request.Recipient.Variables)
        {
            variables[kv.Key] = kv.Value;
        }
        
        // Add default variables
        variables.TryAdd("year", DateTime.UtcNow.Year.ToString());
        variables.TryAdd("user_name", request.Recipient.Name);
        variables.TryAdd("user_email", request.Recipient.Email);

        // Process template
        var subject = request.CustomSubject ?? ReplaceVariables(template.Subject, variables);
        var htmlBody = ReplaceVariables(template.HtmlBody, variables);
        var textBody = ReplaceVariables(template.TextBody, variables);

        // Create email log
        var emailLog = new EmailLog
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            AppId = request.AppId,
            RecipientEmail = request.Recipient.Email,
            RecipientName = request.Recipient.Name,
            TemplateKey = request.TemplateKey,
            Subject = subject,
            Status = EmailStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(request.Recipient.Email, request.Recipient.Name);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, textBody, htmlBody);

            if (!string.IsNullOrEmpty(request.ReplyTo))
            {
                msg.ReplyTo = new EmailAddress(request.ReplyTo);
            }

            var response = await client.SendEmailAsync(msg);
            
            if (response.IsSuccessStatusCode)
            {
                emailLog.Status = EmailStatus.Sent;
                emailLog.SendGridMessageId = response.Headers.TryGetValues("X-Message-Id", out var msgIds) 
                    ? msgIds.FirstOrDefault() 
                    : null;
                
                await _emailLogs.InsertOneAsync(emailLog);
                
                _logger.LogInformation("Email sent successfully to {Email} using template {Template}", 
                    request.Recipient.Email, request.TemplateKey);

                return new EmailSendResult
                {
                    Success = true,
                    MessageId = emailLog.SendGridMessageId,
                    SentAt = DateTime.UtcNow
                };
            }
            else
            {
                var responseBody = await response.Body.ReadAsStringAsync();
                emailLog.Status = EmailStatus.Failed;
                emailLog.ErrorMessage = $"SendGrid returned {response.StatusCode}: {responseBody}";
                
                await _emailLogs.InsertOneAsync(emailLog);
                
                _logger.LogError("Failed to send email to {Email}: {Error}", 
                    request.Recipient.Email, emailLog.ErrorMessage);

                return new EmailSendResult
                {
                    Success = false,
                    Error = emailLog.ErrorMessage
                };
            }
        }
        catch (Exception ex)
        {
            emailLog.Status = EmailStatus.Failed;
            emailLog.ErrorMessage = ex.Message;
            
            await _emailLogs.InsertOneAsync(emailLog);
            
            _logger.LogError(ex, "Exception sending email to {Email}", request.Recipient.Email);

            return new EmailSendResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static string ReplaceVariables(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template)) return template;
        
        // Replace {{variable_name}} with values
        return Regex.Replace(template, @"\{\{(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    // Convenience methods for common transactional emails

    public Task<EmailSendResult> SendWelcomeEmailAsync(string appId, string email, string userName, string actionUrl)
    {
        return SendEmailAsync(new EmailSendRequest
        {
            AppId = appId,
            TemplateKey = "welcome",
            Recipient = new EmailRecipient { Email = email, Name = userName },
            GlobalVariables = new Dictionary<string, string>
            {
                ["action_url"] = actionUrl,
                ["app_name"] = appId // TODO: Lookup actual app name
            }
        });
    }

    public Task<EmailSendResult> SendVerificationEmailAsync(string appId, string email, string userName, string verificationUrl)
    {
        return SendEmailAsync(new EmailSendRequest
        {
            AppId = appId,
            TemplateKey = "email_verification",
            Recipient = new EmailRecipient { Email = email, Name = userName },
            GlobalVariables = new Dictionary<string, string>
            {
                ["verification_url"] = verificationUrl,
                ["app_name"] = appId
            }
        });
    }

    public Task<EmailSendResult> SendPasswordResetEmailAsync(string appId, string email, string userName, string resetUrl)
    {
        return SendEmailAsync(new EmailSendRequest
        {
            AppId = appId,
            TemplateKey = "password_reset",
            Recipient = new EmailRecipient { Email = email, Name = userName },
            GlobalVariables = new Dictionary<string, string>
            {
                ["reset_url"] = resetUrl,
                ["app_name"] = appId
            }
        });
    }

    public Task<EmailSendResult> SendTeamInviteEmailAsync(string appId, string email, string userName, string inviterName, string role, string inviteUrl)
    {
        return SendEmailAsync(new EmailSendRequest
        {
            AppId = appId,
            TemplateKey = "team_invite",
            Recipient = new EmailRecipient { Email = email, Name = userName },
            GlobalVariables = new Dictionary<string, string>
            {
                ["inviter_name"] = inviterName,
                ["role"] = role,
                ["invite_url"] = inviteUrl,
                ["app_name"] = appId
            }
        });
    }

    public Task<EmailSendResult> SendSubscriptionCreatedEmailAsync(string appId, string email, string userName, string planName, string amount, string interval, string nextBillingDate, string dashboardUrl)
    {
        return SendEmailAsync(new EmailSendRequest
        {
            AppId = appId,
            TemplateKey = "subscription_created",
            Recipient = new EmailRecipient { Email = email, Name = userName },
            GlobalVariables = new Dictionary<string, string>
            {
                ["plan_name"] = planName,
                ["amount"] = amount,
                ["interval"] = interval,
                ["next_billing_date"] = nextBillingDate,
                ["dashboard_url"] = dashboardUrl,
                ["manage_subscription_url"] = $"{dashboardUrl}/settings/subscription",
                ["app_name"] = appId
            }
        });
    }

    public Task<EmailSendResult> SendPaymentReceiptEmailAsync(string appId, string email, string userName, string invoiceNumber, string amount, string paymentDate, string paymentMethod, string receiptUrl)
    {
        return SendEmailAsync(new EmailSendRequest
        {
            AppId = appId,
            TemplateKey = "payment_receipt",
            Recipient = new EmailRecipient { Email = email, Name = userName },
            GlobalVariables = new Dictionary<string, string>
            {
                ["invoice_number"] = invoiceNumber,
                ["amount"] = amount,
                ["payment_date"] = paymentDate,
                ["payment_method"] = paymentMethod,
                ["receipt_url"] = receiptUrl,
                ["app_name"] = appId
            }
        });
    }
}

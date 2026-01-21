namespace Notification.API.Email.Services;

public static class DefaultTemplates
{
    // Base styles used across all templates
    private const string BaseStyles = @"
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background-color: #f5f5f5; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .card { background: #ffffff; border-radius: 8px; padding: 40px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .header { text-align: center; margin-bottom: 30px; }
        .logo { font-size: 24px; font-weight: bold; color: #6366f1; }
        .content { margin-bottom: 30px; }
        .button { display: inline-block; background: #6366f1; color: #ffffff !important; text-decoration: none; padding: 12px 30px; border-radius: 6px; font-weight: 600; margin: 20px 0; }
        .button:hover { background: #4f46e5; }
        .footer { text-align: center; color: #666; font-size: 12px; margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee; }
        h1 { color: #111; margin: 0 0 20px 0; }
        p { margin: 0 0 15px 0; }
    ";

    public static string WelcomeHtml => $@"
<!DOCTYPE html>
<html>
<head><style>{BaseStyles}</style></head>
<body>
    <div class=""container"">
        <div class=""card"">
            <div class=""header"">
                <div class=""logo"">{{{{app_name}}}}</div>
            </div>
            <div class=""content"">
                <h1>Welcome, {{{{user_name}}}}! 🎉</h1>
                <p>Thanks for joining <strong>{{{{app_name}}}}</strong>. We're excited to have you on board!</p>
                <p>Here's what you can do next:</p>
                <ul>
                    <li>Complete your profile</li>
                    <li>Explore the features</li>
                    <li>Connect with the community</li>
                </ul>
                <a href=""{{{{action_url}}}}"" class=""button"">Get Started</a>
            </div>
            <div class=""footer"">
                <p>© {{{{year}}}} {{{{app_name}}}}. All rights reserved.</p>
                <p>If you didn't create this account, please ignore this email.</p>
            </div>
        </div>
    </div>
</body>
</html>";

    public static string WelcomeText => @"
Welcome to {{app_name}}, {{user_name}}!

Thanks for joining us. We're excited to have you on board!

Get started: {{action_url}}

© {{year}} {{app_name}}
";

    public static string EmailVerificationHtml => $@"
<!DOCTYPE html>
<html>
<head><style>{BaseStyles}</style></head>
<body>
    <div class=""container"">
        <div class=""card"">
            <div class=""header"">
                <div class=""logo"">{{{{app_name}}}}</div>
            </div>
            <div class=""content"">
                <h1>Verify your email</h1>
                <p>Hi {{{{user_name}}}},</p>
                <p>Please click the button below to verify your email address:</p>
                <a href=""{{{{verification_url}}}}"" class=""button"">Verify Email</a>
                <p style=""color: #666; font-size: 14px;"">This link will expire in 24 hours.</p>
                <p style=""color: #666; font-size: 14px;"">If the button doesn't work, copy and paste this URL into your browser:</p>
                <p style=""word-break: break-all; font-size: 12px; color: #6366f1;"">{{{{verification_url}}}}</p>
            </div>
            <div class=""footer"">
                <p>© {{{{year}}}} {{{{app_name}}}}. All rights reserved.</p>
            </div>
        </div>
    </div>
</body>
</html>";

    public static string EmailVerificationText => @"
Verify your email for {{app_name}}

Hi {{user_name}},

Please verify your email by visiting: {{verification_url}}

This link will expire in 24 hours.

© {{year}} {{app_name}}
";

    public static string PasswordResetHtml => $@"
<!DOCTYPE html>
<html>
<head><style>{BaseStyles}</style></head>
<body>
    <div class=""container"">
        <div class=""card"">
            <div class=""header"">
                <div class=""logo"">{{{{app_name}}}}</div>
            </div>
            <div class=""content"">
                <h1>Reset your password</h1>
                <p>Hi {{{{user_name}}}},</p>
                <p>We received a request to reset your password. Click the button below to create a new password:</p>
                <a href=""{{{{reset_url}}}}"" class=""button"">Reset Password</a>
                <p style=""color: #666; font-size: 14px;"">This link will expire in 1 hour.</p>
                <p style=""color: #666; font-size: 14px;"">If you didn't request this, you can safely ignore this email.</p>
            </div>
            <div class=""footer"">
                <p>© {{{{year}}}} {{{{app_name}}}}. All rights reserved.</p>
            </div>
        </div>
    </div>
</body>
</html>";

    public static string PasswordResetText => @"
Reset your password for {{app_name}}

Hi {{user_name}},

We received a request to reset your password. Visit this link to create a new password:

{{reset_url}}

This link will expire in 1 hour.

If you didn't request this, you can safely ignore this email.

© {{year}} {{app_name}}
";

    public static string TeamInviteHtml => $@"
<!DOCTYPE html>
<html>
<head><style>{BaseStyles}</style></head>
<body>
    <div class=""container"">
        <div class=""card"">
            <div class=""header"">
                <div class=""logo"">{{{{app_name}}}}</div>
            </div>
            <div class=""content"">
                <h1>You've been invited!</h1>
                <p>Hi {{{{user_name}}}},</p>
                <p><strong>{{{{inviter_name}}}}</strong> has invited you to join <strong>{{{{app_name}}}}</strong> as a {{{{role}}}}.</p>
                <a href=""{{{{invite_url}}}}"" class=""button"">Accept Invitation</a>
                <p style=""color: #666; font-size: 14px;"">This invitation will expire in 7 days.</p>
            </div>
            <div class=""footer"">
                <p>© {{{{year}}}} {{{{app_name}}}}. All rights reserved.</p>
            </div>
        </div>
    </div>
</body>
</html>";

    public static string TeamInviteText => @"
You've been invited to {{app_name}}

Hi {{user_name}},

{{inviter_name}} has invited you to join {{app_name}} as a {{role}}.

Accept the invitation: {{invite_url}}

This invitation will expire in 7 days.

© {{year}} {{app_name}}
";

    public static string SubscriptionCreatedHtml => $@"
<!DOCTYPE html>
<html>
<head><style>{BaseStyles}</style></head>
<body>
    <div class=""container"">
        <div class=""card"">
            <div class=""header"">
                <div class=""logo"">{{{{app_name}}}}</div>
            </div>
            <div class=""content"">
                <h1>Welcome to {{{{plan_name}}}}! 🚀</h1>
                <p>Hi {{{{user_name}}}},</p>
                <p>Your subscription to <strong>{{{{plan_name}}}}</strong> is now active!</p>
                <div style=""background: #f5f5f5; padding: 20px; border-radius: 6px; margin: 20px 0;"">
                    <p style=""margin: 0;""><strong>Plan:</strong> {{{{plan_name}}}}</p>
                    <p style=""margin: 10px 0 0 0;""><strong>Amount:</strong> {{{{amount}}}}/{{{{interval}}}}</p>
                    <p style=""margin: 10px 0 0 0;""><strong>Next billing:</strong> {{{{next_billing_date}}}}</p>
                </div>
                <a href=""{{{{dashboard_url}}}}"" class=""button"">Go to Dashboard</a>
            </div>
            <div class=""footer"">
                <p>© {{{{year}}}} {{{{app_name}}}}. All rights reserved.</p>
                <p><a href=""{{{{manage_subscription_url}}}}"">Manage Subscription</a></p>
            </div>
        </div>
    </div>
</body>
</html>";

    public static string SubscriptionCreatedText => @"
Welcome to {{plan_name}} - {{app_name}}

Hi {{user_name}},

Your subscription to {{plan_name}} is now active!

Plan: {{plan_name}}
Amount: {{amount}}/{{interval}}
Next billing: {{next_billing_date}}

Go to dashboard: {{dashboard_url}}

© {{year}} {{app_name}}
";

    public static string PaymentReceiptHtml => $@"
<!DOCTYPE html>
<html>
<head><style>{BaseStyles}</style></head>
<body>
    <div class=""container"">
        <div class=""card"">
            <div class=""header"">
                <div class=""logo"">{{{{app_name}}}}</div>
            </div>
            <div class=""content"">
                <h1>Payment Receipt</h1>
                <p>Hi {{{{user_name}}}},</p>
                <p>Thank you for your payment. Here are your receipt details:</p>
                <div style=""background: #f5f5f5; padding: 20px; border-radius: 6px; margin: 20px 0;"">
                    <p style=""margin: 0;""><strong>Invoice #:</strong> {{{{invoice_number}}}}</p>
                    <p style=""margin: 10px 0 0 0;""><strong>Date:</strong> {{{{payment_date}}}}</p>
                    <p style=""margin: 10px 0 0 0;""><strong>Amount:</strong> {{{{amount}}}}</p>
                    <p style=""margin: 10px 0 0 0;""><strong>Payment method:</strong> {{{{payment_method}}}}</p>
                </div>
                <a href=""{{{{receipt_url}}}}"" class=""button"">View Receipt</a>
            </div>
            <div class=""footer"">
                <p>© {{{{year}}}} {{{{app_name}}}}. All rights reserved.</p>
            </div>
        </div>
    </div>
</body>
</html>";

    public static string PaymentReceiptText => @"
Payment Receipt - {{app_name}}

Hi {{user_name}},

Thank you for your payment.

Invoice #: {{invoice_number}}
Date: {{payment_date}}
Amount: {{amount}}
Payment method: {{payment_method}}

View receipt: {{receipt_url}}

© {{year}} {{app_name}}
";
}

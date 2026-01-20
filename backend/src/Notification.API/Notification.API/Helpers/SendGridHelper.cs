using SendGrid;
using SendGrid.Helpers.Mail;

namespace Notification.API.Helpers
{
    public class SendGridHelper
    {
        public static async Task<string> SendEmailAsync(string toAddress, string name, string subject, string body)
        {
            var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY") ?? throw new InvalidOperationException("SENDGRID_API_KEY not configured");
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("no-reply@mozaiks.io", "Mozaiks");
            var to = new EmailAddress(toAddress, name);
            var plainTextContent = "";
            var htmlContent = body;
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            try
            {
                var response = await client.SendEmailAsync(msg);

                if (response.StatusCode == System.Net.HttpStatusCode.OK ||
                    response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    return "Email sent successfully";
                }
                else
                {
                    return $"Failed to send email. StatusCode: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                return $"Error occurred while sending email: {ex.Message}";
            }
        }
    }
}


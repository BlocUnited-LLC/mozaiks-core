using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.API.Services;
using Stripe;

namespace Payment.API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentWebhookController : ControllerBase
    {
        private readonly PaymentService _paymentService;
        private readonly MozaiksPayService _mozaiksPay;
        private readonly IConfiguration _configuration;

        public PaymentWebhookController(PaymentService paymentService, MozaiksPayService mozaiksPay, IConfiguration configuration)
        {
            _paymentService = paymentService;
            _mozaiksPay = mozaiksPay;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpPost("webhook")]
        [HttpPost("/api/mozaiks/pay/webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signatureHeader = Request.Headers.TryGetValue("Payments-Signature", out var providedSignature)
                ? providedSignature.ToString()
                : Request.Headers["Stripe-Signature"].ToString();
            var webhookSecret = _configuration.GetValue<string>("Payments:WebhookSecret");

            if (string.IsNullOrWhiteSpace(webhookSecret))
            {
                return StatusCode(500, new { error = "Payments webhook secret is not configured." });
            }

            Event providerEvent;
            try
            {
                providerEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);
            }
            catch
            {
                return BadRequest(new { error = "InvalidWebhook" });
            }

            await _paymentService.HandleWebhookEventAsync(providerEvent);
            await _mozaiksPay.HandleWebhookEventAsync(providerEvent);
            return Ok();
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Payment.API.Infrastructure.Observability;
using Payment.API.Models;
using Payment.API.Services;

namespace Payment.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly PaymentService _paymentService;

        public PaymentController(PaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("create-payment-intent")]
        public async Task<ActionResult> CreatePaymentIntent([FromBody] PaymentIntentRequest request)
        {
            try
            {
                var result = await _paymentService.CreatePaymentIntentAsync(request);
                if (!result.Success)
                {
                    return BadRequest(new { error = result.ErrorReason ?? "PaymentIntentCreationFailed" });
                }

                return Ok(new { paymentIntentId = result.PaymentIntentId, clientSecret = result.ClientSecret });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("payment-confirmed")]
        public async Task<ActionResult> PaymentIntentConfirmed([FromBody] PaymentConfirmRequest request)
        {
            try
            {
                var result = await _paymentService.ConfirmPaymentIntentAsync(request);
                if (!result.Success)
                {
                    return BadRequest(new { paymentIntentId = request.PaymentIntentId, status = result.Status });
                }

                return Ok(new { paymentIntentId = request.PaymentIntentId, status = result.Status });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Internal server error." });
            }
        }

        [HttpPost("{paymentIntentId}/refund")]
        public async Task<ActionResult> RefundPayment(string paymentIntentId, [FromBody] RefundRequest request)
        {
            try
            {
                request.PaymentIntentId = paymentIntentId;
                var result = await _paymentService.RefundPaymentAsync(request);
                if (!result.Success)
                {
                    return BadRequest(new { paymentIntentId, status = result.Status });
                }

                return Ok(new { refundId = result.RefundId, status = result.Status });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Internal server error." });
            }
        }

        [HttpGet("payment-status/{paymentIntentId}")]
        public async Task<ActionResult> GetPaymentStatus(string paymentIntentId)
        {
            var status = await _paymentService.GetPaymentStatusAsync(paymentIntentId);
            return Ok(status);
        }
    }
}

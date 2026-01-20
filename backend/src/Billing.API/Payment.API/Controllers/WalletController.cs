using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Payment.API.Models;
using Payment.API.Services;

namespace Payment.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WalletController : ControllerBase
    {
        private readonly WalletService _walletService;
        private readonly PaymentService _paymentService;

        public WalletController(IConfiguration configuration, WalletService walletService, PaymentService paymentService)
        {
            _walletService = walletService;
            _paymentService = paymentService;
        }
        [HttpPost("create/{userId}/{appId}")]
        public async Task<ActionResult> CreateWallet(string userId, string appId)
        {
            try
            {
                var walletInfo = await _walletService.GetWalletAsync(userId, appId);

                if (walletInfo == null)
                {
                    var response = await _walletService.CreateWalletAsync(userId, appId);
                    return Ok(response);
                }
                return Ok(walletInfo);


            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
        /// <summary>
        /// Get wallet balance.
        /// </summary>
        [HttpGet("{walletId}/balance")]
        public async Task<ActionResult> GetBalance(string walletId)
        {
            try
            {
                var balance = await _walletService.GetBalanceAsync(walletId);
                return Ok(new { walletId, balance });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get wallet transaction history.
        /// </summary>
        [HttpGet("{walletId}/transactions")]
        public async Task<ActionResult> GetTransactions(string walletId)
        {
            try
            {
                var transactions = await _walletService.GetTransactionsAsync(walletId);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Refund a specific payment.
        /// </summary>
        [HttpPost("{walletId}/refund")]
        public async Task<ActionResult> RefundPayment(string walletId, [FromBody] RefundRequest request)
        {
            try
            {
                request.PaymentIntentId = request.PaymentIntentId;
                request.WalletId = walletId;
                var result = await _paymentService.RefundPaymentAsync(request);
                if (!result.Success)
                {
                    return BadRequest(new { error = result.Status ?? "RefundFailed" });
                }

                return Ok(new { message = "Refund processed successfully.", refundId = result.RefundId, status = result.Status });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}

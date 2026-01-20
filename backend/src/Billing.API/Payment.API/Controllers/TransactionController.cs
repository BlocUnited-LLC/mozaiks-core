using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Payment.API.Services;

namespace Payment.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionController : ControllerBase
    {
        private readonly TransactionService _transactionService;

        public TransactionController(TransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <summary>
        /// Get all transactions for a specific user
        /// </summary>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetAllByUser(string userId)
        {
            var result = await _transactionService.GetAllTransactionsAsync(userId);
            return Ok(result);
        }

        /// <summary>
        /// Get transaction by internal transaction ID
        /// </summary>
        [HttpGet("{transactionId}")]
        public async Task<IActionResult> GetById(string transactionId)
        {
            var result = await _transactionService.GetByIdAsync(transactionId);
            return result == null ? NotFound() : Ok(result);
        }

        /// <summary>
        /// Get transaction by PaymentIntent ID
        /// </summary>
        [HttpGet("intent/{paymentIntentId}")]
        public async Task<IActionResult> GetByPaymentIntent(string paymentIntentId)
        {
            var result = await _transactionService.GetByIntentIdAsync(paymentIntentId);
            return result == null ? NotFound() : Ok(result);
        }

        /// <summary>
        /// Get transaction history for a wallet (creator, payer, investor)
        /// </summary>
        [HttpGet("wallet/{walletId}/history")]
        public async Task<IActionResult> GetByWallet(string walletId)
        {
            var result = await _transactionService.GetTransactionHistoryByWalletAsync(walletId);
            return Ok(result);
        }

        /// <summary>
        /// Manually update transaction status (Admin use)
        /// </summary>
        [HttpPatch("{transactionId}/status")]
        public async Task<IActionResult> UpdateStatus(string transactionId, [FromBody] UpdateStatusRequest request)
        {
            await _transactionService.UpdateStatusAsync(transactionId, request.Status);
            return NoContent();
        }

        /// <summary>
        /// Manually trigger payment failure handler (test/admin/debug use)
        /// </summary>
        [HttpPost("fail")]
        public async Task<IActionResult> HandleFailed([FromBody] HandleFailedRequest request)
        {
            await _transactionService.HandlePaymentFailedAsync(request.WalletId, request.PaymentIntentId);
            return Ok(new { message = "Marked as failed and ledger updated." });
        }
    }
}
public class UpdateStatusRequest
{
    public string Status { get; set; } = "Failed"; // or Succeeded
}

public class HandleFailedRequest
{
    public string WalletId { get; set; }
    public string PaymentIntentId { get; set; }
}

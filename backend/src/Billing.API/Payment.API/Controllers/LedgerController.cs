using Microsoft.AspNetCore.Mvc;
using Payment.API.Services;

namespace Payment.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LedgerController : ControllerBase
    {
        private readonly LedgerService _ledgerService;

        public LedgerController(LedgerService ledgerService)
        {
            _ledgerService = ledgerService;
        }

        /// <summary>
        /// Get all ledger entries by walletId
        /// </summary>
        [HttpGet("wallet/{walletId}")]
        public async Task<IActionResult> GetByWallet(string walletId)
        {
            var entries = await _ledgerService.GetEntriesByWalletAsync(walletId);
            return Ok(entries);
        }

        /// <summary>
        /// Get all ledger entries by transactionId
        /// </summary>
        [HttpGet("transaction/{transactionId}")]
        public async Task<IActionResult> GetByTransaction(string transactionId)
        {
            var entries = await _ledgerService.GetEntriesByTransactionAsync(transactionId);
            return Ok(entries);
        }
    }
}

using Payment.API.Models;
using Payment.API.Repository.Interfaces;

namespace Payment.API.Services
{
    public class LedgerService
    {
        private readonly ILedgerRepository _ledgerRepo;

        public LedgerService(ILedgerRepository ledgerRepo)
        {
            _ledgerRepo = ledgerRepo;
        }

        /// <summary>
        /// Record a single ledger entry
        /// </summary>
        public async Task RecordEntryAsync(LedgerEntryModel entry)
        {
            await _ledgerRepo.CreateAsync(entry);
        }

        /// <summary>
        /// Record multiple entries atomically (if needed in batch)
        /// </summary>
        public async Task RecordBatchAsync(IEnumerable<LedgerEntryModel> entries)
        {
            foreach (var entry in entries)
            {
                await _ledgerRepo.CreateAsync(entry);
            }
        }

        /// <summary>
        /// Get all ledger entries for a wallet
        /// </summary>
        public Task<List<LedgerEntryModel>> GetEntriesByWalletAsync(string walletId)
        {
            return _ledgerRepo.GetEntriesByWalletIdAsync(walletId);
        }

        /// <summary>
        /// Get all ledger entries for a transaction
        /// </summary>
        public Task<List<LedgerEntryModel>> GetEntriesByTransactionAsync(string transactionId)
        {
            return _ledgerRepo.GetEntriesByTransactionIdAsync(transactionId);
        }
    }
}

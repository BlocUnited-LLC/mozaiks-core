using Payment.API.Models;

namespace Payment.API.Repository.Interfaces
{
    public interface ILedgerRepository
    {
        Task CreateAsync(LedgerEntryModel entry);
        Task<List<LedgerEntryModel>> GetEntriesByTransactionIdAsync(string transactionId);
        Task<List<LedgerEntryModel>> GetEntriesByWalletIdAsync(string walletId);
    }
}

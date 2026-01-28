using Payment.API.Models;

namespace Payment.API.Repository.Interfaces
{
    public interface ITransactionRepository
    {
        Task CreateTransactionAsync(TransactionModel transaction);
        Task<TransactionModel> GetByIntentIdAsync(string paymentIntentId);
        Task<TransactionModel> GetByIdAsync(string transactionId);
        Task<List<TransactionModel>> GetAllTransactionsAsync(string userId);
        Task<List<TransactionModel>> GetPendingRefundsAsync();
        Task<List<TransactionModel>> GetPendingSettlementsAsync();
        Task<long> CountPendingAsync(string transactionType);
        Task UpdateWalletBalanceAndTransactionStatusAsync(string walletId, string paymentIntentId, long amountDelta, string status);
        Task HandlePaymentFailedAsync(string walletId, string paymentIntentId);
        Task<List<TransactionModel>> GetTransactionHistoryByWalletAsync(string walletId);
        Task UpdateStatusAsync(string transactionId, string status);

        Task<TransactionModel?> GetLatestByTypeAsync(string transactionType, string userId, string? appId);
        Task<TransactionModel?> GetBySubscriptionIdAsync(string subscriptionId);
        Task UpdateSubscriptionContractAsync(string transactionId, string status, DateTime? currentPeriodEndUtc);
    }
}

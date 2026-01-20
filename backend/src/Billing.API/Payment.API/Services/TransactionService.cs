using Payment.API.Models;
using Payment.API.Repository.Interfaces;

namespace Payment.API.Services
{
    public class TransactionService
    {
        private readonly ITransactionRepository _transactionRepo;

        public TransactionService(ITransactionRepository transactionRepo)
        {
            _transactionRepo = transactionRepo;
        }

        public Task CreateTransactionAsync(TransactionModel transaction)
        {
            return _transactionRepo.CreateTransactionAsync(transaction);
        }

        public Task<List<TransactionModel>> GetAllTransactionsAsync(string userId)
        {
            return _transactionRepo.GetAllTransactionsAsync(userId);
        }

        public Task<List<TransactionModel>> GetPendingRefundsAsync() => _transactionRepo.GetPendingRefundsAsync();

        public Task<List<TransactionModel>> GetPendingSettlementsAsync() => _transactionRepo.GetPendingSettlementsAsync();

        public Task<long> CountPendingAsync(string transactionType) => _transactionRepo.CountPendingAsync(transactionType);

        public Task<TransactionModel> GetByIdAsync(string transactionId)
        {
            return _transactionRepo.GetByIdAsync(transactionId);
        }

        public Task<TransactionModel> GetByIntentIdAsync(string paymentIntentId)
        {
            return _transactionRepo.GetByIntentIdAsync(paymentIntentId);
        }

        public async Task<List<PaymentTransactionModel>> GetTransactionHistoryByWalletAsync(string walletId)
        {
            var transactions = await _transactionRepo.GetTransactionHistoryByWalletAsync(walletId);

            return transactions.Select(t => new PaymentTransactionModel
            {
                TransactionId = t.Id,
                PaymentIntentId = t.PaymentIntentId,
                Amount = t.Amount,
                Currency = t.Currency,
                TransactionType = t.TransactionType,
                Status = t.Status,
                CreatedAt = t.CreatedAt
            }).ToList();
        }

        public Task HandlePaymentFailedAsync(string walletId, string paymentIntentId)
        {
            return _transactionRepo.HandlePaymentFailedAsync(walletId, paymentIntentId);
        }

        public Task UpdateWalletBalanceAndTransactionStatusAsync(string walletId, string paymentIntentId, long amountDelta, string status)
        {
            return _transactionRepo.UpdateWalletBalanceAndTransactionStatusAsync(walletId, paymentIntentId, amountDelta, status);
        }

        public Task UpdateStatusAsync(string transactionId, string status)
        {
            return _transactionRepo.UpdateStatusAsync(transactionId, status);
        }

        public Task<TransactionModel?> GetLatestByTypeAsync(string transactionType, string userId, string? appId)
        {
            return _transactionRepo.GetLatestByTypeAsync(transactionType, userId, appId);
        }

        public Task<TransactionModel?> GetBySubscriptionIdAsync(string subscriptionId)
        {
            return _transactionRepo.GetBySubscriptionIdAsync(subscriptionId);
        }

        public Task UpdateSubscriptionContractAsync(string transactionId, string status, DateTime? currentPeriodEndUtc)
        {
            return _transactionRepo.UpdateSubscriptionContractAsync(transactionId, status, currentPeriodEndUtc);
        }
    }
}

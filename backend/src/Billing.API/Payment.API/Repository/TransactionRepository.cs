using MongoDB.Driver;
using Payment.API.Models;
using Payment.API.Repository.Interfaces;

namespace Payment.API.Repository
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly IMongoCollection<TransactionModel> _transactions;
        private readonly ILedgerRepository _ledgerRepository;

        public TransactionRepository(IMongoDatabase database, ILedgerRepository ledgerRepository)
        {
            _transactions = database.GetCollection<TransactionModel>("Transactions");
            _ledgerRepository = ledgerRepository;
        }

        public async Task CreateTransactionAsync(TransactionModel transaction)
        {
            await _transactions.InsertOneAsync(transaction);
        }

        public async Task<List<TransactionModel>> GetAllTransactionsAsync(string userId)
        {
            var filter = Builders<TransactionModel>.Filter.Eq(t => t.Metadata.PayerUserId, userId);
            return await _transactions.Find(filter).SortByDescending(t => t.CreatedAt).ToListAsync();
        }

        public async Task<TransactionModel> GetByIdAsync(string transactionId)
        {
            var filter = Builders<TransactionModel>.Filter.Eq(t => t.Id, transactionId);
            return await _transactions.Find(filter).FirstOrDefaultAsync();
        }


        public async Task<TransactionModel> GetByIntentIdAsync(string paymentIntentId)
        {
            return await _transactions.Find(t => t.PaymentIntentId == paymentIntentId).FirstOrDefaultAsync();
        }

        public async Task<List<TransactionModel>> GetPendingRefundsAsync()
        {
            return await _transactions
                .Find(t => t.TransactionType == "Refund" && t.Status == "Pending")
                .ToListAsync();
        }

        public async Task<List<TransactionModel>> GetPendingSettlementsAsync()
        {
            return await _transactions
                .Find(t => t.TransactionType == "Settlement" && t.Status == "Pending")
                .ToListAsync();
        }

        public async Task<long> CountPendingAsync(string transactionType)
        {
            return await _transactions
                .CountDocumentsAsync(t => t.TransactionType == transactionType && t.Status == "Pending");
        }

        public async Task<List<TransactionModel>> GetTransactionHistoryByWalletAsync(string walletId)
        {
            var filter = Builders<TransactionModel>.Filter.Or(
                Builders<TransactionModel>.Filter.Eq(t => t.Metadata.AppCreatorId, walletId),
                Builders<TransactionModel>.Filter.Eq(t => t.Metadata.PayerUserId, walletId),
                Builders<TransactionModel>.Filter.ElemMatch(t => t.Metadata.InvestorShares, i => i.InvestorId == walletId)
            );

            return await _transactions.Find(filter)
                .SortByDescending(t => t.CreatedAt)
                .ToListAsync();
        }


        public async Task HandlePaymentFailedAsync(string walletId, string paymentIntentId)
        {
            var transaction = await _transactions.Find(t => t.PaymentIntentId == paymentIntentId).FirstOrDefaultAsync();

            if (transaction != null && !string.IsNullOrWhiteSpace(transaction.Id))
            {
                await UpdateStatusAsync(transaction.Id, "Failed");

                // Optional: Log failed reason or notify user
                var failedLedgerEntry = new LedgerEntryModel
                {
                    WalletId = walletId,
                    TransactionId = transaction.Id,
                    PaymentIntentId = transaction.PaymentIntentId,
                    Type = "Error",
                    Source = "PaymentProcessor",
                    Reason = "PaymentFailed",
                    Amount = transaction.Amount,
                };

                await _ledgerRepository.CreateAsync(failedLedgerEntry); // only if you wire `LedgerRepository` here
            }
        }


        public async Task UpdateStatusAsync(string transactionId, string status)
        {
            var filter = Builders<TransactionModel>.Filter.Eq(t => t.Id, transactionId);
            var update = Builders<TransactionModel>.Update.Set(t => t.Status, status);
            await _transactions.UpdateOneAsync(filter, update);
        }

        public async Task<TransactionModel?> GetLatestByTypeAsync(string transactionType, string userId, string? appId)
        {
            if (string.IsNullOrWhiteSpace(transactionType) || string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var filters = new List<FilterDefinition<TransactionModel>>
            {
                Builders<TransactionModel>.Filter.Eq(t => t.TransactionType, transactionType),
                Builders<TransactionModel>.Filter.Eq(t => t.Metadata.PayerUserId, userId)
            };

            if (!string.IsNullOrWhiteSpace(appId))
            {
                filters.Add(Builders<TransactionModel>.Filter.Eq(t => t.AppId, appId));
            }

            var filter = Builders<TransactionModel>.Filter.And(filters);

            return await _transactions
                .Find(filter)
                .SortByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<TransactionModel?> GetBySubscriptionIdAsync(string subscriptionId)
        {
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return null;
            }

            var filter = Builders<TransactionModel>.Filter.Eq(t => t.Metadata.SubscriptionId, subscriptionId);
            return await _transactions.Find(filter).FirstOrDefaultAsync();
        }

        public async Task UpdateSubscriptionContractAsync(string transactionId, string status, DateTime? currentPeriodEndUtc)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            var updates = new List<UpdateDefinition<TransactionModel>>
            {
                Builders<TransactionModel>.Update.Set(t => t.Status, status),
                Builders<TransactionModel>.Update.Set(t => t.UpdatedAt, DateTime.UtcNow)
            };

            if (currentPeriodEndUtc.HasValue)
            {
                updates.Add(Builders<TransactionModel>.Update.Set(t => t.Metadata.CurrentPeriodEndUtc, currentPeriodEndUtc.Value));
            }

            var update = Builders<TransactionModel>.Update.Combine(updates);
            await _transactions.UpdateOneAsync(Builders<TransactionModel>.Filter.Eq(t => t.Id, transactionId), update);
        }

        public Task UpdateWalletBalanceAndTransactionStatusAsync(string walletId, string paymentIntentId, long amountDelta, string status)
        {
            return UpdateWalletBalanceAndTransactionStatusInternalAsync(walletId, paymentIntentId, amountDelta, status);
        }

        private async Task UpdateWalletBalanceAndTransactionStatusInternalAsync(string walletId, string paymentIntentId, long amountDelta, string status)
        {
            if (string.IsNullOrWhiteSpace(walletId) || string.IsNullOrWhiteSpace(paymentIntentId))
            {
                return;
            }

            var transaction = await _transactions.Find(t => t.PaymentIntentId == paymentIntentId).FirstOrDefaultAsync();
            if (transaction != null && !string.IsNullOrWhiteSpace(transaction.Id))
            {
                await UpdateStatusAsync(transaction.Id, status);

                var entry = new LedgerEntryModel
                {
                    UserId = transaction.Metadata?.PayerUserId ?? walletId,
                    AppId = transaction.AppId ?? string.Empty,
                    WalletId = walletId,
                    TransactionId = transaction.Id,
                    PaymentIntentId = paymentIntentId,
                    Type = amountDelta >= 0 ? "Credit" : "Debit",
                    Source = "System",
                    Reason = status,
                    Amount = Math.Abs(amountDelta),
                    Currency = transaction.Currency ?? "usd"
                };

                await _ledgerRepository.CreateAsync(entry);
            }
        }
    }
}

         
 

using MongoDB.Driver;
using Payment.API.Models;
using Payment.API.Repository.Interfaces;

namespace Payment.API.Repository
{
    public class WalletRepository : IWalletRepository
    {


        private readonly IMongoCollection<WalletModel> _walletCollection;

        public WalletRepository(IMongoDatabase database)
        {
            _walletCollection = database.GetCollection<WalletModel>("Wallets");
        }

        // Create new wallet
        public async Task<WalletModel> CreateWalletAsync(WalletModel wallet)
        {
            await _walletCollection.InsertOneAsync(wallet);
            return wallet;
        }

        // Retrieve wallet by WalletId
        public async Task<WalletModel> GetWalletByIdAsync(string walletId)
        {
            return await _walletCollection.Find(w => w.Id == walletId).FirstOrDefaultAsync();
        }

        // Retrieve wallet by UserId
        public async Task<WalletModel> GetWalletByUserIdAsync(string userId)
        {
            return await _walletCollection.Find(w => w.UserId == userId).FirstOrDefaultAsync();
        }

        public async Task<WalletModel> GetWalletAsync(string walletId, string userId)
        {
            return await _walletCollection
                .Find(w => w.Id == walletId && w.UserId == userId)
                .FirstOrDefaultAsync();
        }

        public async Task<WalletModel> GetWalletByAppAsync(string userId, string appId)
        {
            return await _walletCollection
                .Find(w => w.AppId == appId && w.UserId == userId)
                .FirstOrDefaultAsync();
        }

        public async Task AddTransactionAsync(string walletId, WalletTransaction transaction)
        {
            var filter = Builders<WalletModel>.Filter.Eq(w => w.Id, walletId);
            var update = Builders<WalletModel>.Update
                .Push(w => w.Transactions, transaction)
                .Set(w => w.UpdatedAt, DateTime.UtcNow);

            await _walletCollection.UpdateOneAsync(filter, update);
        }

        public async Task UpdateWalletBalanceAndTransactionStatusAsync(string walletId, string paymentIntentId, long amountDelta, string status)
        {
            var filter = Builders<WalletModel>.Filter.And(
                Builders<WalletModel>.Filter.Eq(w => w.Id, walletId),
                Builders<WalletModel>.Filter.ElemMatch(w => w.Transactions, t => t.PaymentIntentId == paymentIntentId)
            );

            var update = Builders<WalletModel>.Update
                .Inc(w => w.Balance, amountDelta)
                .Set(w => w.UpdatedAt, DateTime.UtcNow)
                .Set("Transactions.$.Status", status); // Use positional operator $

            await _walletCollection.UpdateOneAsync(filter, update);
        }

        public async Task HandlePaymentFailedAsync(string walletId, string paymentIntentId)
        {
            var filter = Builders<WalletModel>.Filter.And(
                Builders<WalletModel>.Filter.Eq(w => w.Id, walletId),
                Builders<WalletModel>.Filter.ElemMatch(w => w.Transactions, t => t.PaymentIntentId == paymentIntentId)
            );

            var update = Builders<WalletModel>.Update
                .Set(w => w.UpdatedAt, DateTime.UtcNow)
                .Set(w => w.Transactions[-1].Status, "failed"); // -1 indicates matched array element

            await _walletCollection.UpdateOneAsync(filter, update);
        }
        // Retrieve transaction history
        public async Task<List<WalletTransaction>> GetTransactionHistoryAsync(string walletId)
            => (await GetWalletByIdAsync(walletId))?.Transactions.OrderByDescending(t => t.CreatedAt).ToList() ?? new List<WalletTransaction>();
    }
}

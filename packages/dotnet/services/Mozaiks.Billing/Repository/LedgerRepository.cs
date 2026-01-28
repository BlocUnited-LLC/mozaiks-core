using MongoDB.Driver;
using Payment.API.Models;
using Payment.API.Repository.Interfaces;

namespace Payment.API.Repository
{
    public class LedgerRepository: ILedgerRepository
    {
        private readonly IMongoCollection<LedgerEntryModel> _ledger;

        public LedgerRepository(IMongoDatabase db)
        {
            _ledger = db.GetCollection<LedgerEntryModel>("Ledger");
        }

        public Task CreateAsync(LedgerEntryModel entry)
            => _ledger.InsertOneAsync(entry);

        public Task<List<LedgerEntryModel>> GetEntriesByTransactionIdAsync(string transactionId)
            => _ledger.Find(x => x.TransactionId == transactionId).ToListAsync();
        public Task<List<LedgerEntryModel>> GetEntriesByWalletIdAsync(string walletId)
    => _ledger.Find(x => x.WalletId == walletId).ToListAsync();


    }
}

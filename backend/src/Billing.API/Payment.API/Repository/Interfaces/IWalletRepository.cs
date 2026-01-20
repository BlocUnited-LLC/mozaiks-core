using Payment.API.Models;

namespace Payment.API.Repository.Interfaces
{
    public interface IWalletRepository
    {
        Task<WalletModel> CreateWalletAsync(WalletModel wallet);
        Task<WalletModel> GetWalletByIdAsync(string walletId);
        Task<WalletModel> GetWalletByUserIdAsync(string userId);
        Task<WalletModel> GetWalletAsync(string walletId, string userId);
        Task<WalletModel> GetWalletByAppAsync(string userId, string appId);
        Task AddTransactionAsync(string walletId, WalletTransaction transaction);
        Task UpdateWalletBalanceAndTransactionStatusAsync(string walletId, string paymentIntentId, long amountDelta, string status);
        Task HandlePaymentFailedAsync(string walletId, string paymentIntentId);
        Task<List<WalletTransaction>> GetTransactionHistoryAsync(string walletId);
    }
}

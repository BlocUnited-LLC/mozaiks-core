using Payment.API.Models;

namespace Payment.API.Repository.Interfaces
{
    public interface IPaymentRepository
    {
        Task AddTransactionAsync(string walletId, PaymentTransactionModel transaction);
        Task UpdateWalletBalanceAndTransactionStatusAsync(string walletId, string paymentIntentId, long amountDelta, string status);
        Task HandlePaymentFailedAsync(string walletId, string paymentIntentId);
        Task<List<PaymentTransactionModel>> GetTransactionHistoryAsync(string walletId);
    }
}

using Payment.API.Models;
using Payment.API.Repository.Interfaces;
using Payment.API.Infrastructure.Observability;
using System.Diagnostics;

namespace Payment.API.Services
{
    /// <summary>
    /// Unified wallet service for internal balance management.
    /// Handles fiat currency balances tracked in the platform's internal ledger.
    /// 
    /// BLOCKCHAIN INTEGRATION ARCHITECTURE:
    /// When adding blockchain support, this service becomes the orchestrator:
    /// 
    /// 1. OPTION A - Abstraction Layer (Recommended):
    ///    Create IPaymentProvider interface with implementations:
    ///    - StripePaymentProvider (current fiat logic)
    ///    - EthereumPaymentProvider (Web3 integration)
    ///    - SolanaPaymentProvider (Solana integration)
    ///    WalletService routes to appropriate provider based on WalletType.
    /// 
    /// 2. OPTION B - Blockchain Service:
    ///    Create BlockchainWalletService that handles:
    ///    - Wallet generation (HD wallets, custodial vs non-custodial)
    ///    - Token balance queries (via RPC or indexer)
    ///    - Transaction signing and broadcasting
    ///    - Webhook listeners for blockchain events
    /// 
    /// Key integration points marked with "BLOCKCHAIN:" comments below.
    /// </summary>
    public class WalletService
    {
        private readonly IWalletRepository _walletRepository;
        private readonly StructuredLogEmitter _logs;
        private readonly ObservabilityMetrics _metrics;
        private readonly AnalyticsEventEmitter _analytics;
        private readonly ObservabilityTracing _tracing;
        
        // BLOCKCHAIN: Inject blockchain providers here when ready
        // private readonly IBlockchainWalletProvider? _blockchainProvider;

        public WalletService(
            IWalletRepository walletRepository,
            StructuredLogEmitter logs,
            ObservabilityMetrics metrics,
            AnalyticsEventEmitter analytics,
            ObservabilityTracing tracing)
        {
            _walletRepository = walletRepository;
            _logs = logs;
            _metrics = metrics;
            _analytics = analytics;
            _tracing = tracing;
        }

        /// <summary>
        /// Creates a new fiat wallet for the user.
        /// BLOCKCHAIN: Extend to optionally generate blockchain address alongside fiat wallet.
        /// </summary>
        public Task<WalletModel> CreateWalletAsync(string userId, string appId)
            => _walletRepository.CreateWalletAsync(new WalletModel
            {
                UserId = userId,
                AppId = appId,
                Balance = 0
            });

        public Task<WalletModel> GetWalletAsync(string userId, string appId)
            => _walletRepository.GetWalletByAppAsync(userId, appId);

        public async Task<WalletDebitResult> DebitAsync(string userId, string walletId, decimal amount, string? paymentIntentId = null)
        {
            using var span = _tracing.StartSpan("wallet.debit", new ObservabilitySpanContext
            {
                UserId = userId,
                PaymentIntentId = paymentIntentId
            });

            _metrics.RecordWalletDebitRequested();
            _logs.Info("Wallet.DebitRequested", new ActorContext { UserId = userId }, new EntityContext { PaymentIntentId = paymentIntentId }, new { amount, walletId });
            _analytics.Emit("server.wallet.debit_requested", new ActorContext { UserId = userId }, new EntityContext { PaymentIntentId = paymentIntentId }, new { amount, walletId });

            var stopwatch = Stopwatch.StartNew();
            var wallet = await _walletRepository.GetWalletByIdAsync(walletId);
            if (wallet == null || wallet.UserId != userId)
            {
                stopwatch.Stop();
                _metrics.RecordWalletDebitLatency(stopwatch.Elapsed);
                _metrics.RecordWalletDebitFailed("WalletNotFound");
                _logs.Warn("Wallet.DebitFailed", new ActorContext { UserId = userId }, new EntityContext { PaymentIntentId = paymentIntentId }, new { reason = "WalletNotFound" });
                _analytics.Emit("server.wallet.debit_failed", new ActorContext { UserId = userId }, new EntityContext { PaymentIntentId = paymentIntentId }, new { reason = "WalletNotFound" });
                return new WalletDebitResult { Success = false, ErrorReason = "WalletNotFound" };
            }

            if (wallet.Balance < amount)
            {
                stopwatch.Stop();
                _metrics.RecordWalletDebitLatency(stopwatch.Elapsed);
                _metrics.RecordWalletDebitFailed("InsufficientBalance");
                _logs.Warn("Wallet.DebitFailed", new ActorContext { UserId = userId }, new EntityContext { PaymentIntentId = paymentIntentId }, new { reason = "InsufficientBalance" });
                _analytics.Emit("server.wallet.debit_failed", new ActorContext { UserId = userId }, new EntityContext { PaymentIntentId = paymentIntentId }, new { reason = "InsufficientBalance" });
                return new WalletDebitResult { Success = false, ErrorReason = "InsufficientBalance" };
            }

            wallet.Balance -= (long)amount;
            await _walletRepository.UpdateWalletBalanceAndTransactionStatusAsync(walletId, paymentIntentId ?? Guid.NewGuid().ToString(), (long)(-amount), "Debited");
            stopwatch.Stop();
            _metrics.RecordWalletDebitLatency(stopwatch.Elapsed);
            _metrics.RecordWalletDebitSucceeded();

            _logs.Info("Wallet.DebitSucceeded", new ActorContext { UserId = userId }, new EntityContext { PaymentIntentId = paymentIntentId }, new { amount });
            _analytics.Emit("server.wallet.debit_completed", new ActorContext { UserId = userId }, new EntityContext { PaymentIntentId = paymentIntentId }, new { amount });
            
            return new WalletDebitResult { Success = true, WalletId = walletId };
        }

        public async Task<long> GetBalanceAsync(string walletId)
            => (await _walletRepository.GetWalletByIdAsync(walletId)).Balance;

        public Task<List<WalletTransaction>> GetTransactionsAsync(string walletId)
            => _walletRepository.GetTransactionHistoryAsync(walletId);
    }

    public class WalletDebitResult
    {
        public bool Success { get; set; }
        public string? WalletId { get; set; }
        public string? ErrorReason { get; set; }
    }
}

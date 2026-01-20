using System.Diagnostics.Metrics;
using System.Threading;

namespace Payment.API.Infrastructure.Observability
{
    public class ObservabilityMetrics
    {
        private readonly Meter _meter = new("mozaiks.payment-api", "1.0.0");

        private readonly Counter<long> _roundCreated;
        private readonly Counter<long> _roundOpened;
        private readonly Counter<long> _roundClosed;
        private readonly Counter<long> _roundFunded;
        private readonly Counter<long> _roundExecuted;

        private readonly Counter<long> _investmentStarted;
        private readonly Counter<long> _investmentFailed;
        private readonly Counter<long> _investmentCompleted;

        private readonly Counter<long> _walletDebitRequested;
        private readonly Counter<long> _walletDebitFailed;
        private readonly Counter<long> _walletDebitSucceeded;
        private readonly Counter<long> _walletCreditIssued;
        private readonly Counter<long> _walletRefundApplied;

        private readonly Counter<long> _paymentIntentCreated;
        private readonly Counter<long> _paymentConfirmFailed;

        private readonly Counter<long> _refundRequested;
        private readonly Counter<long> _refundInProgress;
        private readonly Counter<long> _refundCompleted;
        private readonly Counter<long> _refundFailed;

        private readonly Counter<long> _settlementStarted;
        private readonly Counter<long> _settlementCompleted;
        private readonly Counter<long> _settlementFailed;
        private readonly Counter<long> _settlementInvalidDestination;
        private readonly Counter<long> _refundWorkerRuns;
        private readonly Counter<long> _refundWorkerEmptyRuns;
        private readonly Counter<long> _refundWorkerErrors;
        private readonly Counter<long> _settlementWorkerRuns;
        private readonly Counter<long> _settlementWorkerEmptyRuns;
        private readonly Counter<long> _settlementWorkerErrors;

        private long _currentOpenRounds;
        private double _totalRaised;
        private long _pendingRefunds;
        private long _pendingSettlements;
        private readonly object _gaugeLock = new();

        private readonly Histogram<double> _apiRequestLatency;
        private readonly Histogram<double> _walletDebitLatency;
        private readonly Histogram<double> _paymentIntentLatency;
        private readonly Histogram<double> _paymentConfirmLatency;
        private readonly Histogram<double> _refundProcessLatency;
        private readonly Histogram<double> _settlementProcessLatency;

        public ObservabilityMetrics()
        {
            _roundCreated = _meter.CreateCounter<long>("mozaiks.payment.funding.round_created");
            _roundOpened = _meter.CreateCounter<long>("mozaiks.payment.funding.round_opened");
            _roundClosed = _meter.CreateCounter<long>("mozaiks.payment.funding.round_closed");
            _roundFunded = _meter.CreateCounter<long>("mozaiks.payment.funding.round_funded");
            _roundExecuted = _meter.CreateCounter<long>("mozaiks.payment.funding.round_executed");

            _investmentStarted = _meter.CreateCounter<long>("mozaiks.payment.investment.started");
            _investmentFailed = _meter.CreateCounter<long>("mozaiks.payment.investment.failed");
            _investmentCompleted = _meter.CreateCounter<long>("mozaiks.payment.investment.completed");

            _walletDebitRequested = _meter.CreateCounter<long>("mozaiks.wallet.debit_requested");
            _walletDebitFailed = _meter.CreateCounter<long>("mozaiks.wallet.debit_failed");
            _walletDebitSucceeded = _meter.CreateCounter<long>("mozaiks.wallet.debit_succeeded");
            _walletCreditIssued = _meter.CreateCounter<long>("mozaiks.wallet.credit_issued");
            _walletRefundApplied = _meter.CreateCounter<long>("mozaiks.wallet.refund_applied");

            _paymentIntentCreated = _meter.CreateCounter<long>("mozaiks.payment.intent_created");
            _paymentConfirmFailed = _meter.CreateCounter<long>("mozaiks.payment.confirm_failed");

            _refundRequested = _meter.CreateCounter<long>("mozaiks.payment.refund.requested");
            _refundInProgress = _meter.CreateCounter<long>("mozaiks.payment.refund.in_progress");
            _refundCompleted = _meter.CreateCounter<long>("mozaiks.payment.refund.completed");
            _refundFailed = _meter.CreateCounter<long>("mozaiks.payment.refund.failed");
            _refundWorkerRuns = _meter.CreateCounter<long>("mozaiks.payment.refund.worker_runs");
            _refundWorkerEmptyRuns = _meter.CreateCounter<long>("mozaiks.payment.refund.worker_empty_runs");
            _refundWorkerErrors = _meter.CreateCounter<long>("mozaiks.payment.refund.worker_errors");

            _settlementStarted = _meter.CreateCounter<long>("mozaiks.payment.settlement.started");
            _settlementCompleted = _meter.CreateCounter<long>("mozaiks.payment.settlement.completed");
            _settlementFailed = _meter.CreateCounter<long>("mozaiks.payment.settlement.failed");
            _settlementInvalidDestination = _meter.CreateCounter<long>("mozaiks.payment.settlement.invalid_destination");
            _settlementWorkerRuns = _meter.CreateCounter<long>("mozaiks.payment.settlement.worker_runs");
            _settlementWorkerEmptyRuns = _meter.CreateCounter<long>("mozaiks.payment.settlement.worker_empty_runs");
            _settlementWorkerErrors = _meter.CreateCounter<long>("mozaiks.payment.settlement.worker_errors");

            _meter.CreateObservableGauge("mozaiks.payment.funding.current_open_rounds", () => new Measurement<long>(_currentOpenRounds));
            _meter.CreateObservableGauge("mozaiks.payment.funding.total_raised", () => new Measurement<double>(_totalRaised));
            // Phase 5 required gauge names
            _meter.CreateObservableGauge("pending_refunds_count", () => new Measurement<long>(_pendingRefunds));
            _meter.CreateObservableGauge("pending_settlements_count", () => new Measurement<long>(_pendingSettlements));

            _apiRequestLatency = _meter.CreateHistogram<double>("mozaiks.payment.latency.api_request");
            _walletDebitLatency = _meter.CreateHistogram<double>("mozaiks.wallet.latency.debit");
            _paymentIntentLatency = _meter.CreateHistogram<double>("mozaiks.payment.latency.intent_create");
            _paymentConfirmLatency = _meter.CreateHistogram<double>("mozaiks.payment.latency.confirm");
            _refundProcessLatency = _meter.CreateHistogram<double>("mozaiks.payment.refund.latency.process");
            _settlementProcessLatency = _meter.CreateHistogram<double>("mozaiks.payment.settlement.latency.process");
        }

        public void SetCurrentOpenRounds(long value) => Interlocked.Exchange(ref _currentOpenRounds, value);
        public void SetTotalRaised(decimal total)
        {
            lock (_gaugeLock)
            {
                _totalRaised = (double)total;
            }
        }
        public void RefillPendingRefundsGauge(long count) => Interlocked.Exchange(ref _pendingRefunds, count);
        public void RefillPendingSettlementsGauge(long count) => Interlocked.Exchange(ref _pendingSettlements, count);

        public void IncrementRoundCreated() => _roundCreated.Add(1);
        public void IncrementRoundOpened() => _roundOpened.Add(1);
        public void IncrementRoundClosed() => _roundClosed.Add(1);
        public void IncrementRoundFunded() => _roundFunded.Add(1);
        public void IncrementRoundExecuted() => _roundExecuted.Add(1);

        public void RecordInvestmentStarted(string? userId = null, string? appId = null, string? roundId = null, string? investmentId = null) =>
            _investmentStarted.Add(1, new KeyValuePair<string, object?>[]
            {
                new("userId", userId ?? string.Empty),
                new("appId", appId ?? string.Empty),
                new("roundId", roundId ?? string.Empty),
                new("investmentId", investmentId ?? string.Empty)
            });

        public void RecordInvestmentFailed(string reason, string? userId = null, string? appId = null, string? roundId = null, string? investmentId = null) =>
            _investmentFailed.Add(1, new KeyValuePair<string, object?>[]
            {
                new("reason", reason),
                new("userId", userId ?? string.Empty),
                new("appId", appId ?? string.Empty),
                new("roundId", roundId ?? string.Empty),
                new("investmentId", investmentId ?? string.Empty)
            });
        public void RecordInvestmentCompleted() => _investmentCompleted.Add(1);

        public void RecordWalletDebitRequested() => _walletDebitRequested.Add(1);
        public void RecordWalletDebitFailed(string reason) => _walletDebitFailed.Add(1, KeyValuePair.Create<string, object?>("reason", reason));
        public void RecordWalletDebitSucceeded() => _walletDebitSucceeded.Add(1);
        public void RecordWalletCreditIssued() => _walletCreditIssued.Add(1);
        public void RecordWalletRefundApplied() => _walletRefundApplied.Add(1);

        public void RecordPaymentIntentCreated() => _paymentIntentCreated.Add(1);
        public void RecordPaymentConfirmFailed(string reason) => _paymentConfirmFailed.Add(1, KeyValuePair.Create<string, object?>("reason", reason));

        public void RecordRefundRequested() => _refundRequested.Add(1);
        public void RecordRefundInProgress() => _refundInProgress.Add(1);
        public void RecordRefundCompleted() => _refundCompleted.Add(1);
        public void RecordRefundFailed() => _refundFailed.Add(1);
        public void RecordRefundWorkerRun() => _refundWorkerRuns.Add(1);
        public void RecordRefundWorkerEmptyRun() => _refundWorkerEmptyRuns.Add(1);
        public void RecordRefundWorkerError() => _refundWorkerErrors.Add(1);

        public void RecordSettlementStarted() => _settlementStarted.Add(1);
        public void RecordSettlementCompleted() => _settlementCompleted.Add(1);
        public void RecordSettlementFailed() => _settlementFailed.Add(1);
        public void RecordSettlementInvalidDestination(string reason) => _settlementInvalidDestination.Add(1, KeyValuePair.Create<string, object?>("reason", reason));
        public void RecordSettlementWorkerRun() => _settlementWorkerRuns.Add(1);
        public void RecordSettlementWorkerEmptyRun() => _settlementWorkerEmptyRuns.Add(1);
        public void RecordSettlementWorkerError() => _settlementWorkerErrors.Add(1);

        public void RecordApiRequestLatency(TimeSpan duration) => _apiRequestLatency.Record(duration.TotalMilliseconds);
        public void RecordWalletDebitLatency(TimeSpan duration) => _walletDebitLatency.Record(duration.TotalMilliseconds);
        public void RecordPaymentIntentLatency(TimeSpan duration) => _paymentIntentLatency.Record(duration.TotalMilliseconds);
        public void RecordPaymentConfirmLatency(TimeSpan duration) => _paymentConfirmLatency.Record(duration.TotalMilliseconds);
        public void RecordRefundProcessLatency(TimeSpan duration) => _refundProcessLatency.Record(duration.TotalMilliseconds);
        public void RecordSettlementProcessLatency(TimeSpan duration) => _settlementProcessLatency.Record(duration.TotalMilliseconds);
    }
}

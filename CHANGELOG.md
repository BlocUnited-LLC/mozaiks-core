# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]
### Added
- **UI Component Discovery** (`@mozaiks/workflow-ui-discovery`)
  - Automatic index.js generation for workflow UI components
  - File watcher for dev mode auto-regeneration
  - CLI tool: `workflow-ui-discover`
  - Runtime integration for automatic initialization
  - Zero manual steps for self-hosters
  - Configuration schema with JSON Schema validation
  - Contract documentation: `docs/contracts/UI_COMPONENT_DISCOVERY.md`

- **Entitlement System**: Complete entitlement and billing alignment with mozaiks-platform
  - `IPaymentProvider` interface for payment abstraction
  - `NoOpPaymentProvider` for self-hosted/dev mode (unlimited features)
  - `PlatformPaymentProvider` for platform-hosted mode (delegates to Platform)
  - `EntitlementSyncController` to receive manifest updates from Platform
  - `EntitlementManifestRepository` for MongoDB persistence
  - `UsageEventController` to receive token usage events from AI runtime
  - `UsageEventRepository` for usage event storage with TTL
  - Token usage metrics in `ObservabilityMetrics`
  
- **Python Entitlements Package** (`runtime/ai/src/core/entitlements/`)
  - `EntitlementManifest` dataclass for declarative entitlement state
  - `EntitlementSource` ABC with `LocalFileSource`, `EnvironmentSource`, `ControlPlaneSource`
  - `TokenBudgetTracker` for token usage tracking with enforcement levels
  - `FeatureGate` for feature flag checking
  - `EntitlementMiddleware` for workflow execution integration
  - Comprehensive test suite

- **SubscriptionManagerWorkflow**: AI workflow for subscription-related user interactions
  - Usage breakdown tool
  - Cost estimation tool
  - Plan suggestions tool
  - Upgrade handling

### Documentation
- `docs/architecture/specs/core-platform-alignment.md` - Full alignment specification
- `docs/architecture/specs/PLATFORM_COORDINATION.md` - Platform integration requirements

## [0.1.0] - 2026-01-21
### Added
- Initial public release
- Core runtime + AI runtime documentation
- Plugin contract specifications

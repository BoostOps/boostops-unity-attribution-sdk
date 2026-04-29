# Changelog

All notable changes to the BoostOps Unity SDK will be documented in this file.

## [1.1.0] - 2026-04-28

### Changed (BREAKING wire change)

- **Purchases now use the dedicated `/v1/purchases` endpoint instead of the generic event log.** This matches the industry pattern (AppsFlyer Purchase Connector / `validateAndSendInAppPurchase`, Adjust `VerifyAndTrackPurchase`, Branch `logEvent(PURCHASE, SKPaymentTransaction)`) and gives revenue events:
  - Idempotency on `(project_id, store, transaction_id)` — replays and reinstalls collapse cleanly.
  - Synchronous bronze persistence on the server — the ack means "durable."
  - A typed wire shape with field-level validation (store enum, ISO 4217 currency, ≤32KB receipt, sandbox flag, sub/trial flags, `original_transaction_id`).
- **`/v1/events` and `/v1/purchases` now share an identical common envelope.** Schema metadata, the four-tier identifier hierarchy (`boostops_id`, `install_id`, `custom_user_id`, `session_id`, plus `install_time_ms`), routing flags (`is_unity_editor`, `is_debug_build`, `is_testflight`, `is_emulator`), the `consent` block, and the device/platform `context` block are populated by a single builder and serialized by a single emitter, so the two endpoints can never drift in what they collect.
- `BoostOps-SDK` no longer emits the `boostops_purchase` event on `/v1/events`. The events client will refuse to enqueue it and log an error pointing at the new path. Only `boostops_impression`, `boostops_click`, `boostops_open`, and `boostops_install_attribution_update` flow through `/v1/events`.
- `IAnalyticsProvider.TrackPurchase` removed from the interface. `BoostOpsAnalyticsContract.TrackPurchase` calls Unity Analytics and Firebase Analytics directly for third-party mirroring; their concrete `TrackPurchase` methods remain in place. `BoostOpsAnalyticsProvider.TrackPurchase` is deleted (purchases no longer go through the BoostOps event provider).

### Added

- `BoostOps.Analytics.BoostOpsPurchaseClient` — dedicated singleton that ships purchases to `POST /v1/purchases`. Per-purchase JSON files under `persistentDataPath/BoostOps/purchases/` give us crash-safe, retry-until-acked durability. Exponential backoff with jitter, capped at 10 minutes. 4xx validation errors are non-recoverable and dropped; 5xx and network errors retry.
- `BoostOps.BoostOpsPurchaseInfo` — typed input for the advanced `BoostOpsAnalyticsContract.TrackPurchase(BoostOpsPurchaseInfo)` overload. Use it when you need subscription/trial flags, original transaction IDs, sandbox overrides, or stable client event IDs across retries.
- `BoostOps.Analytics.BoostOpsCommonPayload` + `BoostOpsCommonPayloadBuilder` + `BoostOpsCommonPayloadJson` — single source of truth for the shared envelope that both endpoints carry. The events serializer's `consent` and `context` rendering now delegates to this module so adding a new envelope field is a one-edit change instead of a two-pipeline change.
- `BoostOps.Analytics.BoostOpsPurchaseRequest` — internal wire-shape mirror of the server's `PurchaseRequest`. Holds the shared `Common` envelope alongside the purchase-specific fields, so `bronze.raw_purchase.raw_payload` ends up structurally identical to `bronze.raw_event.raw_payload` (modulo the purchase-specific tail).

### Removed

- `BoostOpsEventBuilder.CreatePurchaseEvent` and the `EventBuilder.Purchase` factory — dead code now that purchases bypass the event log.
- Purchase-specific install_id recovery branch in `BoostOpsAnalyticsClient.ValidateEventData` — the new client carries the identifier in its own request payload.

### Compatibility

- Public `BoostOpsSDK.TrackPurchase(...)` and `BoostOpsAnalyticsContract.TrackPurchase(...)` signatures are unchanged. Existing app code does not need to be updated.
- The Unity IAP `TrackPurchase(Product)` overload continues to work and now delivers through the new pipeline.

## [1.0.5] - 2026-04-21

- Fix UPM package name to match Unity Asset Store listing (`io.boostops.attribution-sdk`)
- Fix all compile errors for Firebase-only and no-Unity-Services projects
- Fix compile errors when optional Remote Config packages are not installed
- Fix Android dependency resolution (play-services-appset, ads-identifier, basement)

## [1.0.1] - 2026-03-05

- Version bump and package distribution improvements
- Updated package metadata and build pipeline

## [1.0.0] - Initial Release

- Complete mobile attribution platform for Unity
- Install tracking and campaign performance measurement
- Deep link configuration (Universal Links & App Links)
- Built-in cross-promotion for app portfolio growth
- Unity Editor integration with visual workflow
- iOS and Android platform support
- Analytics and event tracking
- Remote config integration
- DLL-protected distribution for IP security

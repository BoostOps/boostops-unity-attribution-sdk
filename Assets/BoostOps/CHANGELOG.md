# Changelog

All notable changes to the BoostOps Unity SDK will be documented in this file.

## [1.2.1] - 2026-09-25

### Added

- **UPM (Unity Package Manager) installs now work.** The SDK ships assembly
  definitions — `BoostOps` (runtime), `BoostOps.Editor` (editor), and
  `BoostOps.Examples` — so installing via Git URL
  (`https://github.com/BoostOps/boostops-unity-attribution-sdk.git?path=/Assets/BoostOps#v1.2.1`)
  compiles correctly. Previously Unity silently skipped all package scripts
  because none were covered by an assembly definition. Optional integrations
  (Unity Remote Config, Firebase Remote Config, Unity IAP, Addressables)
  resolve automatically when those packages are present and are ignored when
  absent.
- `com.unity.nuget.newtonsoft-json` is declared as a package dependency
  (required by the editor tooling); UPM installs pull it in automatically.

### Notes for existing `.unitypackage` / submodule users

- SDK code now compiles into its own `BoostOps` assemblies instead of
  `Assembly-CSharp`. No API changes; your calls into `BoostOpsSDK` continue to
  work unchanged (the assemblies are auto-referenced).

## [1.2.0] - 2026-09-25

### Security

- **Removed a hardcoded JWT from editor source.** The internal OAuth debug menu
  item now reads the token from the system clipboard instead of embedding
  credential material in the codebase.
- **Editor API logging now redacts credentials.** `UnityWaspClient` masks
  `jwt_token`, `token`, `api_key`, and access/refresh token fields before any
  request or response body reaches the Unity console or `Editor.log`.
- **Deep link domain validation now fails closed.** Links are rejected when no
  link domains are configured or when the URL cannot be parsed. Previously
  both cases accepted every URL, allowing attribution spoofing. If you use
  Dynamic Links, make sure your domains are listed in BoostOps project
  settings.
- **Raw IDFA is no longer persisted.** IDFA-reset detection compares SHA-256
  hashes; the legacy plaintext cache key is deleted automatically on first run.
- **Event payload logging is gated.** Full analytics payloads (identifiers,
  receipts, deep links) are only logged when debug logging is explicitly
  enabled — never in release builds by default.
- **Analytics endpoint overrides are validated.** Remote-config and
  server-driven endpoint changes must be HTTPS on `boostops.io` (or a
  subdomain); anything else is rejected and the default endpoint is kept.
- **All hand-built JSON now escapes string fields** (deep link URLs, campaign
  slugs, install referrers, custom user IDs), eliminating payload corruption
  and field-injection from untrusted strings.

### Reliability

- **Durable offline event persistence.** Events that cannot be sent are now
  written as full JSON payloads under `persistentDataPath` (matching the
  purchase pipeline), replayed automatically on the next launch, and deleted
  only after the server acknowledges them. Previously only lossy markers were
  stored — events unsent at process death were lost.
- **Flush-on-background actually works.** Pause/focus/quit hooks moved onto the
  SDK's MonoBehaviour runner (they previously lived on a plain C# class and
  never fired); the event queue is snapshotted to disk at each of those points.
- **`BoostOpsSDK.Init()` is now idempotent**, matching its documented contract.
  Repeat calls no longer regenerate the session ID or fire duplicate
  `app_open` events, and initialization failures surface through
  `OnInitFailed` instead of leaving half-initialized state.
- **Server kill switch persists across restarts** with a 24-hour re-probe, and
  the background batch path now honors server config (kill switch, schema and
  endpoint updates) identically to the explicit flush path.
- **Android JNI objects are disposed deterministically** in the revenue
  tracker and event builder (previously leaked JNI references on the
  analytics hot path).
- **Texture/sprite memory management.** Downloaded-asset caches are bounded
  with LRU eviction, and cross-promo UI destroys runtime-created textures and
  sprites on teardown.
- **Frequency caps and campaign date ranges use UTC**, so device timezone or
  clock changes can't reset caps or flip campaigns; dayparting (day-of-week /
  hour restrictions) intentionally remains in local time. Session caps now
  accumulate correctly for the whole app session.
- **Editor: static state resets correctly** when Enter Play Mode Options has
  domain reload disabled.
- **Android: legacy cross-promo config loads from StreamingAssets** via
  `UnityWebRequest` (a direct file check always failed inside the APK).
- Fixed a PlayerPrefs migration edge case where missing legacy float keys
  could be migrated as `0.0`.

### Added

- **Microsoft Store campaign attribution (Windows).** Cold starts on UWP and on
  Store-signed Standalone Windows (MSIX) builds now read the install-time
  campaign id via `Windows.Services.Store.StoreContext` and tag the next
  `first_open` event with the campaign automatically — no host-app glue
  required. Wire mapping reuses the same `attribution_*` fields the SDK already
  populates for Android Play Install Referrer and iOS Apple Search Ads:
  - `attribution_source = "microsoft_store"`
  - `attribution_channel = "ua:microsoft_store"`
  - `attribution_campaign_slug = <cid>`
  - `attribution_campaign = <cid>`
  - `attribution_method = "deterministic"`
  - `touch_type = "click"`

  Falls back to the user's app-license JSON (`customPolicyField1`) for non-MSA
  installs, mirroring Microsoft's documented dual path. Empty cids (organic
  installs, sideloads, non-Store builds) are silently treated as organic.

  - **UWP (`UNITY_WSA`):** zero-config, works out of the box.
  - **StandaloneWindows / StandaloneWindows64 distributed via MSIX through the
    Microsoft Store:** install the `Microsoft.Windows.SDK.Contracts` NuGet
    (≥ 10.0.19041) and the SDK now flips `ENABLE_WINMD_SUPPORT` for you. No
    scripting-define edit required.
  - **Bare-.exe Standalone Windows builds:** no-op, no runtime cost.

- `BoostOps.BoostOpsMicrosoftStoreCampaign` — new public utility. Use
  `GetCachedCampaignId()` for synchronous reads from your own code, or rely on
  the SDK's automatic first-launch capture.
- `BoostOpsEnvironment.GetEnvironment()` now returns `"microsoft_store"` for
  Store-signed Windows builds and `"standalone"` for sideloaded / direct .exe
  builds. `BoostOpsEnvironment.IsMicrosoftStoreInstall()` is a new helper.
- `BoostOpsStoreDetector.DetectWindowsStore()` returns
  `AppStore.WindowsStore` for Store-signed packages instead of always
  `Sideloaded`, fixing cross-promo store-link selection on Windows targets.
- `BoostOpsEventBuilder.CreateAppOpenEvent` and the `BoostOpsAnalyticsContract`
  app-open chain accept optional `attributionSource` and `attributionMethod`
  parameters. Backward compatible — both default to `null`.

### Editor / Build pipeline

- `BoostOpsWindowsWinmdDefine` (`InitializeOnLoad`, modeled on
  `BoostOpsFirebaseDefine`) — auto-syncs the `ENABLE_WINMD_SUPPORT` scripting
  define for the StandaloneWindows target group based on whether the project
  has the `Microsoft.Windows.SDK.Contracts` NuGet installed. Detects via a
  loaded-assembly probe with a file-scan fallback under `Assets/`, so it works
  for NuGetForUnity, manual DLL drops, and `.csproj`-based workflows. Adds
  the define when the package is detected, removes it when stale. UWP is
  untouched (Unity owns `WINDOWS_UWP` for that target).
- `BoostOpsWindowsBuildPreprocessor` (`IPreprocessBuildWithReport`) — runs
  before every UWP and StandaloneWindows build and turns the most likely silent
  misconfiguration into a hard build failure: a project with
  `BoostOpsProjectSettings.microsoftStoreId` populated but no SDK Contracts
  NuGet installed gets aborted at build start with an actionable error
  message. Bare-.exe builds with no `microsoftStoreId` are left alone.

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

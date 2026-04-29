using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BoostOps.Analytics
{
    /// <summary>
    /// Common envelope shared by every outbound BoostOps payload (events and
    /// purchases alike). Carries the fields that *every* request needs:
    /// schema/event metadata, the four-tier identifier hierarchy, runtime
    /// routing flags (editor / debug / testflight / emulator), the privacy
    /// consent block, and the device/platform context block.
    ///
    /// The events endpoint and the purchases endpoint diverge only in the
    /// endpoint-specific fields they append on top of this envelope, so this
    /// type is the single source of truth for the shape of those common
    /// fields and there is no drift between endpoints.
    /// </summary>
    [Serializable]
    public class BoostOpsCommonPayload
    {
        // Event metadata
        public int SchemaVersion;
        public long TimestampMs;
        public long? ElapsedRealtimeMs;
        public string EventId;

        // Per-attempt replay token. Set fresh by the transport layer for each
        // network attempt. Builders leave this null; serializers accept an
        // override if the caller wants to bake one in (e.g. when re-sending
        // from a persisted queue file).
        public string Nonce;

        // Four-tier identifier hierarchy (top-level on the wire, schema v6+).
        public string BoostOpsId;
        public string InstallId;
        public long? InstallTimeMs;
        public string CustomUserId;
        public string SessionId;

        // Routing flags (Cloudflare edge uses these to fan out to the right
        // bronze table: prod vs editor vs debug vs testflight vs emulator).
        public bool IsUnityEditor;
        public bool IsDebugBuild;
        public bool IsTestFlight;
        public bool IsEmulator;

        // Privacy consent (top-level for compliance auditing).
        public ConsentData Consent;

        // Device/platform/locale/store context.
        public EventContext Context;
    }

    /// <summary>
    /// Builds a fully-populated <see cref="BoostOpsCommonPayload"/> from the
    /// SDK's identifier manager, environment detection, and device info.
    /// Both <see cref="BoostOpsEventBuilder.CreateEvent"/> and the dedicated
    /// purchases pipeline call into this so the two endpoints can never drift
    /// in what they collect.
    /// </summary>
    public static class BoostOpsCommonPayloadBuilder
    {
        /// <summary>
        /// Build a populated common payload.
        /// </summary>
        /// <param name="includeInstallTimestamp">
        /// When true, populates <see cref="BoostOpsCommonPayload.InstallTimeMs"/>
        /// from the OS-provided app install timestamp. Used for first-open events
        /// and SDK-migration detection.
        /// </param>
        /// <param name="includeInstallTimeExtras">
        /// When true, asks the identifier manager to include install-time-only
        /// identifiers (ASA token, install referrer click ID, etc.). Most calls
        /// don't need this; install/first-open paths do.
        /// </param>
        public static BoostOpsCommonPayload Build(
            bool includeInstallTimestamp = false,
            bool includeInstallTimeExtras = false)
        {
            var identifiers = BoostOpsIdentifierManager.CreateIdentifierPayload(includeInstallTimeExtras);

            var payload = new BoostOpsCommonPayload
            {
                SchemaVersion     = 7,
                TimestampMs       = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ElapsedRealtimeMs = BoostOpsDeviceInfo.GetElapsedRealtimeMilliseconds(),
                EventId           = Guid.NewGuid().ToString("N"),

                BoostOpsId        = GetIdentifierValue(identifiers, "boostops_id"),
                InstallId         = GetIdentifierValue(identifiers, "install_id"),
                CustomUserId      = GetIdentifierValue(identifiers, "custom_user_id"),
                SessionId         = GetIdentifierValue(identifiers, "session_id"),

                IsUnityEditor     = Application.isEditor,
                IsDebugBuild      = BoostOps.BoostOpsEnvironment.IsDebugBuild(),
                IsTestFlight      = BoostOps.BoostOpsEnvironment.IsTestFlight(),
                IsEmulator        = BoostOps.BoostOpsEnvironment.IsEmulator(),

                Consent           = BoostOpsEventBuilder.CreateConsentData(),
                Context           = BoostOpsEventBuilder.CreateEventContext(identifiers),
            };

            if (includeInstallTimestamp)
            {
                long installSeconds = BoostOpsDeviceInfo.GetAppInstallTimestamp();
                if (installSeconds > 0)
                {
                    payload.InstallTimeMs = installSeconds * 1000L;
                }
            }

            // install_id is critical for revenue attribution. The dictionary
            // path *should* always have it, but we mirror the recovery the
            // event builder used to do inline so the purchase path is just
            // as defensive.
            if (string.IsNullOrEmpty(payload.InstallId))
            {
                Debug.LogWarning("[BoostOps] Common payload had empty install_id from identifier dict; attempting direct fetch...");
                payload.InstallId = BoostOpsIdentifierManager.GetInstallId();
                if (string.IsNullOrEmpty(payload.InstallId))
                {
                    Debug.LogError("[BoostOps] Could not recover install_id; outbound payload will be missing it.");
                }
            }

            return payload;
        }

        private static string GetIdentifierValue(Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out var v) && v != null)
            {
                return v.ToString();
            }
            return null;
        }
    }

    /// <summary>
    /// Compact JSON serializer for the common envelope. Both the events
    /// transport and the purchases transport call into these helpers so the
    /// wire shape of the shared fields stays in lockstep.
    ///
    /// All append helpers leave a trailing comma; the caller is responsible
    /// for wrapping the result in <c>{...}</c> and trimming the final comma.
    /// Optional fields are omitted when null/empty so we keep payloads close
    /// to the 64KB request cap.
    /// </summary>
    public static class BoostOpsCommonPayloadJson
    {
        /// <summary>
        /// Append the common envelope's fields (top-level metadata,
        /// identifiers, routing flags, <c>consent</c>, <c>context</c>) to the
        /// builder. Caller appends endpoint-specific fields after.
        /// </summary>
        /// <param name="sb">Target builder. Should already contain the opening <c>{</c>.</param>
        /// <param name="p">Common payload. No-op if null.</param>
        /// <param name="nonceOverride">
        /// If set, overrides <see cref="BoostOpsCommonPayload.Nonce"/>. Used by
        /// transports that regenerate the nonce per attempt for replay
        /// protection.
        /// </param>
        public static void AppendEnvelope(StringBuilder sb, BoostOpsCommonPayload p, string nonceOverride = null)
        {
            if (p == null) return;

            AppendInt(sb, "schema_version", p.SchemaVersion);
            AppendLong(sb, "timestamp_ms", p.TimestampMs);
            if (p.ElapsedRealtimeMs.HasValue)
            {
                AppendLong(sb, "elapsed_realtime_ms", p.ElapsedRealtimeMs.Value);
            }
            if (!string.IsNullOrEmpty(p.EventId))
            {
                AppendString(sb, "event_id", p.EventId);
            }

            string nonce = !string.IsNullOrEmpty(nonceOverride) ? nonceOverride : p.Nonce;
            if (!string.IsNullOrEmpty(nonce))
            {
                AppendString(sb, "nonce", nonce);
            }

            // Four-tier identifier hierarchy
            if (!string.IsNullOrEmpty(p.BoostOpsId))
            {
                AppendString(sb, "boostops_id", p.BoostOpsId);
            }
            if (!string.IsNullOrEmpty(p.InstallId))
            {
                AppendString(sb, "install_id", p.InstallId);
            }
            if (p.InstallTimeMs.HasValue && p.InstallTimeMs.Value > 0)
            {
                AppendLong(sb, "install_time_ms", p.InstallTimeMs.Value);
            }
            if (!string.IsNullOrEmpty(p.CustomUserId))
            {
                AppendString(sb, "custom_user_id", p.CustomUserId);
            }
            if (!string.IsNullOrEmpty(p.SessionId))
            {
                AppendString(sb, "session_id", p.SessionId);
            }

            // Routing flags (only emitted when true; absence == false)
            if (p.IsUnityEditor) AppendBool(sb, "is_unity_editor", true);
            if (p.IsDebugBuild)  AppendBool(sb, "is_debug_build",  true);
            if (p.IsTestFlight)  AppendBool(sb, "is_testflight",   true);
            if (p.IsEmulator)    AppendBool(sb, "is_emulator",     true);

            if (p.Consent != null)
            {
                string consentBody = BuildConsentBody(p.Consent);
                if (!string.IsNullOrEmpty(consentBody))
                {
                    sb.Append("\"consent\":{").Append(consentBody).Append("},");
                }
            }

            if (p.Context != null)
            {
                string contextBody = BuildContextBody(p.Context);
                if (!string.IsNullOrEmpty(contextBody))
                {
                    sb.Append("\"context\":{").Append(contextBody).Append("},");
                }
            }
        }

        /// <summary>
        /// Build the inner body of a <c>"context":{...}</c> object (no braces).
        /// </summary>
        public static string BuildContextBody(EventContext c)
        {
            if (c == null) return string.Empty;
            var parts = new List<string>(32);

            if (!string.IsNullOrEmpty(c.source))             parts.Add(StrField("source", c.source));
            // Wire field is `os` (not `platform`) — matches what the events
            // endpoint has been receiving since schema v5.
            if (!string.IsNullOrEmpty(c.platform))           parts.Add(StrField("os", c.platform));
            if (!string.IsNullOrEmpty(c.os_version))         parts.Add(StrField("os_version", c.os_version));
            if (!string.IsNullOrEmpty(c.app_version))        parts.Add(StrField("app_version", c.app_version));
            if (!string.IsNullOrEmpty(c.app_identifier))     parts.Add(StrField("app_identifier", c.app_identifier));
            if (!string.IsNullOrEmpty(c.sdk_version))        parts.Add(StrField("sdk_version", c.sdk_version));
            if (!string.IsNullOrEmpty(c.store))              parts.Add(StrField("store", c.store));
            if (!string.IsNullOrEmpty(c.store_id))           parts.Add(StrField("store_id", c.store_id));
            if (!string.IsNullOrEmpty(c.device_model))       parts.Add(StrField("device_model", c.device_model));
            if (!string.IsNullOrEmpty(c.device_brand))       parts.Add(StrField("device_brand", c.device_brand));
            if (!string.IsNullOrEmpty(c.country))            parts.Add(StrField("country", c.country));
            if (!string.IsNullOrEmpty(c.storefront_country)) parts.Add(StrField("storefront_country", c.storefront_country));
            if (!string.IsNullOrEmpty(c.region))             parts.Add(StrField("region", c.region));
            if (!string.IsNullOrEmpty(c.city))               parts.Add(StrField("city", c.city));
            if (c.timezone_offset_minutes.HasValue)
            {
                parts.Add($"\"timezone_offset_minutes\":{c.timezone_offset_minutes.Value}");
            }
            if (!string.IsNullOrEmpty(c.locale))             parts.Add(StrField("locale", c.locale));
            if (!string.IsNullOrEmpty(c.language))           parts.Add(StrField("language", c.language));
            if (!string.IsNullOrEmpty(c.carrier))            parts.Add(StrField("carrier", c.carrier));
            if (!string.IsNullOrEmpty(c.connection_type))    parts.Add(StrField("connection_type", c.connection_type));
            if (!string.IsNullOrEmpty(c.ip_address))         parts.Add(StrField("ip_address", c.ip_address));

            // Cross-app correlation IDs (install_id and custom_user_id are at
            // top-level since schema v6, so they don't appear here).
            if (!string.IsNullOrEmpty(c.app_account_token))    parts.Add(StrField("app_account_token", c.app_account_token));
            if (!string.IsNullOrEmpty(c.idfv))                 parts.Add(StrField("idfv", c.idfv));
            if (!string.IsNullOrEmpty(c.idfa))                 parts.Add(StrField("idfa", c.idfa));
            if (!string.IsNullOrEmpty(c.asid_sha256))          parts.Add(StrField("asid_sha256", c.asid_sha256));
            if (!string.IsNullOrEmpty(c.gaid))                 parts.Add(StrField("gaid", c.gaid));
            if (!string.IsNullOrEmpty(c.firebase_app_id))      parts.Add(StrField("firebase_app_id", c.firebase_app_id));
            if (!string.IsNullOrEmpty(c.windows_device_id))    parts.Add(StrField("windows_device_id", c.windows_device_id));
            if (!string.IsNullOrEmpty(c.windows_machine_guid)) parts.Add(StrField("windows_machine_guid", c.windows_machine_guid));
            if (!string.IsNullOrEmpty(c.msaid))                parts.Add(StrField("msaid", c.msaid));

            if (!string.IsNullOrEmpty(c.environment))      parts.Add(StrField("environment", c.environment));
            if (!string.IsNullOrEmpty(c.installer_source)) parts.Add(StrField("installer_source", c.installer_source));

            return string.Join(",", parts);
        }

        /// <summary>
        /// Build the inner body of a <c>"consent":{...}</c> object (no braces).
        /// </summary>
        public static string BuildConsentBody(ConsentData c)
        {
            if (c == null) return string.Empty;
            var parts = new List<string>(16);

            if (!string.IsNullOrEmpty(c.framework))
            {
                parts.Add(StrField("framework", c.framework));
            }
            if (c.gdpr_consent_required.HasValue)
            {
                parts.Add($"\"gdpr_required\":{BoolLit(c.gdpr_consent_required.Value)}");
            }
            if (c.ccpa_consent_required.HasValue)
            {
                parts.Add($"\"ccpa_required\":{BoolLit(c.ccpa_consent_required.Value)}");
            }

            if (c.consent_timestamp.HasValue)
            {
                parts.Add($"\"timestamp\":{c.consent_timestamp.Value}");
            }
            if (!string.IsNullOrEmpty(c.consent_version))  parts.Add(StrField("version", c.consent_version));
            if (!string.IsNullOrEmpty(c.consent_language)) parts.Add(StrField("language", c.consent_language));
            if (!string.IsNullOrEmpty(c.consent_method))   parts.Add(StrField("method", c.consent_method));
            if (!string.IsNullOrEmpty(c.consent_source))   parts.Add(StrField("source", c.consent_source));
            if (!string.IsNullOrEmpty(c.legal_basis))      parts.Add(StrField("legal_basis", c.legal_basis));

            if (!string.IsNullOrEmpty(c.consent_string))
            {
                parts.Add(StrField("consent_string", c.consent_string));
            }

            if (c.gdpr != null)
            {
                var gdpr = new List<string>(6);
                if (c.gdpr.applies.HasValue)        gdpr.Add($"\"applies\":{BoolLit(c.gdpr.applies.Value)}");
                if (c.gdpr.consent_given.HasValue)  gdpr.Add($"\"consent_given\":{BoolLit(c.gdpr.consent_given.Value)}");
                if (c.gdpr.analytics.HasValue)      gdpr.Add($"\"analytics\":{BoolLit(c.gdpr.analytics.Value)}");
                if (c.gdpr.advertising.HasValue)    gdpr.Add($"\"advertising\":{BoolLit(c.gdpr.advertising.Value)}");
                if (c.gdpr.measurement.HasValue)    gdpr.Add($"\"measurement\":{BoolLit(c.gdpr.measurement.Value)}");
                if (!string.IsNullOrEmpty(c.gdpr.legal_basis)) gdpr.Add(StrField("legal_basis", c.gdpr.legal_basis));
                if (gdpr.Count > 0) parts.Add($"\"gdpr\":{{{string.Join(",", gdpr)}}}");
            }

            if (c.att != null)
            {
                var att = new List<string>(3);
                if (!string.IsNullOrEmpty(c.att.status)) att.Add(StrField("status", c.att.status));
                if (c.att.authorized_time.HasValue)      att.Add($"\"authorized_time\":{c.att.authorized_time.Value}");
                if (c.att.idfa_available.HasValue)       att.Add($"\"idfa_available\":{BoolLit(c.att.idfa_available.Value)}");
                if (att.Count > 0) parts.Add($"\"att\":{{{string.Join(",", att)}}}");
            }

            if (c.android != null)
            {
                var droid = new List<string>(2);
                if (c.android.advertising_id.HasValue)     droid.Add($"\"advertising_id\":{BoolLit(c.android.advertising_id.Value)}");
                if (c.android.limited_ad_tracking.HasValue) droid.Add($"\"limited_ad_tracking\":{BoolLit(c.android.limited_ad_tracking.Value)}");
                if (droid.Count > 0) parts.Add($"\"android\":{{{string.Join(",", droid)}}}");
            }

            if (c.withdrawal_timestamp.HasValue)
            {
                parts.Add($"\"withdrawal_timestamp\":{c.withdrawal_timestamp.Value}");
            }
            if (!string.IsNullOrEmpty(c.withdrawal_method))
            {
                parts.Add(StrField("withdrawal_method", c.withdrawal_method));
            }

            return string.Join(",", parts);
        }

        // -------- Internal append helpers --------

        internal static void AppendString(StringBuilder sb, string key, string value)
        {
            if (value == null) return;
            sb.Append('"').Append(key).Append("\":\"");
            JsonEscape(sb, value);
            sb.Append("\",");
        }

        internal static void AppendInt(StringBuilder sb, string key, int value)
        {
            sb.Append('"').Append(key).Append("\":").Append(value).Append(',');
        }

        internal static void AppendLong(StringBuilder sb, string key, long value)
        {
            sb.Append('"').Append(key).Append("\":").Append(value).Append(',');
        }

        internal static void AppendBool(StringBuilder sb, string key, bool value)
        {
            sb.Append('"').Append(key).Append("\":").Append(BoolLit(value)).Append(',');
        }

        internal static string StrField(string key, string value)
        {
            // For when the caller is using a List<string>+Join pattern instead
            // of a StringBuilder. Returns "key":"value" with the value JSON-escaped.
            var sb = new StringBuilder(key.Length + (value?.Length ?? 0) + 6);
            sb.Append('"').Append(key).Append("\":\"");
            if (!string.IsNullOrEmpty(value)) JsonEscape(sb, value);
            sb.Append('"');
            return sb.ToString();
        }

        internal static string BoolLit(bool b) => b ? "true" : "false";

        internal static void JsonEscape(StringBuilder sb, string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            foreach (var c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
        }
    }
}

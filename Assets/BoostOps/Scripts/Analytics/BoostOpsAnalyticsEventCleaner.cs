using System;
using System.Linq;
using UnityEngine;

namespace BoostOps.Analytics
{
    /// <summary>
    /// Utility class to clean analytics event data by removing empty strings, null values, and empty objects
    /// </summary>
    public static class BoostOpsAnalyticsEventCleaner
    {
        /// <summary>
        /// Clean event data by removing empty strings, null values, and empty objects
        /// </summary>
        public static AnalyticsEventData CleanEventData(AnalyticsEventData eventData)
        {
            if (eventData == null) return null;
            
            var cleaned = new AnalyticsEventData
            {
                // Top-level metadata (always include)
                event_type = eventData.event_type,
                schema_version = eventData.schema_version,
                timestamp_ms = eventData.timestamp_ms
            };
            
            // Deduplication identifiers
            if (!string.IsNullOrEmpty(eventData.event_id)) cleaned.event_id = eventData.event_id;  // Database UNIQUE INDEX (never changes)
            if (!string.IsNullOrEmpty(eventData.nonce)) cleaned.nonce = eventData.nonce;           // Network replay prevention (fresh per attempt)
            
            // Universal correlation identifiers (four-tier ID hierarchy)
            if (!string.IsNullOrEmpty(eventData.boostops_id)) cleaned.boostops_id = eventData.boostops_id;
            if (!string.IsNullOrEmpty(eventData.install_id)) cleaned.install_id = eventData.install_id;
            if (eventData.install_time_ms.HasValue && eventData.install_time_ms.Value > 0) cleaned.install_time_ms = eventData.install_time_ms;
            if (!string.IsNullOrEmpty(eventData.custom_user_id)) cleaned.custom_user_id = eventData.custom_user_id;
            if (!string.IsNullOrEmpty(eventData.session_id)) cleaned.session_id = eventData.session_id;
            // Note: project_key is sent in HTTP header ONLY, never in payload for security
            // Note: storefront_country moved to context (environmental data)
            
            // Critical routing flags
            if (eventData.is_unity_editor.HasValue) cleaned.is_unity_editor = eventData.is_unity_editor;
            if (eventData.is_debug_build.HasValue) cleaned.is_debug_build = eventData.is_debug_build;
            if (eventData.is_testflight.HasValue) cleaned.is_testflight = eventData.is_testflight;
            if (eventData.is_emulator.HasValue) cleaned.is_emulator = eventData.is_emulator;
            
            // Privacy consent (includes ATT status on iOS)
            if (eventData.consent != null)
            {
                cleaned.consent = eventData.consent;  // Preserve full consent object including ATT
            }
            
            // Clean context
            if (eventData.context != null)
            {
                cleaned.context = CleanEventContext(eventData.context);
            }
            
            // Clean event data
            if (eventData.@event != null)
            {
                cleaned.@event = CleanEventDataFields(eventData.@event);
            }
            
            return cleaned;
        }
        
        /// <summary>
        /// Clean event context by removing empty fields
        /// </summary>
        private static EventContext CleanEventContext(EventContext context)
        {
            var cleaned = new EventContext();
            
            if (!string.IsNullOrEmpty(context.source)) cleaned.source = context.source;
            if (!string.IsNullOrEmpty(context.platform)) cleaned.platform = context.platform;
            if (!string.IsNullOrEmpty(context.os_version)) cleaned.os_version = context.os_version;
            if (!string.IsNullOrEmpty(context.app_version)) cleaned.app_version = context.app_version;
            if (!string.IsNullOrEmpty(context.app_identifier)) cleaned.app_identifier = context.app_identifier;
            if (!string.IsNullOrEmpty(context.sdk_version)) cleaned.sdk_version = context.sdk_version;
            if (!string.IsNullOrEmpty(context.store)) cleaned.store = context.store;
            if (!string.IsNullOrEmpty(context.store_id)) cleaned.store_id = context.store_id;
            if (!string.IsNullOrEmpty(context.device_model)) cleaned.device_model = context.device_model;
            if (!string.IsNullOrEmpty(context.device_brand)) cleaned.device_brand = context.device_brand;
            if (!string.IsNullOrEmpty(context.country)) cleaned.country = context.country;
            if (!string.IsNullOrEmpty(context.storefront_country)) cleaned.storefront_country = context.storefront_country;
            if (!string.IsNullOrEmpty(context.region)) cleaned.region = context.region;
            if (!string.IsNullOrEmpty(context.city)) cleaned.city = context.city;
            if (context.timezone_offset_minutes.HasValue) cleaned.timezone_offset_minutes = context.timezone_offset_minutes;
            if (!string.IsNullOrEmpty(context.locale)) cleaned.locale = context.locale;
            if (!string.IsNullOrEmpty(context.language)) cleaned.language = context.language;
            if (!string.IsNullOrEmpty(context.carrier)) cleaned.carrier = context.carrier;
            if (!string.IsNullOrEmpty(context.connection_type)) cleaned.connection_type = context.connection_type;
            if (!string.IsNullOrEmpty(context.ip_address)) cleaned.ip_address = context.ip_address;
            // Note: timestamp is at top-level (milliseconds precision) - not duplicated in context
            
            // Device identifiers (critical for attribution)
            // Note: install_id and custom_user_id moved to top-level (schema v6)
            if (!string.IsNullOrEmpty(context.app_account_token)) cleaned.app_account_token = context.app_account_token;
            if (!string.IsNullOrEmpty(context.idfv)) cleaned.idfv = context.idfv;
            if (!string.IsNullOrEmpty(context.idfa)) cleaned.idfa = context.idfa;  // iOS advertising ID (when ATT authorized)
            if (!string.IsNullOrEmpty(context.asid_sha256)) cleaned.asid_sha256 = context.asid_sha256;  // Android hashed ASID
            if (!string.IsNullOrEmpty(context.gaid)) cleaned.gaid = context.gaid;  // Google Advertising ID
            if (!string.IsNullOrEmpty(context.firebase_app_id)) cleaned.firebase_app_id = context.firebase_app_id;
            
            return cleaned;
        }
        
        /// <summary>
        /// Clean event data fields by removing empty values
        /// </summary>
        private static EventData CleanEventDataFields(EventData eventData)
        {
            var cleaned = new EventData();
            
            // Attribution & Identity
            // Note: boostops_id and session_id moved to top-level, no longer in event data
            if (!string.IsNullOrEmpty(eventData.user_id)) cleaned.user_id = eventData.user_id;
            
            // Cross-Promotion Attribution
            // Note: source_store_id is in context.store_id (universal) - not duplicated here
            // Note: source_project_id is derived server-side from project_key - not sent from SDK
            if (!string.IsNullOrEmpty(eventData.target_store_id)) cleaned.target_store_id = eventData.target_store_id;
            if (!string.IsNullOrEmpty(eventData.target_project_id)) cleaned.target_project_id = eventData.target_project_id;
            if (!string.IsNullOrEmpty(eventData.network_campaign_id)) cleaned.network_campaign_id = eventData.network_campaign_id;
            if (!string.IsNullOrEmpty(eventData.placement_id)) cleaned.placement_id = eventData.placement_id;
            
            // Campaign attribution
            if (eventData.campaign_id.HasValue) cleaned.campaign_id = eventData.campaign_id;
            if (!string.IsNullOrEmpty(eventData.campaign_slug)) cleaned.campaign_slug = eventData.campaign_slug;
            if (eventData.creative_id.HasValue) cleaned.creative_id = eventData.creative_id;
            if (!string.IsNullOrEmpty(eventData.keyword)) cleaned.keyword = eventData.keyword;
            
            // App context
            if (!string.IsNullOrEmpty(eventData.project_slug)) cleaned.project_slug = eventData.project_slug;
            
            // Revenue & Commerce
            if (!string.IsNullOrEmpty(eventData.currency)) cleaned.currency = eventData.currency;
            if (eventData.amount_micros.HasValue) cleaned.amount_micros = eventData.amount_micros;
            if (eventData.tax_micros.HasValue) cleaned.tax_micros = eventData.tax_micros;
            if (eventData.discount_micros.HasValue) cleaned.discount_micros = eventData.discount_micros;
            
            // Product details
            if (!string.IsNullOrEmpty(eventData.product_id)) cleaned.product_id = eventData.product_id;
            if (!string.IsNullOrEmpty(eventData.product_name)) cleaned.product_name = eventData.product_name;
            if (!string.IsNullOrEmpty(eventData.product_category)) cleaned.product_category = eventData.product_category;
            if (eventData.quantity.HasValue) cleaned.quantity = eventData.quantity;
            if (!string.IsNullOrEmpty(eventData.transaction_id)) cleaned.transaction_id = eventData.transaction_id;
            if (!string.IsNullOrEmpty(eventData.receipt)) cleaned.receipt = eventData.receipt;
            
            // Commerce context
            if (eventData.is_trial.HasValue) cleaned.is_trial = eventData.is_trial;
            if (eventData.is_subscription.HasValue) cleaned.is_subscription = eventData.is_subscription;
            if (!string.IsNullOrEmpty(eventData.billing_period)) cleaned.billing_period = eventData.billing_period;
            if (eventData.renewal_number.HasValue) cleaned.renewal_number = eventData.renewal_number;
            
            // Cross-promotion specific
            if (!string.IsNullOrEmpty(eventData.impression_id)) cleaned.impression_id = eventData.impression_id;
            if (!string.IsNullOrEmpty(eventData.format)) cleaned.format = eventData.format;
            if (eventData.duration_ms.HasValue) cleaned.duration_ms = eventData.duration_ms;
            if (eventData.viewable.HasValue) cleaned.viewable = eventData.viewable;
            if (eventData.above_fold.HasValue) cleaned.above_fold = eventData.above_fold;
            if (eventData.completion_rate.HasValue) cleaned.completion_rate = eventData.completion_rate;
            
            // Click specific
            if (eventData.impression_timestamp.HasValue) cleaned.impression_timestamp = eventData.impression_timestamp;
            if (eventData.click_coordinates != null && (eventData.click_coordinates.x != 0 || eventData.click_coordinates.y != 0))
                cleaned.click_coordinates = eventData.click_coordinates;
            if (eventData.time_to_click_ms.HasValue) cleaned.time_to_click_ms = eventData.time_to_click_ms;
            if (!string.IsNullOrEmpty(eventData.referrer)) cleaned.referrer = eventData.referrer;
            if (eventData.click_through_rate.HasValue) cleaned.click_through_rate = eventData.click_through_rate;
            
            // Cross-App Navigation
            if (!string.IsNullOrEmpty(eventData.deep_link_url)) cleaned.deep_link_url = eventData.deep_link_url;
            if (!string.IsNullOrEmpty(eventData.redirect_url)) cleaned.redirect_url = eventData.redirect_url;
            if (eventData.store_redirect.HasValue) cleaned.store_redirect = eventData.store_redirect;
            if (eventData.attribution_window_hours.HasValue) cleaned.attribution_window_hours = eventData.attribution_window_hours;
            
            // Revenue Context
            if (eventData.revenue_share_rate.HasValue) cleaned.revenue_share_rate = eventData.revenue_share_rate;
            if (eventData.estimated_cpm_micros.HasValue) cleaned.estimated_cpm_micros = eventData.estimated_cpm_micros;
            if (eventData.impression_value_micros.HasValue) cleaned.impression_value_micros = eventData.impression_value_micros;
            if (eventData.click_value_micros.HasValue) cleaned.click_value_micros = eventData.click_value_micros;
            
            // Install specific
            if (eventData.organic.HasValue) cleaned.organic = eventData.organic;
            if (eventData.reinstall.HasValue) cleaned.reinstall = eventData.reinstall;
            if (eventData.install_size_bytes.HasValue) cleaned.install_size_bytes = eventData.install_size_bytes;
            if (eventData.install_duration_ms.HasValue) cleaned.install_duration_ms = eventData.install_duration_ms;
            
            // App open specific
            if (!string.IsNullOrEmpty(eventData.launch_type)) cleaned.launch_type = eventData.launch_type;
            if (!string.IsNullOrEmpty(eventData.deep_link_url)) cleaned.deep_link_url = eventData.deep_link_url;
            if (eventData.time_since_install_ms.HasValue) cleaned.time_since_install_ms = eventData.time_since_install_ms;
            
            // Device identification fields (app open events)
            // Note: network_type is in context.connection_type (universal) - not duplicated here
            // Note: country is in context.country (universal) - not duplicated here
            // Note: locale is in context.locale (universal) - not duplicated here
            // Note: language is in context.language (universal) - not duplicated here
            // Note: timezone_offset_minutes is in context (universal) - not duplicated here
            if (eventData.screen_width.HasValue) cleaned.screen_width = eventData.screen_width;
            if (eventData.screen_height.HasValue) cleaned.screen_height = eventData.screen_height;
            if (!string.IsNullOrEmpty(eventData.device_orientation)) cleaned.device_orientation = eventData.device_orientation;
            
            // Attribution update specific
            if (!string.IsNullOrEmpty(eventData.attribution_source)) cleaned.attribution_source = eventData.attribution_source;
            if (!string.IsNullOrEmpty(eventData.attribution_method)) cleaned.attribution_method = eventData.attribution_method;
            if (eventData.attribution_confidence.HasValue) cleaned.attribution_confidence = eventData.attribution_confidence;
            
            // Install-time identifiers (first_open events only)
            if (!string.IsNullOrEmpty(eventData.asa_token)) cleaned.asa_token = eventData.asa_token;
            if (!string.IsNullOrEmpty(eventData.skan_source_id)) cleaned.skan_source_id = eventData.skan_source_id;
            if (!string.IsNullOrEmpty(eventData.install_referrer_click_id)) cleaned.install_referrer_click_id = eventData.install_referrer_click_id;
            if (!string.IsNullOrEmpty(eventData.attribution_click_id)) cleaned.attribution_click_id = eventData.attribution_click_id;
            
            // Purchase specific - removed user_ltv_micros as it doesn't exist in schema
            
            // Device identifiers (only if not empty)
            if (!string.IsNullOrEmpty(eventData.idfa_hash)) cleaned.idfa_hash = eventData.idfa_hash;
            if (!string.IsNullOrEmpty(eventData.idfv_hash)) cleaned.idfv_hash = eventData.idfv_hash;
            if (!string.IsNullOrEmpty(eventData.gaid_hash)) cleaned.gaid_hash = eventData.gaid_hash;
            if (!string.IsNullOrEmpty(eventData.android_id_hash)) cleaned.android_id_hash = eventData.android_id_hash;
            if (!string.IsNullOrEmpty(eventData.custom_user_id)) cleaned.custom_user_id = eventData.custom_user_id;
            if (!string.IsNullOrEmpty(eventData.fingerprint_hash)) cleaned.fingerprint_hash = eventData.fingerprint_hash;
            
            // Clean nested objects only if they have meaningful content
            // Note: consent is now handled at the top-level event data
            cleaned.apple_search_ads = CleanAppleSearchAdsData(eventData.apple_search_ads);
            cleaned.skan = CleanSKANData(eventData.skan);
            cleaned.aak = CleanAAKData(eventData.aak);
            cleaned.play_install_referrer = CleanPlayInstallReferrerData(eventData.play_install_referrer);
            cleaned.google_ads = CleanGoogleAdsData(eventData.google_ads);
            cleaned.custom_data = CleanCustomData(eventData.custom_data);
            
            return cleaned;
        }
        
        /// <summary>
        /// Clean consent data, return null if empty
        /// </summary>
        private static ConsentData CleanConsentData(ConsentData consent)
        {
            if (consent == null) return null;
            
            var hasContent = !string.IsNullOrEmpty(consent.framework) ||
                           !string.IsNullOrEmpty(consent.consent_method) ||
                           !string.IsNullOrEmpty(consent.consent_string) ||
                           (consent.gdpr != null && !string.IsNullOrEmpty(consent.gdpr.legal_basis)) ||
                           (consent.att != null && !string.IsNullOrEmpty(consent.att.status));
                           
            return hasContent ? consent : null;
        }
        
        /// <summary>
        /// Clean Apple Search Ads data, return null if empty
        /// </summary>
        private static AppleAttributionData CleanAppleSearchAdsData(AppleAttributionData data)
        {
            if (data == null) return null;
            return !string.IsNullOrEmpty(data.token) ? data : null;
        }
        
        /// <summary>
        /// Clean SKAN data, return null if empty
        /// </summary>
        private static SKANData CleanSKANData(SKANData data)
        {
            if (data == null) return null;
            
            var hasContent = !string.IsNullOrEmpty(data.version) ||
                           !string.IsNullOrEmpty(data.coarse_value) ||
                           !string.IsNullOrEmpty(data.source_identifier) ||
                           !string.IsNullOrEmpty(data.attribution_signature);
                           
            return hasContent ? data : null;
        }
        
        /// <summary>
        /// Clean AAK data, return null if empty
        /// </summary>
        private static AAKData CleanAAKData(AAKData data)
        {
            if (data == null) return null;
            
            var hasContent = !string.IsNullOrEmpty(data.conversion_type) ||
                           !string.IsNullOrEmpty(data.marketplace_identifier);
                           
            return hasContent ? data : null;
        }
        
        /// <summary>
        /// Clean Play Install Referrer data, return null if empty
        /// </summary>
        private static PlayInstallReferrerData CleanPlayInstallReferrerData(PlayInstallReferrerData data)
        {
            if (data == null) return null;
            return !string.IsNullOrEmpty(data.referrer) ? data : null;
        }
        
        /// <summary>
        /// Clean Google Ads data, return null if empty
        /// </summary>
        private static GoogleAdsData CleanGoogleAdsData(GoogleAdsData data)
        {
            if (data == null) return null;
            
            var hasContent = !string.IsNullOrEmpty(data.campaign_id) ||
                           !string.IsNullOrEmpty(data.ad_group_id) ||
                           !string.IsNullOrEmpty(data.creative_id) ||
                           !string.IsNullOrEmpty(data.keyword) ||
                           !string.IsNullOrEmpty(data.match_type);
                           
            return hasContent ? data : null;
        }
        
        /// <summary>
        /// Clean custom data, return null if empty
        /// </summary>
        private static CustomData CleanCustomData(CustomData data)
        {
            if (data == null) return null;
            
            var hasContent = false;
            
            // Check game data
            if (data.game != null)
            {
                var gameHasContent = data.game.level.HasValue ||
                                  !string.IsNullOrEmpty(data.game.character) ||
                                  !string.IsNullOrEmpty(data.game.guild_id);
                if (!gameHasContent) data.game = null;
                else hasContent = true;
            }
            
            // Check ecommerce data  
            if (data.ecommerce != null)
            {
                var ecommerceHasContent = !string.IsNullOrEmpty(data.ecommerce.cart_id) ||
                                        !string.IsNullOrEmpty(data.ecommerce.shipping_method) ||
                                        !string.IsNullOrEmpty(data.ecommerce.payment_method) ||
                                        !string.IsNullOrEmpty(data.ecommerce.coupon_code) ||
                                        !string.IsNullOrEmpty(data.ecommerce.category) ||
                                        !string.IsNullOrEmpty(data.ecommerce.brand) ||
                                        !string.IsNullOrEmpty(data.ecommerce.variant);
                if (!ecommerceHasContent) data.ecommerce = null;
                else hasContent = true;
            }
            
            // Check SaaS data
            if (data.saas != null)
            {
                var saasHasContent = !string.IsNullOrEmpty(data.saas.plan_tier) ||
                                   !string.IsNullOrEmpty(data.saas.billing_cycle);
                if (!saasHasContent) data.saas = null;
                else hasContent = true;
            }
            
            return hasContent ? data : null;
        }
    }
}
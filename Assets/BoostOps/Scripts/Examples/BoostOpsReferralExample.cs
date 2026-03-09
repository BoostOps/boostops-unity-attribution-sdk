using UnityEngine;
using System.Collections.Generic;

namespace BoostOps.Examples
{
    /// <summary>
    /// Example demonstrating how to use BoostOps referral URLs for social sharing and invites
    /// </summary>
    public class BoostOpsReferralExample : MonoBehaviour
    {
        [Header("Example Settings")]
        [Tooltip("Enable to see detailed console logs")]
        public bool enableDebugLogs = true;

        void Start()
        {
            if (enableDebugLogs)
            {
                Debug.Log("[BoostOps Referral Example] Starting examples...");
            }
        }

        // =================================================================
        // EXAMPLE 1: Basic Social Share
        // =================================================================
        
        /// <summary>
        /// Simple social share - just get the URL and share it
        /// </summary>
        public void Example1_BasicShare()
        {
            // Get your referral code for display
            string myCode = BoostOpsSDK.GetReferralCode();
            Debug.Log($"[Example 1] My referral code: {myCode}");
            
            // Get your app's BoostLink URL (includes your code)
            string shareUrl = BoostOpsSDK.GetReferralUrl();
            
            if (string.IsNullOrEmpty(shareUrl))
            {
                Debug.LogError("[Example 1] Referral URL not available. Check project slug configuration.");
                return;
            }
            
            Debug.Log($"[Example 1] Share URL: {shareUrl}");
            
            // Compose message
            string message = $"Join me in this awesome game! Use my code {myCode} or click: {shareUrl}";
            
            // Copy to clipboard (works on all platforms)
            GUIUtility.systemCopyBuffer = shareUrl;
            Debug.Log($"[Example 1] Share URL copied to clipboard: {message}");
            
            // In a real app, you'd use native sharing:
            // - iOS: UIActivityViewController
            // - Android: Intent.ACTION_SEND
            // - Or use a Unity plugin like: https://github.com/yasirkula/UnityNativeShare
        }

        // =================================================================
        // EXAMPLE 2: Track Share Events
        // =================================================================
        
        /// <summary>
        /// Track when users share (analytics only, source not in URL)
        /// </summary>
        public void Example2_TrackShareEvent(string source)
        {
            // Get referral URL (same for all shares)
            string shareUrl = BoostOpsSDK.GetReferralUrl();
            
            Debug.Log($"[Example 2] Share URL: {shareUrl}");
            
            // Track the share event in analytics
            BoostOpsSDK.TrackConversionEvent("social_share_initiated", new Dictionary<string, object>
            {
                ["source"] = source,
                ["url"] = shareUrl,
                ["timestamp"] = System.DateTime.UtcNow.ToString("o")
            });
            
            // Share the URL
            ShareToSocial(shareUrl, $"Check out this game! {shareUrl}");
        }

        // =================================================================
        // EXAMPLE 3: Friend Invite with Reward
        // =================================================================
        
        /// <summary>
        /// Invite friends and reward the sharer
        /// </summary>
        public void Example3_InviteFriendsWithReward()
        {
            // Get invite URL
            string inviteUrl = BoostOpsSDK.GetReferralUrl();
            
            Debug.Log($"[Example 3] Invite URL: {inviteUrl}");
            
            // Reward the user for sharing (instant gratification)
            int rewardCoins = 10;
            GivePlayerReward(rewardCoins);
            
            // Show thank you message
            Debug.Log($"[Example 3] Thanks for inviting friends! +{rewardCoins} coins");
            
            // Share with enticing message
            string message = $"I'm playing this awesome game! Join me and we both get bonus coins: {inviteUrl}";
            ShareToSocial(inviteUrl, message);
        }

        // =================================================================
        // EXAMPLE 4: Post-Level Share
        // =================================================================
        
        /// <summary>
        /// Share after completing a level with the score
        /// </summary>
        public void Example4_ShareLevelComplete(int level, int score)
        {
            // Get referral URL
            string shareUrl = BoostOpsSDK.GetReferralUrl();
            
            Debug.Log($"[Example 4] Level complete share URL: {shareUrl}");
            
            // Create context-aware message
            string message = $"I just scored {score:N0} on level {level}! Can you beat me? {shareUrl}";
            
            // Share it
            ShareToSocial(shareUrl, message);
            
            // Track the share
            BoostOpsSDK.TrackConversionEvent("level_complete_share", new Dictionary<string, object>
            {
                ["level"] = level,
                ["score"] = score,
                ["url"] = shareUrl
            });
        }

        // =================================================================
        // EXAMPLE 5: Multiple Share Channels
        // =================================================================
        
        /// <summary>
        /// Offer multiple share destinations
        /// </summary>
        public void Example5_ShowShareOptions()
        {
            Debug.Log("[Example 5] Showing share options...");
            
            // Get base URL
            string baseUrl = BoostOpsSDK.GetReferralUrl();
            
            if (string.IsNullOrEmpty(baseUrl))
            {
                Debug.LogError("[Example 5] No referral URL available");
                return;
            }
            
            // Show options for different platforms
            // In a real app, this would be a UI popup with buttons
            
            Debug.Log($"[Example 5] Share to Facebook: {baseUrl}");
            Debug.Log($"[Example 5] Share to Twitter: {baseUrl}");
            Debug.Log($"[Example 5] Share to WhatsApp: {baseUrl}");
            Debug.Log($"[Example 5] Share to Instagram: {baseUrl}");
            Debug.Log($"[Example 5] Copy Link: {baseUrl}");
        }

        // =================================================================
        // EXAMPLE 6: Future - User Referral Codes (v2.0)
        // =================================================================
        
        /// <summary>
        /// Per-user referral codes (now working!)
        /// Each user gets a unique code automatically
        /// </summary>
        public void Example6_UserReferralCode()
        {
            // Get user's unique referral code (generated automatically)
            string myCode = BoostOpsSDK.GetReferralCode();
            Debug.Log($"[Example 6] My referral code: {myCode}");
            
            // Get referral URL (includes code automatically)
            string referralUrl = BoostOpsSDK.GetReferralUrl();
            Debug.Log($"[Example 6] User referral URL: {referralUrl}");
            
            // Code is stored locally and reused
            // Same code across all shares!
            
            // Share it everywhere:
            // - Twitter bio: "Use code {myCode}"
            // - Twitch overlay: "Code: {myCode}"
            // - Discord: "Join with {myCode}"
            
            // Track attribution:
            // When someone installs via your link,
            // BoostOps tracks: ref={myCode} → You referred this user
        }

        // =================================================================
        // EXAMPLE 7: Display Referral Code in UI
        // =================================================================
        
        /// <summary>
        /// Show how to display the referral code in your UI
        /// </summary>
        public void Example7_DisplayCodeInUI()
        {
            // Get user's referral code
            string myCode = BoostOpsSDK.GetReferralCode();
            
            Debug.Log($"[Example 7] Displaying referral code: {myCode}");
            
            // Example UI displays:
            Debug.Log($"[Example 7] Profile screen: Your code: {myCode}");
            Debug.Log($"[Example 7] Share screen: Share code {myCode} with friends!");
            Debug.Log($"[Example 7] Leaderboard: Player #{myCode}");
            
            // In your actual UI (e.g., Unity UI Text):
            // profileCodeText.text = $"Your Code: {myCode}";
            // shareCodeText.text = $"Share {myCode} with friends!";
            
            // With copy button:
            // copyButton.onClick.AddListener(() => {
            //     GUIUtility.systemCopyBuffer = myCode;
            //     ShowToast("Code copied!");
            // });
        }

        // =================================================================
        // Helper Methods
        // =================================================================
        
        void ShareToSocial(string url, string message)
        {
            // Placeholder for native sharing
            // In production, use a native share plugin or implement platform-specific sharing
            
            #if UNITY_EDITOR
                Debug.Log($"[Share] Would share: {message}");
                GUIUtility.systemCopyBuffer = url;
            #elif UNITY_IOS || UNITY_ANDROID
                // Use native share plugin here
                // Example: NativeShare.Share(message, url);
                Debug.Log($"[Share] Sharing on mobile: {message}");
            #else
                Debug.Log($"[Share] Clipboard copy: {url}");
                GUIUtility.systemCopyBuffer = url;
            #endif
        }
        
        void GivePlayerReward(int coins)
        {
            // Placeholder for your reward system
            Debug.Log($"[Reward] Giving player {coins} coins");
            
            // In your actual game:
            // PlayerData.AddCoins(coins);
            // ShowRewardNotification(coins);
        }
        
        string GetCurrentUserId()
        {
            // Placeholder - use your actual user ID system
            // Examples:
            // - PlayerPrefs.GetString("user_id")
            // - PlayFabManager.Instance.PlayFabId
            // - Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser.UserId
            // - Your own backend user ID
            
            return "USER_" + UnityEngine.Random.Range(1000, 9999).ToString();
        }

        // =================================================================
        // UI Button Examples (attach to Unity UI buttons)
        // =================================================================
        
        /// <summary>
        /// Example: Attach to a "Share" button in your UI
        /// </summary>
        public void OnShareButtonClicked()
        {
            Example2_TrackShareEvent("share_button");
        }
        
        /// <summary>
        /// Example: Attach to an "Invite Friends" button
        /// </summary>
        public void OnInviteFriendsClicked()
        {
            Example3_InviteFriendsWithReward();
        }
        
        /// <summary>
        /// Example: Call after level complete
        /// </summary>
        public void OnLevelCompleteShare()
        {
            // Get current level and score from your game state
            int currentLevel = 5; // Replace with actual level
            int currentScore = 12500; // Replace with actual score
            
            Example4_ShareLevelComplete(currentLevel, currentScore);
        }
        
        /// <summary>
        /// Example: Show share menu
        /// </summary>
        public void OnShareMenuClicked()
        {
            Example5_ShowShareOptions();
        }
    }
}


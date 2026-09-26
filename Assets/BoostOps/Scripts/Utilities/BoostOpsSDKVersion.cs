namespace BoostOps
{
    /// <summary>
    /// Single source of truth for the SDK version reported to the backend
    /// (event context sdk_version, User-Agent header, remote-config handshake).
    ///
    /// IMPORTANT: this constant is bumped automatically by publish-sdk-release.sh
    /// alongside package.json — do not edit it by hand during a release.
    /// Historical note: before 1.2.2 every build reported the hardcoded string
    /// "2.0.6" regardless of the actual release version, which made SDK
    /// versions indistinguishable in backend data.
    /// </summary>
    public static class BoostOpsSDKVersion
    {
        public const string VERSION = "1.2.2";
    }
}

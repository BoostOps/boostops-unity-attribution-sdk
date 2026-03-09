using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace BoostOps.Internal
{
    /// <summary>
    /// Debug utility to track app open event calls and identify double-sending
    /// </summary>
    public static class AppOpenEventDebugger
    {
        private static List<AppOpenEventCall> _callHistory = new List<AppOpenEventCall>();
        private static int _callCount = 0;
        
        public struct AppOpenEventCall
        {
            public int CallNumber;
            public DateTime Timestamp;
            public string LaunchType;
            public bool? IsFirstSession;
            public string CallerMethod;
            public string StackTrace;
        }
        
        /// <summary>
        /// Record an app open event call
        /// </summary>
        public static void RecordCall(string launchType, bool? isFirstSession, string callerMethod = null)
        {
            _callCount++;
            
            // Get the calling method name if not provided
            if (string.IsNullOrEmpty(callerMethod))
            {
                var stackTrace = new StackTrace();
                if (stackTrace.FrameCount > 1)
                {
                    var callerFrame = stackTrace.GetFrame(1);
                    var method = callerFrame.GetMethod();
                    callerMethod = $"{method.DeclaringType?.Name}.{method.Name}";
                }
            }
            
            var call = new AppOpenEventCall
            {
                CallNumber = _callCount,
                Timestamp = DateTime.Now,
                LaunchType = launchType,
                IsFirstSession = isFirstSession,
                CallerMethod = callerMethod,
                StackTrace = new StackTrace(1, true).ToString() // Skip this method
            };
            
            _callHistory.Add(call);
            
            // UnityEngine.Debug.Log($"[AppOpenEventDebugger] 📞 Call #{_callCount} - LaunchType: {launchType}, IsFirstSession: {isFirstSession}, Caller: {callerMethod}");
            
            // Check for duplicate calls within 100ms (but ignore calls in the same stack trace)
            if (_callHistory.Count > 1)
            {
                var previousCall = _callHistory[_callHistory.Count - 2];
                var timeDiff = (call.Timestamp - previousCall.Timestamp).TotalMilliseconds;
                
                if (timeDiff < 100)
                {
                    // Check if this is the same call chain (current stack contains previous caller)
                    bool isSameCallChain = call.StackTrace.Contains(previousCall.CallerMethod);
                    
                    if (!isSameCallChain)
                    {
                        // This is a true duplicate - different call chains happening at the same time
                        UnityEngine.Debug.LogWarning($"[AppOpenEventDebugger] ⚠️ TRUE DOUBLE SEND DETECTED! Two separate calls within {timeDiff:F1}ms");
                        UnityEngine.Debug.LogWarning($"[AppOpenEventDebugger]   Previous call #{previousCall.CallNumber}: {previousCall.LaunchType}, from {previousCall.CallerMethod}");
                        UnityEngine.Debug.LogWarning($"[AppOpenEventDebugger]   Current call #{call.CallNumber}: {call.LaunchType}, from {call.CallerMethod}");
                        
                        // Log stack traces for comparison
                        UnityEngine.Debug.Log($"[AppOpenEventDebugger] Previous call stack:\n{previousCall.StackTrace}");
                        UnityEngine.Debug.Log($"[AppOpenEventDebugger] Current call stack:\n{call.StackTrace}");
                    }
                    // else
                    // {
                    //     // This is just the normal call chain - not a duplicate
                    //     UnityEngine.Debug.Log($"[AppOpenEventDebugger] ✅ Call #{call.CallNumber} is part of the same call chain as #{previousCall.CallNumber} (not a duplicate)");
                    // }
                }
            }
        }
        
        /// <summary>
        /// Get the full call history
        /// </summary>
        public static List<AppOpenEventCall> GetCallHistory()
        {
            return new List<AppOpenEventCall>(_callHistory);
        }
        
        /// <summary>
        /// Clear the call history
        /// </summary>
        public static void ClearHistory()
        {
            _callHistory.Clear();
            _callCount = 0;
            UnityEngine.Debug.Log("[AppOpenEventDebugger] Call history cleared");
        }
        
        /// <summary>
        /// Print a summary of all calls
        /// </summary>
        public static void PrintSummary()
        {
            UnityEngine.Debug.Log($"[AppOpenEventDebugger] === APP OPEN EVENT CALL SUMMARY ===");
            UnityEngine.Debug.Log($"[AppOpenEventDebugger] Total calls: {_callCount}");
            UnityEngine.Debug.Log($"[AppOpenEventDebugger] Call history:");
            
            for (int i = 0; i < _callHistory.Count; i++)
            {
                var call = _callHistory[i];
                UnityEngine.Debug.Log($"[AppOpenEventDebugger]   #{call.CallNumber} at {call.Timestamp:HH:mm:ss.fff} - LaunchType: {call.LaunchType}, IsFirstSession: {call.IsFirstSession}, Caller: {call.CallerMethod}");
            }
            
            UnityEngine.Debug.Log($"[AppOpenEventDebugger] ====================================");
        }
    }
}



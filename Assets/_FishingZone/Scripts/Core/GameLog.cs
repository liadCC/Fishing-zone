using UnityEngine;

namespace FishingZone.Core
{
    /// <summary>
    /// Categories used to tag console output. Kept as an enum rather than free strings so
    /// filtering the Console by "[FISH]" stays reliable as more systems start logging.
    /// </summary>
    public enum LogCategory
    {
        Boot,
        Flow,
        Input,
        Network,
        Save,
        Mission,
        Fish,
        Economy,
        UI
    }

    /// <summary>
    /// Single entry point for gameplay logging, so every system produces the same
    /// "[CATEGORY] message" format described in the Technical Specification.
    /// </summary>
    public static class GameLog
    {
        public static void Info(LogCategory category, string message)
        {
            Debug.Log(Format(category, message));
        }

        public static void Warn(LogCategory category, string message)
        {
            Debug.LogWarning(Format(category, message));
        }

        public static void Error(LogCategory category, string message)
        {
            Debug.LogError(Format(category, message));
        }

        private static string Format(LogCategory category, string message)
        {
            return $"[{category.ToString().ToUpperInvariant()}] {message}";
        }
    }
}

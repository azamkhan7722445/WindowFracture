using MoreMountains.NiceVibrations;
using UnityEngine;

namespace GlassSystem.Scripts
{
    public static class VibrationsHandler
    {
        private const bool DefaultToRegularVibrate = true;
        private const bool AlsoRumble = false;
        private const float RepeatedStrongImpactInterval = 0.08f;

        private static bool _isEnabled = true;
        private static bool _logInEditor = true;
        private static float _lastRepeatedStrongImpactTime = -RepeatedStrongImpactInterval;

        public static bool IsEnabled => _isEnabled;
        public static bool SupportsDeviceHaptics => IsNiceVibrationsSupportedPlatform();

        public static void SetEnabled(bool isEnabled)
        {
            _isEnabled = isEnabled;

            if (IsNiceVibrationsSupportedPlatform())
                MMVibrationManager.SetHapticsActive(isEnabled);
        }

        public static void SetEditorLogging(bool shouldLog)
        {
            _logInEditor = shouldLog;
        }

        public static void Tap()
        {
            Play(HapticTypes.LightImpact);
        }

        public static void Selection()
        {
            Play(HapticTypes.Selection);
        }

        public static void LightImpact()
        {
            Play(HapticTypes.LightImpact);
        }

        public static void MediumImpact()
        {
            Play(HapticTypes.MediumImpact);
        }

        public static void HeavyImpact()
        {
            Play(HapticTypes.HeavyImpact);
        }

        public static void RigidImpact()
        {
            Play(HapticTypes.RigidImpact);
        }

        public static void SoftImpact()
        {
            Play(HapticTypes.SoftImpact);
        }

        public static void Success()
        {
            Play(HapticTypes.Success);
        }

        public static void Warning()
        {
            Play(HapticTypes.Warning);
        }

        public static void Failure()
        {
            Play(HapticTypes.Failure);
        }

        public static void GlassTap()
        {
            LightImpact();
        }

        public static void GlassBreak()
        {
            StrongImpact("Glass Break");
        }

        public static void FinalShatter()
        {
            StrongImpact("Final Shatter");
        }

        public static void RepeatedFinalShatter()
        {
            if (Time.unscaledTime - _lastRepeatedStrongImpactTime < RepeatedStrongImpactInterval)
                return;

            _lastRepeatedStrongImpactTime = Time.unscaledTime;
            FinalShatter();
        }

        public static void BombBlast()
        {
            StrongImpact("Bomb Blast");
        }

        public static void VibrateDefault()
        {
            if (!_isEnabled)
                return;

            LogEditorHaptic("Default Vibrate");

            if (!IsNiceVibrationsSupportedPlatform())
                return;

            MMVibrationManager.Vibrate();
        }

        public static void StopAll()
        {
            if (!IsNiceVibrationsSupportedPlatform())
                return;

            MMVibrationManager.StopAllHaptics(AlsoRumble);
        }

        public static void Play(HapticTypes hapticType)
        {
            if (!_isEnabled || hapticType == HapticTypes.None)
                return;

            LogEditorHaptic(hapticType.ToString());

            if (!IsNiceVibrationsSupportedPlatform())
                return;

            MMVibrationManager.Haptic(hapticType, DefaultToRegularVibrate, AlsoRumble);
        }

        private static void StrongImpact(string hapticName)
        {
            if (!_isEnabled)
                return;

            LogEditorHaptic(hapticName);

            if (!IsNiceVibrationsSupportedPlatform())
                return;

            MMVibrationManager.TransientHaptic(1f, 1f, AlsoRumble);
        }

        private static bool IsNiceVibrationsSupportedPlatform()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private static void LogEditorHaptic(string hapticName)
        {
#if UNITY_EDITOR
            if (_logInEditor)
                Debug.Log($"Haptic: {hapticName}");
#endif
        }
    }
}

using UnityEngine;

namespace GlassSystem.Scripts
{
    public readonly struct DevicePerformanceProfile
    {
        public readonly int MaxShardsPerFrame;
        public readonly int MaxBlastCount;
        public readonly int MaxPrespawnParticles;
        public readonly float MinBlastDelay;

        private DevicePerformanceProfile(
            int maxShardsPerFrame,
            int maxBlastCount,
            int maxPrespawnParticles,
            float minBlastDelay
        )
        {
            MaxShardsPerFrame = maxShardsPerFrame;
            MaxBlastCount = maxBlastCount;
            MaxPrespawnParticles = maxPrespawnParticles;
            MinBlastDelay = minBlastDelay;
        }

        public static DevicePerformanceProfile Current => FromMemory(SystemInfo.systemMemorySize);

        private static DevicePerformanceProfile FromMemory(int memoryMb)
        {
            if (memoryMb <= 3072)
                return new DevicePerformanceProfile(1, 5, 5, 0.16f);

            if (memoryMb <= 5120)
                return new DevicePerformanceProfile(1, 7, 7, 0.12f);

            if (memoryMb <= 8192)
                return new DevicePerformanceProfile(2, 10, 10, 0.08f);

            return new DevicePerformanceProfile(3, 12, 12, 0.05f);
        }

        public int ClampShardsPerFrame(int value)
        {
            return Mathf.Clamp(value, 1, MaxShardsPerFrame);
        }

        public int ClampBlastCount(int value)
        {
            return Mathf.Clamp(value, 1, MaxBlastCount);
        }

        public int ClampPrespawnParticles(int value)
        {
            return Mathf.Clamp(value, 1, MaxPrespawnParticles);
        }

        public Vector2 ClampBlastDelayRange(Vector2 value)
        {
            float min = Mathf.Max(Mathf.Min(value.x, value.y), MinBlastDelay);
            float max = Mathf.Max(Mathf.Max(value.x, value.y), min);
            return new Vector2(min, max);
        }
    }
}

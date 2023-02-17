using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace TensionTools
{
    public static class LeakDetectionControl
    {
        [MenuItem("Tension Tools/Leak Detection Mode/Enabled")]
        private static void LeakDetection()
        {
            UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.Enabled);
        }

        [MenuItem("Tension Tools/Leak Detection Mode/With Stack Trace")]
        private static void LeakDetectionWithStackTrace()
        {
            UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.EnabledWithStackTrace);
        }

        [MenuItem("Tension Tools/Leak Detection Mode/Disabled")]
        private static void NoLeakDetection()
        {
            UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.Disabled);
        }
    }
}
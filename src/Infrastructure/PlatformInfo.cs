using UnityEngine;

namespace BoneAI.Infrastructure;

public static class PlatformInfo
{
    public static bool IsAndroid => Application.platform == RuntimePlatform.Android;
    public static string DisplayName => IsAndroid ? "Standalone Quest" : "Windows PCVR";
}

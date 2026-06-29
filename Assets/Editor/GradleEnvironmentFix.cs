using System;
using System.IO;
using UnityEditor;

[InitializeOnLoad]
public static class GradleEnvironmentFix
{
    private const string GradleUserHomeFolderName = ".gradle";

    static GradleEnvironmentFix()
    {
        Apply();
    }

    [MenuItem("Tools/Fix/Apply Gradle Environment Fix")]
    public static void Apply()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
            userProfile = Environment.GetEnvironmentVariable("USERPROFILE");

        if (string.IsNullOrWhiteSpace(userProfile))
            return;

        var gradleUserHome = Path.Combine(userProfile, GradleUserHomeFolderName);
        Directory.CreateDirectory(gradleUserHome);

        Environment.SetEnvironmentVariable("GRADLE_USER_HOME", gradleUserHome, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("HOME", userProfile, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("USERPROFILE", userProfile, EnvironmentVariableTarget.Process);

        UnityEngine.Debug.Log($"Gradle environment fixed. GRADLE_USER_HOME={gradleUserHome}");
    }
}

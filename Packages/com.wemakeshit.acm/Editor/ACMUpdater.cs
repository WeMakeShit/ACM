// Avatar Creation Manager - VCC-managed update interface - By DTAA
// This replaces the standalone ZIP updater for the VCC edition.
// The VCC owns installation, updates and downgrades of package files.
using UnityEditor;
using UnityEngine;

public static class ACMUpdater
{
    public const string CurrentVersion = "0.8.3";
    public const bool IsVccManaged = true;
    public static readonly string StatusText =
        "ACM updates are managed by VRChat Creator Companion.";

    // Keep the method used by the existing ACM main window so that its
    // Check for Updates button never tries to modify VCC-managed package files.
    public static void CheckForUpdates(bool manual)
    {
        if (manual)
            EditorUtility.DisplayDialog("ACM - Updates through VCC",
                "ACM is installed as a VCC package. To update it, open VRChat Creator " +
                "Companion, select this project, and choose the latest ACM package version. " +
                "The ACM editor files are never updated from inside Unity.", "OK");
        else
            Debug.Log("ACM: updates managed by VRChat Creator Companion.");
    }
}

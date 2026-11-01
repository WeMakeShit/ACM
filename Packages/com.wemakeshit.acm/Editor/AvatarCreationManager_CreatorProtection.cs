// Optional provenance and credit helpers. These deliberately add no DRM or
// obfuscation: VRChat clients must download avatars in order to display them.
// The receipt is evidence for the creator's own records; it is not a claim that
// an uploaded avatar cannot be copied.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed partial class AvatarCreationManager
{
    [Serializable]
    private class CreatorReceipt
    {
        public string format = "ACM Creator Proof v1";
        public string buildId;
        public string createdUtc;
        public string creator;
        public string contact;
        public string avatarName;
        public string avatarKey;
        public string acmVersion;
        public bool visibleCreditEnabled;
        public string visibleCreditLabel;
        public string note = "This is a creator-provided provenance record, not DRM or proof of exclusive ownership.";
        public string receiptHash;
    }

    private void DrawCreatorProtection()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Creator Protection (optional)", EditorStyles.boldLabel);
        creatorProtection = EditorGUILayout.Toggle(new GUIContent("Create ownership receipt",
            "Makes a dated local receipt inside this ACM build. It never changes locomotion."),
            creatorProtection);
        if (!creatorProtection) return;

        EditorGUILayout.HelpBox("ACM will save a dated CreatorProof.json in this build folder. " +
            "It helps document your work, but cannot stop someone determined to extract an avatar.",
            MessageType.Info);
        creatorName = EditorGUILayout.TextField("Creator name / brand", creatorName);
        creatorContact = EditorGUILayout.TextField("Contact (optional)", creatorContact);
        brandingIcon = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Branding picture",
            "Used as the action-wheel icon for the optional credit button."), brandingIcon,
            typeof(Texture2D), false);
        visibleCredit = EditorGUILayout.Toggle(new GUIContent("Add action-wheel credit button",
            "Adds a non-toggleable button in ACM's VRChat action-wheel menu. It only displays your name and image."),
            visibleCredit);
        if (visibleCredit)
            EditorGUILayout.HelpBox("This is an information-only button. It does not toggle an object, " +
                "does not create an FX layer, and uses no Expression Parameters.", MessageType.None);
    }

    private bool HasCreatorCreditButton()
    {
        return creatorProtection && visibleCredit && !string.IsNullOrWhiteSpace(creatorName);
    }

    private VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control CreatorCreditButton()
    {
        return new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control {
            name = "Creator — " + creatorName.Trim(),
            type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.Button,
            icon = brandingIcon
        };
    }

    private string CreatorProtectionPreflightProblem()
    {
        if (!creatorProtection) return null;
        if (string.IsNullOrWhiteSpace(creatorName))
            return "Creator Protection needs a Creator name / brand.";
        return null;
    }

    private void WriteCreatorReceipt(string buildFolder)
    {
        var receipt = new CreatorReceipt {
            buildId = "ACM-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant(),
            createdUtc = DateTime.UtcNow.ToString("o"),
            creator = creatorName == null ? "" : creatorName.Trim(),
            contact = creatorContact == null ? "" : creatorContact.Trim(),
            avatarName = avatar == null ? "" : avatar.name,
            avatarKey = Key(),
            acmVersion = "0.9.3",
            visibleCreditEnabled = visibleCredit,
            visibleCreditLabel = HasCreatorCreditButton() ? "Creator — " + creatorName.Trim() : ""
        };
        string unsigned = JsonUtility.ToJson(receipt, true);
        using (var sha = SHA256.Create())
            receipt.receiptHash = BitConverter.ToString(sha.ComputeHash(
                Encoding.UTF8.GetBytes(unsigned))).Replace("-", "");
        string projectPath = Path.Combine(Directory.GetCurrentDirectory(),
            buildFolder.Replace('/', Path.DirectorySeparatorChar), "CreatorProof.json");
        File.WriteAllText(projectPath, JsonUtility.ToJson(receipt, true), Encoding.UTF8);
        Debug.Log("ACM Creator Protection: receipt created at " + buildFolder + "/CreatorProof.json");
    }
}

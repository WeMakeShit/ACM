// ACM v0.8 Smart Engine foundation — editor-only. The avatar's original source
// assets are never edited by this module. Restore points are reference snapshots,
// not copies of source asset bytes; see README for the recovery boundary.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

public sealed partial class AvatarCreationManager
{
    private const int SmartRestoreLimit = 5;
    private const int SmartErrorLimit = 20;
    private const int SmartHistoryMaxCharacters = 1500000;

    [Serializable]
    private class SmartLayerReference
    {
        public int type;
        public bool isDefault;
        public string path;
    }

    [Serializable]
    private class SmartRestorePoint
    {
        public string timestampUtc;
        public string reason;
        public string expressionMenuPath;
        public string expressionParametersPath;
        public bool customExpressions;
        public bool customizeAnimationLayers;
        public List<SmartLayerReference> layers = new List<SmartLayerReference>();
        public string originalManifestJson;
        public string profileJson;
        public string generatedFolder;
        // Original-source inputs belong to ACM's GUI, not to the descriptor.
        public string sourceMenuGuid; public long sourceMenuId;
        public string sourceParamsGuid; public long sourceParamsId;
        public string sourceFxGuid; public long sourceFxId;
        public string sourceActionGuid; public long sourceActionId;
    }

    [Serializable]
    private class SmartFailureRecord
    {
        public string stage;
        public string message;
        public string timeUtc;
        public int count;
    }

    [Serializable]
    private class SmartRepairRecord
    {
        public string timeUtc;
        public string summary;
    }

    [Serializable]
    private class SmartState
    {
        public int version = 1;
        public string lastInputFingerprint;
        public string lastOutputFingerprint;
        public string lastGoodProfileJson;
        public string lastGoodBuildFolder;
        public string lastSuccessUtc;
        public List<SmartRestorePoint> restorePoints = new List<SmartRestorePoint>();
        public List<SmartFailureRecord> failures = new List<SmartFailureRecord>();
        public List<SmartRepairRecord> repairs = new List<SmartRepairRecord>();
    }

    private bool smartGenerationRunning;
    private readonly List<string> smartPendingRepairs = new List<string>();

    private static string SmartUtc()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
    }

    private string SmartPath()
    {
        return Profiles + "/Smart_" + Key() + ".json";
    }

    private static string SmartReadText(string path)
    {
        return !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private SmartState SmartLoad()
    {
        if (avatar == null) return new SmartState();
        try
        {
            string json = SmartReadText(SmartPath());
            var state = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SmartState>(json);
            if (state == null) state = new SmartState();
            if (state.restorePoints == null) state.restorePoints = new List<SmartRestorePoint>();
            if (state.failures == null) state.failures = new List<SmartFailureRecord>();
            if (state.repairs == null) state.repairs = new List<SmartRepairRecord>();
            return state;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ACM Smart History could not be read; no backups were erased: " + ex.Message);
            return new SmartState();
        }
    }

    private void SmartSave(SmartState state)
    {
        if (avatar == null || state == null) return;
        Folders();
        // Prune snapshot records only. Never delete generated controllers, source
        // assets, folders, or files referenced by an avatar merely to save space.
        while (state.restorePoints.Count > SmartRestoreLimit)
            state.restorePoints.RemoveAt(0);
        while (state.failures.Count > SmartErrorLimit)
            state.failures.RemoveAt(0);
        while (state.repairs.Count > SmartErrorLimit)
            state.repairs.RemoveAt(0);
        string json = JsonUtility.ToJson(state, true);
        while (json.Length > SmartHistoryMaxCharacters && state.restorePoints.Count > 1)
        {
            state.restorePoints.RemoveAt(0);
            json = JsonUtility.ToJson(state, true);
        }
        if (json.Length > SmartHistoryMaxCharacters)
            throw new IOException("ACM Smart History is too large; kept existing history unchanged.");
        string path = SmartPath();
        string temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                // Atomic replacement where supported. Do not destroy the current
                // history if the replacement fails on this file system.
                File.Replace(temp, path, null);
            }
            else File.Move(temp, path);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private SmartRestorePoint SmartCapture(string reason)
    {
        if (avatar == null) return null;
        var snap = new SmartRestorePoint
        {
            timestampUtc = SmartUtc(),
            reason = reason,
            expressionMenuPath = AssetDatabase.GetAssetPath(avatar.expressionsMenu),
            expressionParametersPath = AssetDatabase.GetAssetPath(avatar.expressionParameters),
            customExpressions = avatar.customExpressions,
            customizeAnimationLayers = avatar.customizeAnimationLayers,
            originalManifestJson = SmartReadText(OriginalPath()),
            profileJson = SmartReadText(ProfilePath())
        };
        OriginalFile origin = null;
        try
        {
            if (!string.IsNullOrEmpty(snap.originalManifestJson))
                origin = JsonUtility.FromJson<OriginalFile>(snap.originalManifestJson);
        }
        catch { /* A damaged manifest is retained verbatim for later diagnosis. */ }
        snap.generatedFolder = origin == null ? "" : origin.generatedFolder;
        FileId(originalRootMenu, out snap.sourceMenuGuid, out snap.sourceMenuId);
        FileId(originalExpressionParameters, out snap.sourceParamsGuid, out snap.sourceParamsId);
        FileId(originalFXController, out snap.sourceFxGuid, out snap.sourceFxId);
        FileId(originalActionController, out snap.sourceActionGuid, out snap.sourceActionId);
        foreach (var layer in avatar.baseAnimationLayers)
        {
            snap.layers.Add(new SmartLayerReference
            {
                type = (int)layer.type,
                isDefault = layer.isDefault,
                path = AssetDatabase.GetAssetPath(layer.animatorController)
            });
        }
        return snap;
    }

    private int SmartRecoverFieldsFromSnapshot(SmartRestorePoint snap)
    {
        if (snap == null) return 0;
        int recovered = 0;
        if (originalRootMenu == null)
        {
            var menu = ResolveAsset<VRCExpressionsMenu>(snap.sourceMenuGuid, snap.sourceMenuId);
            if (menu != null && !IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(menu)))
            { originalRootMenu = menu; recovered++; }
        }
        if (originalExpressionParameters == null)
        {
            var pars = ResolveAsset<VRCExpressionParameters>(snap.sourceParamsGuid, snap.sourceParamsId);
            if (pars != null && !IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(pars)))
            { originalExpressionParameters = pars; recovered++; }
        }
        if (originalFXController == null)
        {
            var fx = ResolveAsset<AnimatorController>(snap.sourceFxGuid, snap.sourceFxId);
            if (fx != null && !IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(fx)))
            { originalFXController = fx; recovered++; }
        }
        if (originalActionController == null)
        {
            var action = ResolveAsset<AnimatorController>(snap.sourceActionGuid, snap.sourceActionId);
            if (action != null && !IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(action)))
            { originalActionController = action; recovered++; }
        }
        return recovered + RecoverOriginalFieldsFromManifestJson(snap.originalManifestJson);
    }

    private int SmartRecoverOriginalFieldsFromHistory()
    {
        if (avatar == null) return 0;
        SmartState state = SmartLoad();
        int recovered = 0;
        for (int i = state.restorePoints.Count - 1; i >= 0; --i)
        {
            var snap = state.restorePoints[i];
            if (snap == null) continue;
            if (originalRootMenu == null)
            {
                var menu = ResolveAsset<VRCExpressionsMenu>(snap.sourceMenuGuid, snap.sourceMenuId);
                if (menu != null && !IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(menu)))
                { originalRootMenu = menu; recovered++; }
            }
            if (originalExpressionParameters == null)
            {
                var pars = ResolveAsset<VRCExpressionParameters>(snap.sourceParamsGuid, snap.sourceParamsId);
                if (pars != null && !IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(pars)))
                { originalExpressionParameters = pars; recovered++; }
            }
            if (originalFXController == null)
            {
                var fx = ResolveAsset<AnimatorController>(snap.sourceFxGuid, snap.sourceFxId);
                if (fx != null && !IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(fx)))
                { originalFXController = fx; recovered++; }
            }
            if (originalActionController == null)
            {
                var action = ResolveAsset<AnimatorController>(snap.sourceActionGuid, snap.sourceActionId);
                if (action != null && !IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(action)))
                { originalActionController = action; recovered++; }
            }
            recovered += RecoverOriginalFieldsFromManifestJson(snap.originalManifestJson);
            if (originalRootMenu != null && originalExpressionParameters != null &&
                originalFXController != null && originalActionController != null) break;
        }
        return recovered;
    }

    private static bool SmartValidAsset(string path, Type assetType)
    {
        return string.IsNullOrEmpty(path) ||
            AssetDatabase.LoadAssetAtPath(path, assetType) != null;
    }

    private static bool SmartRestorePointUsable(SmartRestorePoint snap)
    {
        if (snap == null) return false;
        if (!SmartValidAsset(snap.expressionMenuPath, typeof(VRCExpressionsMenu)) ||
            !SmartValidAsset(snap.expressionParametersPath, typeof(VRCExpressionParameters)))
            return false;
        if (snap.layers == null) return false;
        foreach (var layer in snap.layers)
        {
            if (layer != null && !SmartValidAsset(layer.path, typeof(RuntimeAnimatorController)))
                return false;
        }
        return true;
    }

    private void SmartApplySnapshot(SmartRestorePoint snap, bool restoreProfileAndManifest)
    {
        if (avatar == null || !SmartRestorePointUsable(snap))
            throw new Exception("Restore point refers to missing assets. No assignments were changed.");
        Undo.RecordObject(avatar, "ACM Smart Restore Point");
        avatar.expressionsMenu = string.IsNullOrEmpty(snap.expressionMenuPath) ? null :
            AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(snap.expressionMenuPath);
        avatar.expressionParameters = string.IsNullOrEmpty(snap.expressionParametersPath) ? null :
            AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(snap.expressionParametersPath);
        avatar.customExpressions = snap.customExpressions;
        var layers = avatar.baseAnimationLayers;
        foreach (var saved in snap.layers)
        {
            if (saved == null) continue;
            int i = Array.FindIndex(layers, x => (int)x.type == saved.type);
            if (i < 0) continue;
            layers[i].isDefault = saved.isDefault;
            layers[i].animatorController = string.IsNullOrEmpty(saved.path) ? null :
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(saved.path);
        }
        avatar.baseAnimationLayers = layers;
        avatar.customizeAnimationLayers = snap.customizeAnimationLayers;
        EditorUtility.SetDirty(avatar);
        if (avatar.gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
        if (restoreProfileAndManifest)
        {
            Folders();
            if (string.IsNullOrEmpty(snap.originalManifestJson))
            {
                if (File.Exists(OriginalPath())) File.Delete(OriginalPath());
            }
            else File.WriteAllText(OriginalPath(), snap.originalManifestJson);
            if (!string.IsNullOrEmpty(snap.profileJson))
                File.WriteAllText(ProfilePath(), snap.profileJson);
        }
    }

    private void SmartRestoreLatest()
    {
        if (avatar == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        SmartState state = SmartLoad();
        var snap = state.restorePoints.LastOrDefault(SmartRestorePointUsable);
        if (snap == null)
        {
            EditorUtility.DisplayDialog("ACM Recovery", "No usable restore point is available. Original assets were not touched.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("ACM — Restore Previous Setup",
            "Restore avatar assignments from " + snap.timestampUtc + "?\n\n" +
            "This reassigns previously recorded menus, parameters and controllers, " +
            "and restores ACM's manifest/profile. It does not change your original asset files.",
            "Restore", "Cancel")) return;
        // Capture the current state so the user can undo even the recovery action.
        var current = SmartCapture("Before manual recovery");
        try
        {
            SmartApplySnapshot(snap, true);
            AssetDatabase.SaveAssets();
            state.restorePoints.Remove(snap);
            if (current != null && SmartRestorePointUsable(current))
                state.restorePoints.Add(current);
            state.lastInputFingerprint = ""; // Force an actual rebuild next time.
            state.lastOutputFingerprint = "";
            SmartSave(state);
            LoadProfile();
            // Older profiles didn't store source fields; only recover verifiable
            // originals from the restored manifest/snapshot, never by names.
            SmartRecoverOriginalFieldsFromHistory();
            if (snap != null)
                SmartRecoverFieldsFromSnapshot(snap);
            SaveProfile();
            EditorUtility.DisplayDialog("ACM Recovery",
                "Previous avatar assignments and any verifiable original source fields " +
                "were restored. Blank original fields mean ACM has no trustworthy " +
                "record for those assets; select them manually before updating.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("ACM Smart Restore failed: " + ex);
            EditorUtility.DisplayDialog("ACM Recovery", ex.Message, "OK");
        }
    }

    private static string SmartSha256(string input)
    {
        using (var h = SHA256.Create())
        {
            return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(input ?? "")))
                .Replace("-", "");
        }
    }

    private static void SmartAppendAssetHash(StringBuilder builder, string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        builder.Append("|asset:").Append(path);
        try { builder.Append(':').Append(AssetDatabase.GetAssetDependencyHash(path)); }
        catch { builder.Append(":unavailable"); }
    }

    private string SmartInputFingerprint(OriginalFile origin)
    {
        var sb = new StringBuilder(4096);
        sb.Append("smart-v1|").Append(SmartReadText(ProfilePath()));
        if (origin != null)
        {
            SmartAppendAssetHash(sb, origin.menuPath);
            SmartAppendAssetHash(sb, origin.parameterPath);
            SmartAppendAssetHash(sb, origin.controllerPath);
            SmartAppendAssetHash(sb, origin.fxPath);
            SmartAppendAssetHash(sb, origin.actionPath);
            sb.Append("|defaults:").Append(origin.baseWasDefault).Append(origin.fxWasDefault)
                .Append(origin.actionWasDefault);
        }
        SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(sourceBase));
        foreach (var pose in standing.Concat(crouching).Concat(prone))
        {
            if (pose == null) continue;
            SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(pose.clip));
            SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(pose.icon));
        }
        foreach (var dance in dances)
        {
            if (dance == null) continue;
            SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(dance.clip));
            SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(dance.audio));
            SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(dance.icon));
        }
        foreach (var toggle in toggles)
        {
            if (toggle == null) continue;
            SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(toggle.onClip));
            SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(toggle.offClip));
            SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(toggle.icon));
            if (toggle.target != null)
                sb.Append("|object:").Append(RelativePath(toggle.target.transform))
                    .Append(":").Append(toggle.target.activeSelf);
        }
        // A changed or newly added optional default PNG must invalidate a no-op.
        string iconFolder = "Packages/com.wemakeshit.acm/Icons";
        if (AssetDatabase.IsValidFolder(iconFolder))
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { iconFolder }).OrderBy(x => x))
                SmartAppendAssetHash(sb, AssetDatabase.GUIDToAssetPath(guid));
        return SmartSha256(sb.ToString());
    }

    private string SmartOutputFingerprint()
    {
        if (avatar == null) return "";
        var sb = new StringBuilder();
        SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(avatar.expressionsMenu));
        SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(avatar.expressionParameters));
        foreach (var layer in avatar.baseAnimationLayers)
        {
            sb.Append("|layer:").Append((int)layer.type).Append(':').Append(layer.isDefault);
            SmartAppendAssetHash(sb, AssetDatabase.GetAssetPath(layer.animatorController));
        }
        return SmartSha256(sb.ToString());
    }

    private bool SmartCanSkipBuild(OriginalFile origin, bool installed, string inputFingerprint)
    {
        if (!installed || string.IsNullOrEmpty(inputFingerprint)) return false;
        var state = SmartLoad();
        if (state.lastInputFingerprint != inputFingerprint ||
            string.IsNullOrEmpty(state.lastOutputFingerprint) ||
            origin == null || origin.generatedFolder != state.lastGoodBuildFolder)
            return false;
        return state.lastOutputFingerprint == SmartOutputFingerprint();
    }

    private void SmartCommitSuccessfulBuild(SmartRestorePoint before, OriginalFile origin, string inputFingerprint)
    {
        var state = SmartLoad();
        if (before != null && SmartRestorePointUsable(before))
        {
            // Restore the last known successful profile, not any in-progress GUI edits.
            if (!string.IsNullOrEmpty(state.lastGoodProfileJson))
                before.profileJson = state.lastGoodProfileJson;
            if (state.restorePoints.Count == 0 ||
                state.restorePoints.Last().expressionMenuPath != before.expressionMenuPath ||
                state.restorePoints.Last().expressionParametersPath != before.expressionParametersPath ||
                state.restorePoints.Last().generatedFolder != before.generatedFolder)
                state.restorePoints.Add(before);
        }
        foreach (var repair in smartPendingRepairs.Distinct())
            state.repairs.Add(new SmartRepairRecord { timeUtc = SmartUtc(), summary = repair });
        state.lastInputFingerprint = inputFingerprint;
        state.lastOutputFingerprint = SmartOutputFingerprint();
        state.lastGoodProfileJson = SmartReadText(ProfilePath());
        state.lastGoodBuildFolder = origin == null ? "" : origin.generatedFolder;
        state.lastSuccessUtc = SmartUtc();
        SmartSave(state);
    }

    private void SmartRememberRepairs(IEnumerable<string> repairs)
    {
        if (avatar == null || repairs == null) return;
        var items = repairs.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (items.Count == 0) return;
        if (smartGenerationRunning)
        {
            smartPendingRepairs.AddRange(items);
            return;
        }
        try
        {
            var state = SmartLoad();
            foreach (string repair in items)
                state.repairs.Add(new SmartRepairRecord { timeUtc = SmartUtc(), summary = repair });
            SmartSave(state);
        }
        catch (Exception ex)
        { Debug.LogWarning("ACM repair succeeded but history could not be updated: " + ex.Message); }
    }

    private void SmartRecordFailure(string stage, Exception error)
    {
        if (avatar == null) return;
        try
        {
            var state = SmartLoad();
            string msg = error == null ? "Unknown error" : error.GetType().Name + ": " + error.Message;
            string key = (stage ?? "Unknown stage") + "|" + msg;
            var known = state.failures.FirstOrDefault(x => x != null && x.stage + "|" + x.message == key);
            if (known == null)
            {
                known = new SmartFailureRecord { stage = stage ?? "Unknown stage", message = msg, count = 0 };
                state.failures.Add(known);
            }
            known.count++;
            known.timeUtc = SmartUtc();
            SmartSave(state);
        }
        catch (Exception historyError)
        {
            Debug.LogWarning("ACM could not record repair history: " + historyError.Message);
        }
    }

    private List<string> SmartHealthFindings()
    {
        var issues = new List<string>();
        if (avatar == null) { issues.Add("Select an avatar in the Hierarchy."); return issues; }
        if (avatar.GetComponent<Animator>() == null)
            issues.Add("Humanoid Animator missing.");
        if (avatar.expressionsMenu == null) issues.Add("No active Expressions Menu (safe repair available).");
        if (avatar.expressionParameters == null) issues.Add("No active Expression Parameters (safe repair available).");
        var pars = avatar.expressionParameters == null ? null : avatar.expressionParameters.parameters;
        if (pars != null)
        {
            if (pars.Any(x => x == null || string.IsNullOrEmpty(x.name)))
                issues.Add("Expressions Parameters contain unnamed entries.");
            if (pars.Where(x => x != null).GroupBy(x => x.name).Any(g => g.Count() > 1))
                issues.Add("Expressions Parameters contain duplicate names.");
        }
        var menu = avatar.expressionsMenu;
        if (menu != null && menu.controls != null && menu.controls.Count > 8)
            issues.Add("Active root menu has more than eight controls.");
        var layers = avatar.baseAnimationLayers;
        if (LayerIndex(layers, VRCAvatarDescriptor.AnimLayerType.Base) < 0 ||
            LayerIndex(layers, VRCAvatarDescriptor.AnimLayerType.Action) < 0 ||
            LayerIndex(layers, VRCAvatarDescriptor.AnimLayerType.FX) < 0)
            issues.Add("A required Base, Action or FX playable layer is missing.");
        return issues;
    }

    private void DrawSmartHealthPanel()
    {
        if (avatar == null) return;
        var state = SmartLoad();
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Smart Engine & Recovery", EditorStyles.boldLabel);
        var issues = SmartHealthFindings();
        if (issues.Count == 0)
            EditorGUILayout.HelpBox("Preflight: no structural issues detected. Existing features will be preserved.", MessageType.Info);
        else
            EditorGUILayout.HelpBox("Preflight found " + issues.Count + " issue(s):\n- " +
                string.Join("\n- ", issues.Take(4).ToArray()), MessageType.Warning);
        EditorGUILayout.LabelField("Restore points", state.restorePoints.Count + " / " + SmartRestoreLimit);
        if (!string.IsNullOrEmpty(state.lastSuccessUtc))
            EditorGUILayout.LabelField("Last build", state.lastSuccessUtc);
        if (state.repairs.Count > 0)
            EditorGUILayout.LabelField("Last repair", state.repairs.Last().summary);
        if (state.failures.Count > 0)
        {
            var last = state.failures.Last();
            EditorGUILayout.HelpBox("Last recorded problem: " + last.stage + " — " + last.message +
                " (seen " + last.count + "x)", MessageType.None);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Health Check", GUILayout.Height(26)))
                EditorUtility.DisplayDialog("ACM Avatar Health Check",
                    issues.Count == 0 ? "No structural problems detected. This is not a VRChat runtime test." :
                    string.Join("\n\n", issues.ToArray()), "OK");
            using (new EditorGUI.DisabledScope(!state.restorePoints.Any(SmartRestorePointUsable)))
            {
                if (GUILayout.Button("Restore Previous", GUILayout.Height(26)))
                    SmartRestoreLatest();
            }
        }
        EditorGUILayout.HelpBox("Keeps up to five compact restore records per avatar. " +
            "Old generated asset folders are NOT auto-deleted, because other Unity assets may reference them.",
            MessageType.None);
    }
}

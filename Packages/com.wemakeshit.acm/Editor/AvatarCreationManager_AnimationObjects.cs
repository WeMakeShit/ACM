// ACM v0.8.8 (preview): optional GameObject visibility per pose/dance.
// VRChat object visibility belongs in the FX playable layer, not Base/Action.
// This file is isolated from the protected v0.8.7 locomotion/pagination logic.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed partial class AvatarCreationManager
{
    [Serializable]
    private class AnimationObjectRule
    {
        public GameObject target;
        public string lastKnownPath = "";
        public bool activeWhilePlaying = true;
    }

    [Serializable]
    private class AnimationObjectRuleFile
    {
        public string targetPath;
        public bool activeWhilePlaying;
    }

    private class AnimationObjectSpec
    {
        public string path;
        public string parameter;
        public int id;
        public bool activeWhilePlaying;
        public bool defaultActive;
        public string label;
    }

    private void DrawAnimationObjectRules(ref List<AnimationObjectRule> rules)
    {
        if (rules == null) rules = new List<AnimationObjectRule>();
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Objects with this animation (optional)", EditorStyles.miniBoldLabel);
        if (rules.Count > 0)
            EditorGUILayout.LabelField("FX changes selected avatar objects; resets when the menu selection is cleared.",
                EditorStyles.miniLabel);
        for (int index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            if (rule == null) { rules.RemoveAt(index--); continue; }
            using (new EditorGUILayout.HorizontalScope())
            {
                rule.target = (GameObject)EditorGUILayout.ObjectField(
                    rule.target, typeof(GameObject), true);
                rule.activeWhilePlaying = GUILayout.Toggle(rule.activeWhilePlaying,
                    rule.activeWhilePlaying ? "ON" : "OFF", GUILayout.Width(48));
                if (GUILayout.Button("×", GUILayout.Width(27)))
                { rules.RemoveAt(index); GUI.changed = true; break; }
            }
            if (rule.target != null && avatar != null &&
                (rule.target == avatar.gameObject ||
                 !rule.target.transform.IsChildOf(avatar.transform)))
                EditorGUILayout.HelpBox("Choose a child object of the selected avatar, not the avatar root.",
                    MessageType.Warning);
            if (rule.target == null && !string.IsNullOrEmpty(rule.lastKnownPath))
                EditorGUILayout.HelpBox("Saved target missing: " + rule.lastKnownPath +
                    ". Relink before generating.", MessageType.Warning);
        }
        if (GUILayout.Button("+ Toggle Object with Animation", EditorStyles.miniButton))
        { rules.Add(new AnimationObjectRule()); GUI.changed = true; }
    }

    private List<AnimationObjectRuleFile> SaveAnimationObjectRules(
        List<AnimationObjectRule> rules)
    {
        return (rules ?? new List<AnimationObjectRule>())
            .Where(r => r != null)
            .Select(r => new AnimationObjectRuleFile {
                targetPath = r.target != null && avatar != null &&
                    r.target != avatar.gameObject &&
                    r.target.transform.IsChildOf(avatar.transform)
                    ? RelativePath(r.target.transform) : (r.lastKnownPath ?? ""),
                activeWhilePlaying = r.activeWhilePlaying
            }).ToList();
    }

    private List<AnimationObjectRule> LoadAnimationObjectRules(
        List<AnimationObjectRuleFile> entries)
    {
        return (entries ?? new List<AnimationObjectRuleFile>())
            .Where(e => e != null)
            .Select(e => new AnimationObjectRule {
                lastKnownPath = e.targetPath ?? "",
                target = FromRelativePath(e.targetPath),
                activeWhilePlaying = e.activeWhilePlaying
            }).ToList();
    }

    private List<AnimationObjectSpec> CollectAnimationObjectRules()
    {
        var specs = new List<AnimationObjectSpec>();
        AddAnimationObjectRules(specs, standing, Parameters[0], "Standing");
        AddAnimationObjectRules(specs, crouching, Parameters[1], "Crouching");
        AddAnimationObjectRules(specs, prone, Parameters[2], "Prone");
        foreach (var dance in dances)
            if (dance != null) AddAnimationObjectRules(specs, dance.objects,
                DanceParam, dance.id, dance.label);

        // A target can be used by several animations in the SAME selector
        // (e.g. multiple dances). Crossing selectors would create competing FX
        // layers and could leave the object in an unpredictable state.
        foreach (var group in specs.GroupBy(s => s.path, StringComparer.Ordinal))
        {
            if (group.Select(s => s.parameter).Distinct().Count() > 1)
                throw new Exception("Animation object '" + group.Key + "' is selected by different " +
                    "pose/dance categories. For now, use each GameObject in ONE category only " +
                    "(multiple dances OR multiple Standing poses, for example). " +
                    "Use separate child objects for other categories.");
            if (group.GroupBy(s => s.id).Any(g => g.Count() > 1))
                throw new Exception("Animation object '" + group.Key +
                    "' is listed more than once for the same animation.");
        }
        return specs;
    }

    private void AddAnimationObjectRules(List<AnimationObjectSpec> specs,
        List<Pose> poses, string parameter, string heading)
    {
        foreach (var pose in poses ?? new List<Pose>())
            if (pose != null) AddAnimationObjectRules(specs, pose.objects,
                parameter, pose.id, heading + " / " + pose.label);
    }

    private void AddAnimationObjectRules(List<AnimationObjectSpec> specs,
        List<AnimationObjectRule> rules, string parameter, int id, string label)
    {
        foreach (var rule in rules ?? new List<AnimationObjectRule>())
        {
            if (rule == null) continue;
            if (rule.target == null && !string.IsNullOrEmpty(rule.lastKnownPath))
                rule.target = FromRelativePath(rule.lastKnownPath);
            if (rule.target == null)
                throw new Exception("Animation '" + label + "' has an empty/missing object. " +
                    "Assign the object or remove its rule before generating.");
            if (rule.target == avatar.gameObject ||
                !rule.target.transform.IsChildOf(avatar.transform))
                throw new Exception("Animation '" + label +
                    "' can only toggle child objects of the selected avatar.");
            var path = RelativePath(rule.target.transform);
            if (string.IsNullOrEmpty(path) || avatar.transform.Find(path) != rule.target.transform)
                throw new Exception("Animation '" + label + "' has an ambiguous or invalid " +
                    "avatar object hierarchy path. Rename duplicate siblings before generating.");
            rule.lastKnownPath = path;
            specs.Add(new AnimationObjectSpec {
                path=path, parameter=parameter, id=id, label=label,
                activeWhilePlaying=rule.activeWhilePlaying,
                defaultActive=rule.target.activeSelf
            });
        }
    }

    private void ValidateAnimationObjectFxConflicts(AnimatorController manualFx,
        List<AnimationObjectSpec> objects, List<ToggleEntry> toggles)
    {
        if (objects.Count == 0) return;
        var paths = new HashSet<string>(objects.Select(o => o.path), StringComparer.Ordinal);
        // Don't overwrite animator bindings that a manually-authored FX layer owns.
        if (manualFx != null)
            foreach (var clip in manualFx.animationClips.Where(c => c != null).Distinct())
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive" &&
                        paths.Contains(binding.path))
                        throw new Exception("Original FX already controls '" + binding.path +
                            "'. ACM will not override its visibility for an animation.");
        foreach (var toggle in toggles ?? new List<ToggleEntry>())
        {
            if (toggle == null) continue;
            if (toggle.automatic)
            {
                if (toggle.target != null && paths.Contains(RelativePath(toggle.target.transform)))
                    throw new Exception("Object '" + toggle.target.name + "' has both an ACM " +
                        "manual visibility toggle and an animation object rule. Choose one.");
            }
            else
                foreach (var clip in new [] { toggle.onClip, toggle.offClip })
                    if (clip != null && AnimationUtility.GetCurveBindings(clip).Any(b =>
                        b.type == typeof(GameObject) && b.propertyName == "m_IsActive" &&
                        paths.Contains(b.path)))
                        throw new Exception("An ACM custom toggle also controls an object " +
                            "selected for automatic animation visibility. Choose one.");
        }
    }

    private void SmartAppendAnimationObjectStates(StringBuilder sb,
        List<AnimationObjectRule> rules)
    {
        foreach (var rule in rules ?? new List<AnimationObjectRule>())
        {
            if (rule == null) continue;
            var target = rule.target != null ? rule.target : FromRelativePath(rule.lastKnownPath);
            sb.Append("|anim-visibility:").Append(rule.lastKnownPath ?? "")
                .Append(":").Append(target == null ? "missing" : target.activeSelf.ToString());
        }
    }

    private static string AnimationObjectLayerName(string path)
    {
        byte[] hash;
        using (var sha = SHA256.Create())
            hash = sha.ComputeHash(Encoding.UTF8.GetBytes("ACM animation object:" + path));
        return OwnedFxPrefix + BitConverter.ToString(hash, 0, 5).Replace("-", "").ToLowerInvariant();
    }

    private static void ConfigureActivationTransition(AnimatorStateTransition transition,
        AnimationObjectSpec obj, bool matching)
    {
        transition.hasExitTime = false;
        transition.duration = 0f;
        if (obj.parameter == DanceParam)
            transition.AddCondition(matching ? AnimatorConditionMode.Equals :
                AnimatorConditionMode.NotEqual, obj.id, obj.parameter);
        else if (matching)
        {
            transition.AddCondition(AnimatorConditionMode.Greater, obj.id - .25f, obj.parameter);
            transition.AddCondition(AnimatorConditionMode.Less, obj.id + .25f, obj.parameter);
        }
        else throw new Exception("Float exit transitions require both opposite ranges.");
    }

    private static void BuildAnimationObjectFx(AnimatorController controller,
        string folder, List<AnimationObjectSpec> objects)
    {
        if (controller == null || objects == null || objects.Count == 0) return;
        var existingNames = new HashSet<string>(controller.layers.Select(l => l.name),
            StringComparer.Ordinal);
        foreach (var group in objects.GroupBy(o => o.path, StringComparer.Ordinal))
        {
            var rules = group.ToList();
            string parameter = rules[0].parameter;
            bool floatParam = parameter != DanceParam;
            var expectedType = floatParam ? AnimatorControllerParameterType.Float :
                AnimatorControllerParameterType.Int;
            if (!controller.parameters.Any(p => p.name == parameter))
                controller.AddParameter(parameter, expectedType);
            else if (controller.parameters.Any(p => p.name == parameter && p.type != expectedType))
                throw new Exception("FX Animator parameter '" + parameter +
                    "' has the wrong type for automatic animation objects.");

            var layerName = AnimationObjectLayerName(group.Key);
            if (!existingNames.Add(layerName))
                throw new Exception("An ACM FX layer name collided while assigning '" +
                    group.Key + "'. No avatar assignments were changed.");
            var sm = NewOwnedLayer(controller, layerName);
            var normal = sm.AddState("Original visibility");
            normal.writeDefaultValues = false;
            normal.motion = CreateObjectClip(folder, group.Key,
                rules[0].defaultActive, "ACM_Obj_Restore_" + layerName.Substring(OwnedFxPrefix.Length));
            sm.defaultState = normal;
            foreach (var rule in rules)
            {
                var state = sm.AddState("On " + rule.id + ": " + rule.label);
                state.writeDefaultValues = false;
                state.motion = CreateObjectClip(folder, group.Key,
                    rule.activeWhilePlaying,
                    "ACM_Obj_" + layerName.Substring(OwnedFxPrefix.Length) + "_" + rule.id);
                var toRule = sm.AddAnyStateTransition(state);
                toRule.canTransitionToSelf = false;
                ConfigureActivationTransition(toRule, rule, true);
                if (!floatParam)
                {
                    var reset = state.AddTransition(normal);
                    ConfigureActivationTransition(reset, rule, false);
                }
                else
                {
                    // Animator float conditions have no reliable NotEqual semantics.
                    // Two exit edges explicitly cover values below/above this ID.
                    var below = state.AddTransition(normal);
                    below.hasExitTime = false; below.duration = 0f;
                    below.AddCondition(AnimatorConditionMode.Less, rule.id - .25f, parameter);
                    var above = state.AddTransition(normal);
                    above.hasExitTime = false; above.duration = 0f;
                    above.AddCondition(AnimatorConditionMode.Greater, rule.id + .25f, parameter);
                }
            }
        }
        EditorUtility.SetDirty(controller);
    }
}

// ACM v0.9.2 preview: isolated, ACM-owned Base locomotion state graph.
// Build from the VRChat SDK or a user-supplied motion library, NEVER mutate it.
// v0.8.8 legacy Base-cloning path remains available and unchanged.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed partial class AvatarCreationManager
{
    private const float AcmProneUpright = 0.33f;
    private const float AcmStandUpright = 0.72f;

    private struct AcmCondition
    {
        internal readonly string parameter;
        internal readonly AnimatorConditionMode mode;
        internal readonly float value;
        internal AcmCondition(string name, AnimatorConditionMode condition, float threshold = 0f)
        { parameter = name; mode = condition; value = threshold; }
    }

    private static AcmCondition IsTrue(string p) => new AcmCondition(p, AnimatorConditionMode.If);
    private static AcmCondition IsFalse(string p) => new AcmCondition(p, AnimatorConditionMode.IfNot);
    private static AcmCondition Below(string p, float n) => new AcmCondition(p, AnimatorConditionMode.Less, n);
    private static AcmCondition Above(string p, float n) => new AcmCondition(p, AnimatorConditionMode.Greater, n);

    private static void AcmAddTransition(AnimatorStateMachine stateMachine, AnimatorState target,
        params AcmCondition[] conditions)
    {
        var transition = stateMachine.AddAnyStateTransition(target);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.12f;
        transition.canTransitionToSelf = false;
        foreach (var condition in conditions)
            transition.AddCondition(condition.mode, condition.value, condition.parameter);
    }

    private static AnimatorState AcmAddState(AnimatorStateMachine stateMachine,
        string name, Motion motion, Vector3 position)
    {
        var state = stateMachine.AddState(name, position);
        state.motion = motion;
        // No IK or temporary pose-space overrides. Those were explicitly rolled
        // back after v0.8.9 testing, and will not be reintroduced here.
        state.iKOnFeet = false;
        state.writeDefaultValues = false;
        EditorUtility.SetDirty(state);
        return state;
    }

    // In ACM-owned mode a motion library is not required to have VRChat proxy
    // placeholders. Resolve the posture by STATE identity (or tree name), then
    // use its entire Motion as the default branch of an ACM-owned selector.
    // Return null when no clearly identifiable posture exists; callers may then
    // use the official SDK motion library. Never choose a random proxy match.
    private static AnimatorState AcmFindStanceSource(AnimatorController source,
        string stance, string clipName)
    {
        if (source == null) return null;
        var candidates = source.layers.Where(l => l.stateMachine != null)
            .SelectMany(l => AllStates(l.stateMachine))
            .Where(s => s != null && s.motion != null).Distinct().ToList();
        string alias = stance == "Crouching" ? "Crouch" :
            stance == "Standing" ? "Stand" : stance;

        var named = candidates.Where(s =>
            string.Equals(s.name, stance, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.name, alias, StringComparison.OrdinalIgnoreCase)).ToList();
        if (named.Count == 0)
            named = candidates.Where(s =>
                s.name.StartsWith(stance + " ", StringComparison.OrdinalIgnoreCase) ||
                s.name.StartsWith(alias + " ", StringComparison.OrdinalIgnoreCase) ||
                s.name.StartsWith(stance + "_", StringComparison.OrdinalIgnoreCase)).ToList();
        if (named.Count == 0)
            named = candidates.Where(s => s.motion != null &&
                (string.Equals(s.motion.name, stance, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(s.motion.name, alias, StringComparison.OrdinalIgnoreCase))).ToList();
        if (named.Count > 0)
        {
            // Different state wrappers can point at the same BlendTree.
            if (named.Select(s => s.motion).Distinct().Count() == 1) return named[0];
            // One exact named state wins over generic locomotion matches.
            var exact = named.Where(s => string.Equals(s.name, stance,
                StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1) return exact[0];
            throw new Exception("ACM-owned locomotion found conflicting " + stance +
                " motion states: " + string.Join(", ", named.Select(s => s.name).Distinct().ToArray()) +
                ". Select an unambiguous Source Base. Your original controller was not edited.");
        }
        var byProxy = candidates.Where(s => s.motion is BlendTree tree &&
            CountClip(tree, clipName) > 0).ToList();
        if (byProxy.Count > 0 && byProxy.Select(s => s.motion).Distinct().Count() == 1)
            return byProxy[0];
        return null;
    }

    private static AnimatorController AcmFindSdkMotionLibrary()
    {
        var direct = AssetDatabase.LoadAssetAtPath<AnimatorController>(SampleBase);
        if (direct != null) return direct;
        // SDK samples can move inside package revisions. Only accept the exact
        // official file name inside the VRChat Avatars package, not arbitrary
        // similarly named controllers from the user's Assets or old ACM builds.
        foreach (var guid in AssetDatabase.FindAssets(
            "vrc_AvatarV3LocomotionLayer t:AnimatorController"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Packages/com.vrchat.avatars/", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(System.IO.Path.GetFileName(path),
                    "vrc_AvatarV3LocomotionLayer.controller", StringComparison.OrdinalIgnoreCase))
                continue;
            var candidate = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (candidate != null) return candidate;
        }
        return null;
    }

    private AnimatorController AcmResolveOwnedSourceBase(string originalPath,
        bool originalWasDefault)
    {
        // Explicit Source Base is a motion *library*. It must never be saved as
        // the avatar's ORIGINAL playable-layer assignment for later restoration.
        if (sourceBase != null) return sourceBase;
        if (!originalWasDefault)
        {
            var original = AssetDatabase.LoadAssetAtPath<AnimatorController>(originalPath);
            if (original != null) return original;
            throw new Exception("The saved original Base controller is missing at '" +
                originalPath + "'. Restore that asset before generating; ACM will not " +
                "replace an avatar whose original Base cannot be restored.");
        }
        var sdk = AcmFindSdkMotionLibrary();
        if (sdk != null) return sdk;
        throw new Exception("The avatar uses Default Base, but ACM cannot find the " +
            "VRChat SDK locomotion sample in Packages/com.vrchat.avatars. " +
            "Choose an Animator Controller in 'Source Base' (your own or SDK sample). " +
            "The avatar has not been changed.");
    }

    private static AnimatorState AcmResolvePostureMotion(AnimatorController source,
        string stance, string clipName)
    {
        var fromSource = AcmFindStanceSource(source, stance, clipName);
        if (fromSource != null) return fromSource;
        var sdk = AcmFindSdkMotionLibrary();
        if (sdk != null && sdk != source)
        {
            var fromSdk = AcmFindStanceSource(sdk, stance, clipName);
            if (fromSdk != null) return fromSdk;
        }
        throw new Exception("ACM-owned locomotion couldn't find a " + stance +
            " state with an animation in the selected motion library or the " +
            "VRChat SDK locomotion sample. Select a Source Base with a " + stance +
            " state. An animation does not have to be named '" + clipName + "'.");
    }

    private static Motion AcmOwnPoseMotion(Motion defaultMotion, List<Pose> poses,
        string parameter, string folder, ref int sequence)
    {
        // Nested source movement retains its own blend parameters. Selecting a
        // custom pose only switches the outer ACM parameter; no proxy animation
        // replacement or assumptions about its idle clip names are needed.
        Motion copiedDefault = AcmOwnMotion(defaultMotion, folder, ref sequence);
        if (poses == null || poses.Count == 0) return copiedDefault;
        var selector = new BlendTree {
            name = "ACM_PoseSelector_" + (sequence++),
            blendType = BlendTreeType.Simple1D,
            blendParameter = parameter,
            useAutomaticThresholds = false
        };
        AssetDatabase.CreateAsset(selector,
            BuildAssetPath(folder, BlendTreesSubfolder, selector.name + ".asset"));
        selector.AddChild(copiedDefault, 0f);
        foreach (var pose in poses.OrderBy(p => p.id))
            selector.AddChild(pose.clip, pose.id);
        EditorUtility.SetDirty(selector);
        return selector;
    }

    private static AnimatorState AcmFindMotionState(AnimatorController source,
        params string[] words)
    {
        var states = source.layers.Where(l => l.stateMachine != null)
            .SelectMany(l => AllStates(l.stateMachine))
            .Where(s => s != null && s.motion != null).ToList();
        // Names are only used to pick optional source motion templates; the new
        // controller never inherits their transitions or state behaviours.
        foreach (var term in words)
        {
            var match = states.FirstOrDefault(s =>
                string.Equals(s.name, term, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        foreach (var term in words)
        {
            var match = states.FirstOrDefault(s =>
                s.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            if (match != null) return match;
        }
        return null;
    }

    private static Motion AcmOwnMotion(Motion sourceMotion, string folder, ref int sequence)
    {
        // Source animations remain asset references. Every nested BlendTree is
        // reconstructed as an independent writable ACM asset.
        if (sourceMotion is BlendTree tree)
            return CopyTree(tree, folder, ref sequence);
        return sourceMotion;
    }

    private static void AcmCollectTreeParameters(Motion motion, HashSet<string> names)
    {
        if (!(motion is BlendTree tree)) return;
        if (!string.IsNullOrEmpty(tree.blendParameter)) names.Add(tree.blendParameter);
        if (tree.blendType == BlendTreeType.FreeformCartesian2D ||
            tree.blendType == BlendTreeType.FreeformDirectional2D ||
            tree.blendType == BlendTreeType.SimpleDirectional2D)
        {
            if (!string.IsNullOrEmpty(tree.blendParameterY)) names.Add(tree.blendParameterY);
        }
        foreach (var child in tree.children)
        {
            if (tree.blendType == BlendTreeType.Direct &&
                !string.IsNullOrEmpty(child.directBlendParameter))
                names.Add(child.directBlendParameter);
            AcmCollectTreeParameters(child.motion, names);
        }
    }

    private static void AcmAddRequiredParameters(AnimatorController controller,
        AnimatorController source, IEnumerable<Motion> motions)
    {
        var required = new HashSet<string>(StringComparer.Ordinal) {
            "Upright", "Grounded", "VelocityY", "AFK", "Seated",
            Parameters[0], Parameters[1], Parameters[2]
        };
        foreach (var motion in motions) AcmCollectTreeParameters(motion, required);
        foreach (var name in required.OrderBy(x => x))
        {
            if (controller.parameters.Any(p => p.name == name)) continue;
            AnimatorControllerParameterType type;
            if (name == "Grounded" || name == "AFK" || name == "Seated")
                type = AnimatorControllerParameterType.Bool;
            else
            {
                var fromSource = source.parameters.FirstOrDefault(p => p.name == name);
                type = fromSource != null ? fromSource.type : AnimatorControllerParameterType.Float;
            }
            // The movement graph requires numeric floats and built-in Booleans.
            if ((name == "Upright" || name == "VelocityY" || Parameters.Contains(name)) &&
                type != AnimatorControllerParameterType.Float)
                throw new Exception("ACM Locomotion parameter must be Float: " + name);
            if ((name == "Grounded" || name == "AFK" || name == "Seated") &&
                type != AnimatorControllerParameterType.Bool)
                throw new Exception("ACM Locomotion parameter must be Bool: " + name);
            controller.AddParameter(new AnimatorControllerParameter {
                name = name, type = type,
                defaultFloat = name == "Upright" ? 1f : 0f,
                defaultBool = name == "Grounded"
            });
        }
    }

    // Generates one complete ACM-owned Animator Controller instead of copying
    // an existing controller's states. Keeps original controller untouched and
    // eligible for ACM Restore Previous/Restore Original.
    private AnimatorController BuildAcmOwnedLocomotion(
        AnimatorController source, string controllerPath, string folder)
    {
        if (source == null)
            throw new Exception("ACM Locomotion needs an animation motion source.");

        var sampleStates = new AnimatorState[3];
        var sourceMotions = new List<Motion>();
        for (int i = 0; i < sampleStates.Length; i++)
        {
            sampleStates[i] = AcmResolvePostureMotion(source, StanceNames[i], IdleClipNames[i]);
            sourceMotions.Add(sampleStates[i].motion);
        }

        var jumpTemplate = AcmFindMotionState(source, "Jumping", "Jump", "Small Hop", "Hop");
        var fallTemplate = AcmFindMotionState(source, "Falling", "Fall", "Short Fall", "In Air");
        // Try the installed VRChat SDK sample for absent aerial motions, but
        // never substitute an idle/ground clip for a real Jump/Fall template.
        var sdkMotionLibrary = AcmFindSdkMotionLibrary();
        if (sdkMotionLibrary != null && sdkMotionLibrary != source)
        {
            if (jumpTemplate == null)
                jumpTemplate = AcmFindMotionState(sdkMotionLibrary,
                    "Jumping", "Jump", "Small Hop", "Hop");
            if (fallTemplate == null)
                fallTemplate = AcmFindMotionState(sdkMotionLibrary,
                    "Falling", "Fall", "Short Fall", "In Air");
        }
        if (jumpTemplate == null || fallTemplate == null)
            throw new Exception("ACM-owned locomotion needs Jump and Fall motions. " +
                "Choose a Source Base with them or install the VRChat SDK locomotion sample. " +
                "No avatar assets were assigned.");
        sourceMotions.Add(jumpTemplate.motion);
        sourceMotions.Add(fallTemplate.motion);

        var seatTemplate = AcmFindMotionState(source, "Sitting", "Seated", "Sit");
        var afkTemplate = AcmFindMotionState(source, "AFK");
        if (seatTemplate != null) sourceMotions.Add(seatTemplate.motion);
        if (afkTemplate != null) sourceMotions.Add(afkTemplate.motion);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if (controller == null || controller.layers.Length == 0)
            throw new Exception("Unity could not create the ACM Base Animator Controller.");
        controller.name = "ACM Locomotion";
        AcmAddRequiredParameters(controller, source, sourceMotions);

        var layer = controller.layers[0];
        layer.name = "ACM Locomotion";
        layer.defaultWeight = 1f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = new[] { layer };
        var machine = controller.layers[0].stateMachine;
        if (machine == null) throw new Exception("Unity failed to construct ACM's locomotion state machine.");
        machine.name = "ACM Base Locomotion";
        // Create a fresh state graph using the source only as a motion library.
        // The generated animation selectors are inserted into ACM-owned trees.
        int sequence = 0;
        List<Pose>[] allPoses = { standing, crouching, prone };
        var postures = new AnimatorState[3];
        for (int i = 0; i < 3; i++)
        {
            var state = AcmAddState(machine, "ACM " + StanceNames[i],
                AcmOwnPoseMotion(sampleStates[i].motion, allPoses[i], Parameters[i],
                    folder, ref sequence), new Vector3(260f + 240f * i, 110f, 0f));
            postures[i] = state;
        }
        // Never share a writable BlendTree from the original source. Copies of
        // optional source motions are separately owned by this build too.
        var jump = AcmAddState(machine, "ACM Jumping",
            AcmOwnMotion(jumpTemplate.motion, folder, ref sequence), new Vector3(260f, 330f, 0f));
        var fall = AcmAddState(machine, "ACM Falling",
            AcmOwnMotion(fallTemplate.motion, folder, ref sequence), new Vector3(520f, 330f, 0f));
        var seated = AcmAddState(machine, "ACM Seated",
            AcmOwnMotion(seatTemplate == null ? sampleStates[0].motion : seatTemplate.motion,
                folder, ref sequence), new Vector3(780f, 330f, 0f));
        var afk = AcmAddState(machine, "ACM AFK",
            AcmOwnMotion(afkTemplate == null ? sampleStates[0].motion : afkTemplate.motion,
                folder, ref sequence), new Vector3(1020f, 330f, 0f));
        machine.defaultState = postures[0];

        // Priority: AFK, stations, airborne and grounded posture. All edges are
        // interruptible, no exit-time dead ends, no permanent tracking overrides.
        AcmAddTransition(machine, afk, IsTrue("AFK"));
        AcmAddTransition(machine, seated, IsFalse("AFK"), IsTrue("Seated"));
        AcmAddTransition(machine, jump, IsFalse("AFK"), IsFalse("Seated"),
            IsFalse("Grounded"), Above("VelocityY", 0.05f));
        AcmAddTransition(machine, fall, IsFalse("AFK"), IsFalse("Seated"),
            IsFalse("Grounded"), Below("VelocityY", 0.05f));
        AcmAddTransition(machine, postures[2], IsFalse("AFK"), IsFalse("Seated"),
            IsTrue("Grounded"), Below("Upright", AcmProneUpright));
        AcmAddTransition(machine, postures[1], IsFalse("AFK"), IsFalse("Seated"),
            IsTrue("Grounded"), Above("Upright", AcmProneUpright),
            Below("Upright", AcmStandUpright));
        AcmAddTransition(machine, postures[0], IsFalse("AFK"), IsFalse("Seated"),
            IsTrue("Grounded"), Above("Upright", AcmStandUpright));

        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        return controller;
    }
}

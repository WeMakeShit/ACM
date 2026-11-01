// ACM v0.9.2 preview: isolated, ACM-owned Base locomotion state graph.
// Build from the VRChat SDK or a user-supplied motion library, NEVER mutate it.
// v0.8.8 legacy Base-cloning path remains available and unchanged.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

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
        // ACM locomotion is deliberately based on the canonical VRChat Base
        // template, not a creator's arbitrary Base graph. This gives every
        // generated avatar the same reliable Standing/Crouching/Prone/Jump/Fall
        // layout. The avatar's original Base assignment remains in the manifest
        // and is never changed on disk, so Restore can return to it.
        var sdk = AcmFindSdkMotionLibrary();
        if (sdk != null) return sdk;
        throw new Exception("ACM cannot find VRChat's vrc_AvatarV3LocomotionLayer.controller " +
            "inside Packages/com.vrchat.avatars. Reinstall or repair the VRChat Avatars SDK; " +
            "no avatar assets were changed.");
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

    // LocomotionFIX's internal VR stand idle is a neutral humanoid pose. ACM
    // creates the equivalent owned proxy only when a source controller omitted
    // VRChat's normal proxy_stand_still. VRChat replaces this recognised proxy
    // name in-client; no third-party controller or animation asset is required.
    private static AnimationClip AcmCreateStandStillFallback(string folder)
    {
        // This is an actual ACM package asset, not a project-path lookup. Every
        // VCC install therefore receives the supplied default standing animation.
        var supplied = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Packages/com.wemakeshit.acm/Locomotion/StandAnim_NewUpdate.anim");
        if (supplied != null && !supplied.legacy) return supplied;
        const string name = "proxy_stand_still";
        string path = BuildAssetPath(folder, AnimationsSubfolder, "ACM_" + name + ".anim");
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) return existing;
        var clip = new AnimationClip { name = name };
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimationClip AcmFindZeroThresholdIdle(BlendTree tree)
    {
        if (tree == null) return null;
        foreach (var child in tree.children)
        {
            if (!Mathf.Approximately(child.threshold, 0f)) continue;
            if (child.motion is AnimationClip clip) return clip;
            var nested = AcmFindZeroThresholdIdle(child.motion as BlendTree);
            if (nested != null) return nested;
        }
        return null;
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
            "TrackingType",
            Parameters[0], Parameters[1], Parameters[2]
        };
        foreach (var motion in motions) AcmCollectTreeParameters(motion, required);
        foreach (var name in required.OrderBy(x => x))
        {
            if (controller.parameters.Any(p => p.name == name)) continue;
            AnimatorControllerParameterType type;
            if (name == "Grounded" || name == "AFK" || name == "Seated")
                type = AnimatorControllerParameterType.Bool;
            else if (name == "TrackingType") type = AnimatorControllerParameterType.Int;
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

    // VRChat changes TrackingType as a player moves between desktop, 3-point,
    // hip, feet and full-body tracking. Keep the original head/hands untouched;
    // ACM only tells its locomotion layer whether hip/feet should follow tracking
    // or generated locomotion. This prevents 3-point users from losing lower
    // body locomotion while retaining hip/foot tracking for FBT users.
    private static void AcmConfigureTracking(AnimatorState state, bool hipTracking,
        bool feetTracking)
    {
        var control = state.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
        control.trackingHead = VRCAnimatorTrackingControl.TrackingType.NoChange;
        control.trackingLeftHand = VRCAnimatorTrackingControl.TrackingType.NoChange;
        control.trackingRightHand = VRCAnimatorTrackingControl.TrackingType.NoChange;
        control.trackingHip = hipTracking ? VRCAnimatorTrackingControl.TrackingType.Tracking :
            VRCAnimatorTrackingControl.TrackingType.Animation;
        control.trackingLeftFoot = feetTracking ? VRCAnimatorTrackingControl.TrackingType.Tracking :
            VRCAnimatorTrackingControl.TrackingType.Animation;
        control.trackingRightFoot = feetTracking ? VRCAnimatorTrackingControl.TrackingType.Tracking :
            VRCAnimatorTrackingControl.TrackingType.Animation;
    }

    private static void AcmAddTrackingGuard(AnimatorController controller)
    {
        var guard = NewOwnedLayer(controller, "ACM Tracking Guard");
        var unknown = AcmAddState(guard, "Tracking initializing", null, new Vector3(100, 40));
        var threePoint = AcmAddState(guard, "3-point / desktop", null, new Vector3(340, 40));
        var hip = AcmAddState(guard, "Hip tracking", null, new Vector3(580, 40));
        var feet = AcmAddState(guard, "Feet tracking", null, new Vector3(820, 40));
        var full = AcmAddState(guard, "Full-body tracking", null, new Vector3(1060, 40));
        AcmConfigureTracking(unknown, false, false);
        AcmConfigureTracking(threePoint, false, false);
        AcmConfigureTracking(hip, true, false);
        AcmConfigureTracking(feet, false, true);
        AcmConfigureTracking(full, true, true);
        guard.defaultState = unknown;
        AcmAddTransition(guard, threePoint, new AcmCondition("TrackingType", AnimatorConditionMode.Equals, 3));
        AcmAddTransition(guard, hip, new AcmCondition("TrackingType", AnimatorConditionMode.Equals, 4));
        AcmAddTransition(guard, feet, new AcmCondition("TrackingType", AnimatorConditionMode.Equals, 5));
        AcmAddTransition(guard, full, new AcmCondition("TrackingType", AnimatorConditionMode.Equals, 6));
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
        for (int i = 0; i < sampleStates.Length; i++)
            sampleStates[i] = AcmResolvePostureMotion(source, StanceNames[i], IdleClipNames[i]);
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath) || !AssetDatabase.CopyAsset(sourcePath, controllerPath))
            throw new Exception("ACM could not create a protected copy of the Base controller.");
        // CopyAsset can return before nested Animator state-machine subassets are
        // queryable. Import synchronously before resolving the copied posture
        // states; otherwise valid controllers intermittently appear to have no
        // Standing state and leave an empty staging folder on aborted builds.
        AssetDatabase.ImportAsset(controllerPath, ImportAssetOptions.ForceSynchronousImport);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) throw new Exception("ACM could not load its copied Base controller.");
        // Unity requires the main controller object to match its .controller
        // filename. The generated file is named ACM Locomotion.controller.
        controller.name = "ACM Locomotion";

        // The copied VRChat template contains movement parameters, but not
        // ACM's APM_Standing/APM_Crouching/APM_Prone selector parameters.
        // Without these, menu selections change Expression Parameters yet the
        // generated BlendTrees remain at zero in the live client.
        AcmAddRequiredParameters(controller, source,
            sampleStates.Where(s => s != null).Select(s => s.motion));

        // A fully recreated state machine looked simpler but silently discarded
        // SDK/creator movement logic, causing the avatar to glide without walk
        // or run animation. ACM instead owns a private controller copy: all
        // original locomotion states, movement parameters, transitions and SDK
        // behaviours stay intact. When a stance contains its normal proxy idle,
        // inject ACM's selector into that idle branch only. This is the important
        // locomotion-fix pattern: Default Base keeps proxy_stand_still as slot 0
        // while walking/running remains driven by the surrounding source tree.
        // Unusual custom trees without a known idle still receive a safe outer
        // selector instead of being rejected or guessed.
        int sequence = 0;
        List<Pose>[] allPoses = { standing, crouching, prone };
        for (int i = 0; i < 3; i++)
        {
            var copiedState = AcmFindStanceSource(controller, StanceNames[i], IdleClipNames[i]);
            if (copiedState == null)
            {
                // Prefer the exact source state name when the source was a
                // creator controller with a non-standard state naming scheme.
                copiedState = controller.layers.Where(l => l.stateMachine != null)
                    .SelectMany(l => AllStates(l.stateMachine)).FirstOrDefault(s => s != null &&
                        string.Equals(s.name, sampleStates[i].name, StringComparison.OrdinalIgnoreCase) &&
                        s.motion != null);
            }
            if (copiedState == null)
                throw new Exception("ACM couldn't find copied " + StanceNames[i] +
                    " locomotion state after importing the generated controller. " +
                    "This Base layout is unsupported; original assets were not changed.");
            var tree = copiedState.motion as BlendTree;
            if (tree != null && CountClip(tree, IdleClipNames[i]) > 0)
                PatchStance(copiedState, IdleClipNames[i], Parameters[i], allPoses[i],
                    folder, ref sequence);
            else if (tree != null && i == 0 && AcmFindZeroThresholdIdle(tree) != null)
            {
                // Preserve the source locomotion tree and every non-idle branch.
                // Only substitute its identifiable idle leaf with the generated
                // proxy selector, so standing uses proxy_stand_still by default.
                var cloned = CopyTree(tree, folder, ref sequence);
                var sourceIdle = AcmFindZeroThresholdIdle(cloned);
                var selector = Selector(folder, Parameters[i],
                    AcmCreateStandStillFallback(folder), allPoses[i], ref sequence);
                if (sourceIdle == null || !ReplaceClip(cloned, sourceIdle.name, selector))
                    throw new Exception("ACM couldn't install the stand-still fallback safely.");
                copiedState.motion = cloned;
            }
            else
                copiedState.motion = AcmOwnPoseMotion(sampleStates[i].motion, allPoses[i],
                    Parameters[i], folder, ref sequence);
            copiedState.writeDefaultValues = false;
            EditorUtility.SetDirty(copiedState);
        }
        // The official VRChat Base template already contains the tracking and
        // locomotion behaviour required for desktop, half-body and full-body.
        // Do not add a second unmasked override layer here: it can suppress the
        // Base pose in the live client and leave an avatar frozen.
        EditorUtility.SetDirty(controller);
        return controller;
    }
}

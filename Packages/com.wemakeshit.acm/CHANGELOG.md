## 0.9.3 — material selectors & universal menu destinations

- Int material selectors now use one generated FX layer with one state per value, so every value—including 0 and 3—can switch directly without other selector layers writing an OFF material over it.
- Adds explicit Int values for selector options, including using value 0 as a real material variant.
- Adds one-click material-variant creation for an existing material selector.
- Menu Destination now discovers the root Expressions Menu and every existing nested submenu, not only Appearance. ACM safely clones only the selected menu branch into the generated build before adding controls.
- Adds validation for duplicate selector values, mixed selector value modes, renderer material-slot bounds, and missing swap materials.

## 0.9.2 — stable release

- Publishes the completed ACM 0.9.2 feature set as a normal VCC release rather than a pre-release, so VCC selects it automatically.

## 0.9.2-preview.20 — credit order

- Places the main-menu Creator credit entry immediately before Avatar Creation Manager in the generated root menu order.

## 0.9.2-preview.19 — main-menu creator credit

- Places the optional Creator credit button directly on the avatar's main Expressions menu, alongside the avatar's existing main controls, rather than inside the ACM submenu.

## 0.9.2-preview.18 — action-wheel creator credit

- Replaces the proposed object-toggle credit with an optional information-only action-wheel button inside the generated ACM submenu.
- The button shows the creator name and selected branding image, creates no FX layer, toggles no object, and uses no Expression Parameter.

## 0.9.2-preview.17 — optional Creator Protection

- Adds an opt-in Creator Protection section with a dated local `CreatorProof.json` receipt for each generated ACM build.
- Adds an optional visible creator-credit toggle: select a badge/text/logo child object, enter a creator name, and choose an action-wheel branding icon.
- The credit uses ACM's ordinary safe object-toggle wiring (one synced Bool only when enabled); no DRM, obfuscation, locomotion changes, or hidden parameter cost is added.

## 0.9.2-preview.16 — pose selector parameter fix
- Adds ACM's Standing, Crouching and Prone selector parameters to the generated `ACM Locomotion` controller.
- Fixes pose menu selections not switching the generated pose BlendTrees in VRChat.

## 0.9.2-preview.15 — live-client locomotion regression fix
- Removes the extra ACM Tracking Guard override layer, which could suppress the official Base pose and freeze locomotion in VRChat.
- Relies on VRChat's official Base template for desktop, half-body and full-body tracking behaviour.

## 0.9.2-preview.14 — simpler beginner workflow
- Adds clear readiness status, an ACM setup check, live ACM-toggle parameter estimate and generated-build asset counts.
- Moves original FX/Action/Menu/Parameter selectors and recovery behind an Advanced foldout while retaining them for compatibility.
- Adds quick templates for object toggles, material swaps and hue/float shader changes.
- Renames the main action to Generate / Update ACM.

## 0.9.2-preview.13 — controller-name warning fix
- Names the generated file and main object `ACM Locomotion`, matching Unity's required filename convention.
- Existing builds named `Locomotion.controller` remain recognized during Update and Restore.

## 0.9.2-preview.12 — automatic VRChat Base template
- ACM-owned locomotion now always uses the installed `vrc_AvatarV3LocomotionLayer.controller` as its source template.
- The Source Base field is automatic/read-only; users no longer need to assign the VRChat controller manually.
- The original avatar Base remains recorded for Restore but no longer blocks generation when it has an unusual or missing layout.

## 0.9.2-preview.11 — copied-controller import fix
- Forces Unity to synchronously import the generated Base-controller copy before ACM resolves its locomotion states.
- Adds an exact source-state-name fallback for non-standard controller layouts, fixing the false “couldn't find copied Standing locomotion state” build abort.

## 0.9.2-preview.10 — automatic locomotion migration
- Removes the legacy strict proxy-only pose generator from normal pose builds.
- Existing profiles now automatically use ACM's protected Base-controller copy, so a custom Base without `proxy_stand_still` no longer aborts validation before fallback handling can run.

## 0.9.2-preview.9 — bundled default standing animation
- Bundles `Locomotion/StandAnim_NewUpdate.anim` inside ACM.
- ACM uses this supplied animation as the default Standing fallback when it needs to construct a missing stand-still idle branch; no project-path lookup is required.

## 0.9.2-preview.8 — supplied standing fallback
- Automatically uses an unambiguous, non-Legacy `StandAnim_NewUpdate` clip from the Unity project as ACM's stand-still fallback.
- Falls back to ACM's generated `proxy_stand_still` when the supplied clip is not present or ambiguous.

## 0.9.2-preview.7 — stand-still fallback
- Adds an ACM-generated neutral `proxy_stand_still` fallback based on the LocomotionFIX reference's neutral VR stand-idle behavior.
- If Standing has no named proxy idle but has an identifiable zero-threshold idle branch, ACM replaces only that idle leaf and retains the source walk/run blend tree.

## 0.9.2-preview.6 — generated idle-slot locomotion
- Uses the locomotion-fix reference pattern without bundling its third-party controllers or animations.
- For Default/Base controllers containing VRChat proxy idles, ACM inserts its generated pose selector only at the idle branch (for example `proxy_stand_still`), preserving the surrounding walk/run blend tree.
- Custom Base controllers without a recognizable proxy idle retain the safe whole-stance fallback and are never modified in place.

## 0.9.2-preview.5 — two-profile rotation
- Adds an explicit Create New Profile workflow: the current per-avatar profile becomes the clearly labelled Backup profile.
- Keeps at most two profiles for each avatar: Active and Backup. A later new profile replaces only that avatar's older backup.
- Adds Restore Backup Profile. Original restoration manifests and generated assets are not deleted by profile rotation.

## 0.9.2-preview.4 — descriptor pose-slot preservation
- Protects the Avatar Descriptor Sitting, T-Pose and IK Pose controller assignments during ACM generation and updates.
- ACM deliberately does not replace these calibration/special-pose slots; if an internal build would alter one, generation stops before assigning the avatar.

## 0.9.2-preview.3 — locomotion movement repair
- Fixes the experimental owned-locomotion graph that could make an avatar glide without walk/run animation.
- ACM now creates a protected owned copy of the complete original/SDK Base controller, preserving movement parameters, transitions, jump/fall and required SDK behaviours.
- Only Standing, Crouching and Prone motions are replaced by ACM's generated pose selector blend trees; the original Base asset remains untouched and restorable.

## 0.9.2-preview.2 — legacy ACM repair preview
- Restores the v0.9.2 preview package as the implementation base, retaining its existing banner, icons, pose/dance, category, pagination, recovery and object-toggle workflows.
- ACM-owned locomotion now adds a tracking guard for desktop/3-point, hip, feet and full-body tracking while leaving head and hand tracking untouched.
- Adds Bool controls for independent simultaneous effects and Int selector groups with an explicit Off action for mutually exclusive choices.
- Adds generated material-swap, material-colour and float-property clips alongside existing object visibility and user-supplied animation clips.
- Existing saved toggle profiles remain compatible; legacy manual clips and automatic visibility entries migrate to their corresponding effect modes.
- This is a development preview. Unity editor and in-client VRChat testing are still required before publication; stable v0.8.8 remains untouched.

## 0.9.2-preview.1 — Correct development version numbering
- Carries forward the latest ACM-owned locomotion motion-source compatibility work from 0.9.1-preview.3.
- Version number and package metadata updated consistently for testing.
- No behavioral changes to pose, dance, object-toggle, pagination, or controller generation logic in this version-only update.
- Unity and VRChat locomotion tests are still required. Published stable release remains v0.8.8.

## 0.9.1-preview.3 — ACM-owned locomotion motion-source compatibility
- Owned locomotion now resolves posture movement from Standing, Crouching, and Prone states or clearly named motion trees, without requiring `proxy_crouch_still` or other proxy clips.
- It reconstructs complete source posture motions under ACM-owned pose-selector BlendTrees, preserving source movement blending without modifying the source.
- Missing posture and aerial templates may use the installed VRChat SDK locomotion sample; missing assets produce actionable errors instead of guessing an unrelated animation.
- When Base is Default, ACM can locate the canonical SDK sample by its filename even if the sample path has moved.
- Source Base is kept separate from the original Base restoration record for owned mode. Existing generated assets and the published v0.8.8 code path are not rewritten.
- Experimental: Unity/VRChat live validation is required, and missing original controllers still block unsafe builds.

## 0.9.1-preview.2 — ACM-owned locomotion source-state resolver fix
- Fixed a false “Ambiguous Standing idle” build abort when the VRChat source controller references `proxy_stand_still` from multiple states.
- ACM-owned mode selects the canonical Standing / Crouching / Prone state and its motion tree first. Reused proxy clips or repeated references to one tree are valid.
- Genuinely different, indistinguishable trees still abort safely; original avatar assets remain unmodified.
- Published v0.8.8 legacy locomotion generation, object-toggle behavior, menu pagination, icons and animation clips are unchanged.
- Experimental: needs real Unity/VRChat tests before release.

## 0.9.1-preview.1 — Experimental ACM-owned locomotion

- Optional new ACM-owned Base Animator graph and independent pose blend trees.
- Reuses the existing configured poses and VRChat locomotion motion templates, retaining legacy v0.8.8 mode.
- Does not import custom source transition graphs or replace original controller files.
- Requires Unity/VRChat tests; not a confirmed floor-sinking fix.

# Changelog

## 0.8.8 — Object visibility with animations

- Optional per-pose and per-dance GameObject visibility (ON/OFF while the selected animation parameter is active).
- Generated object-visibility curves are placed in a separate FX layer, leaving humanoid clips and the protected v0.8.7 pagination unchanged.
- Preserve rules in ACM profiles; validate avatar-relative object paths and protect against manual FX and ACM toggle conflicts.
- Objects revert to the GameObject active state from the Unity scene when the associated animation selection is cleared.
- Known limitation: for poses, visibility tracks the selected pose parameter, which may remain set when physical stance changes. Reusing one object across different pose/dance categories is not yet supported.
- Known limitation: some crouching, prone and dance animations may sink into the floor due to locomotion, root/hip motion or IK interaction. The experimental v0.8.9 IK overrides and v0.9.0 root-height changes are not included in this release.

## 0.8.7 — Correct eight-slot pose and dance pagination
- Eight user-added poses fit in one menu with no Next Page; the ninth triggers pagination.
- Default reset is shown for 1–7 poses only. With 8+ poses, turning off a pose toggle resets the parameter to 0.
- Dances use the same eight-control threshold and do not create early pages.
- Validate generated pages for overflow, null controls, and missing submenu references.
- Existing generated menu assets remain unchanged until Generate / Update is run.

## 0.8.6 — Reliable Poses / Dances submenu icons
- Recover icons when existing category submenus are copied into ACM-generated assets.
- Add Apply Missing Poses / Dances Icons to Current ACM Build.
- Show whether both bundled default images were imported by Unity.
- Prevent Smart Engine from treating menus with missing expected icons as Already Up to Date.
- Preserve manually selected icons and source menus.

## 0.8.5 — Official Poses and Dances submenu icons
- Bundle `ACM Poses.png` and `ACM Dances.png` with their Unity meta files.
- Look up the exact filenames and preserve already-assigned icons.

## 0.8.4 — Poses and Dances submenu icon recovery
- Recover icons from the installed package, legacy `Assets/ACM/Icons`, or a prior generated menu when possible.
- Assign icons on reused submenus when an icon field is empty.

## 0.8.3 — Original-source recovery
- Persist original Root Menu, Parameters, FX, and Action reference selectors in the per-avatar profile.
- Recover those fields from verified recorded references; do not guess missing assets.
- Restore Previous reloads original selectors when trusted records exist.

## 0.8.2 — Initial VCC package
- Convert ACM into `com.wemakeshit.acm` for VCC/VPM.

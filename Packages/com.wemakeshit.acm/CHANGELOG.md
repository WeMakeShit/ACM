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

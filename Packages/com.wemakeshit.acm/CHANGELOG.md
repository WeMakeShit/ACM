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

# Avatar Creation Manager (ACM) — By DTAA

Unity 2022.3 editor utility for VRChat avatars. Includes pose, dance and toggle generators, menu organization, Smart Engine history, and a separate About window with Ko-fi support.

## VCC edition

This package is meant to be installed through VRChat Creator Companion (VCC). **VCC manages updates.** ACM does not download or overwrite editor scripts in this edition.

**User files:** ACM keeps its generated avatar assets, profiles and Smart Engine history under `Assets/AvatarPoseManager/` (including `Profiles/` and `Generated/`). These stay intact when VCC replaces the package.

**Icons and banner:** Bundled under `Packages/com.wemakeshit.acm/` and loaded through package-aware Unity asset paths. New icons supplied by the avatar creator override defaults.

**Support:** https://ko-fi.com/wemakeshit

## Important migration warning

Never have `Assets/ACM/Editor/AvatarCreationManager.cs` and the VCC package installed together. Back up the project, then remove the **legacy editor scripts** after validating that ACM's writable data remains safe. Do not use VPM `legacyFolders` to delete `Assets/ACM` automatically, because user files may exist there.

This is an unverified VCC preview release of ACM v0.8.3. Unity compilation, VCC install/uninstall and VRChat avatar generation have **not** been live-tested in this environment. Test in a disposable copy of an avatar project before public release.

## v0.8.3 original-reference recovery

ACM now saves the four original asset selector fields into its per-avatar profile and provides a recovery action that only uses verified historical references. Existing original files cannot be reconstructed if deleted. Back up your project and verify on a duplicate avatar before publishing.

## Poses / Dances submenu icons (v0.8.6)

The bundled `Icons/ACM Poses.png` and `Icons/ACM Dances.png` are looked up by exact filename. If the main ACM interface is showing empty Poses/Dances submenu icons after updating:

1. Confirm the current Unity project uses package `com.wemakeshit.acm` version 0.8.6, not an old copied zip. GitHub upload does NOT update a local embedded copy.
2. In ACM expand **Menu / Folder Icons (optional)**; the labels **Bundled Poses icon** and **Bundled Dances icon** should say **Found**.
3. With the avatar selected, click **Apply Missing Poses / Dances Icons to Current ACM Build**. This modifies only missing icons on currently assigned ACM-generated menu assets; it never overwrites a manually assigned icon or a source menu.
4. If the labels say NOT FOUND, inspect Unity's `Packages/com.wemakeshit.acm/Icons` folder and reimport the missing image; do not run Generate to fix an asset that's not imported.
5. If a submenu is not found, verify the avatar currently points to the ACM-generated Expressions Menu in the Avatar Descriptor.

The normal Generate / Update process now handles both new and reused submenu controls. All changes require Unity live testing before release.

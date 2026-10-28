# Avatar Creation Manager (ACM) — By DTAA

Unity 2022.3 editor utility for VRChat avatars. Includes pose, dance and toggle generators, menu organization, Smart Engine history, and a separate About window with Ko-fi support.

## VCC edition

This package is meant to be installed through VRChat Creator Companion (VCC). **VCC manages updates.** ACM does not download or overwrite editor scripts in this edition.

**User files:** ACM keeps its generated avatar assets, profiles and Smart Engine history under `Assets/AvatarPoseManager/` (including `Profiles/` and `Generated/`). These stay intact when VCC replaces the package.

**Icons and banner:** Bundled under `Packages/com.wemakeshit.acm/` and loaded through package-aware Unity asset paths. New icons supplied by the avatar creator override defaults.

**Support:** https://ko-fi.com/wemakeshit

## Important migration warning

Never have `Assets/ACM/Editor/AvatarCreationManager.cs` and the VCC package installed together. Back up the project, then remove the **legacy editor scripts** after validating that ACM's writable data remains safe. Do not use VPM `legacyFolders` to delete `Assets/ACM` automatically, because user files may exist there.

This is a pre-release VCC conversion of ACM v0.8.1. Unity compilation, VCC install/uninstall and VRChat avatar generation have **not** been live-tested in this environment. Test in a disposable copy of an avatar project before public release.

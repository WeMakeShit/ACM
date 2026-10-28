# Avatar Creation Manager (ACM) — By DTAA

**Version 0.8.8** · Unity 2022.3 · VRChat avatar editor extension · Published by DTAA / WeMakeShit

ACM helps creators organize VRChat avatar poses, dances, toggles, and expression menus. It includes automatic menu pagination, configurable icons, Smart Engine validation, previous-setup recovery, and an About window.

## Install and update with VRChat Creator Companion (VCC)

Add the published ACM VPM repository to VCC, then install **Avatar Creation Manager** in a VRChat avatar project. VCC handles future package updates. This VCC edition does not auto-download or overwrite its own Editor scripts.

**For manual local testing:** Place only `com.wemakeshit.acm` in the Unity project's `Packages/` directory. Do **not** put a downloaded GitHub repository or nested Unity project under `Assets/`.

**Do not install ACM twice:** Avoid keeping legacy `Assets/ACM/Editor/AvatarCreationManager*.cs` scripts alongside the VCC package. Back up your project before removing legacy Editor scripts.

## Project data and safety

ACM writes generated avatar assets and profiles to `Assets/AvatarPoseManager/`, including `Generated/` and `Profiles/`. Those files live in the user project, not in the VCC package. Do not delete them when upgrading ACM.

Smart Engine history and **Restore Previous** restore recorded references and settings when possible; they are **not** full byte-for-byte backups of overwritten or deleted Unity assets. Always back up your Unity project before regenerating a production avatar.

## Menus and bundled icons

- Bundled icon PNGs are in `Icons/`, including `ACM Poses.png` and `ACM Dances.png`.
- Manually chosen icons take priority over bundled defaults.
- Pose/dance menus allow **8 entries without a Next Page button**; with 9 or more entries, ACM paginates while reserving a slot for navigation. For a full eight-pose menu the pose-off state resets its parameter to zero rather than requiring an additional Default menu control.
- For already-generated avatars, use **Apply Missing Poses / Dances Icons to Current ACM Build** to repair eligible missing category icons without regenerating all controls.
- Generated menu assets are not changed by updating the package; use Generate / Update on a backup or duplicate avatar to apply updated menu logic.

## Testing and support

**v0.8.8 is the public release.** The maintainer has confirmed the animation object toggles work in Unity. Select a pose or dance, expand **Objects with this animation (optional)**, and add child GameObjects to enable (ON) or disable (OFF) while that pose/dance's menu parameter is selected. ACM writes animation visibility only into new generated FX layers. It restores each object's original scene active state when the selection is cleared. Avoid assigning the same object to multiple pose/dance categories or to an existing manual FX toggle. Changing stance without clearing a pose selection can leave its prop shown; test on a duplicate avatar. Known limitation: ACM does not yet correct all floor-sinking issues in unusual crouching, prone, or dance animations. Experimental IK and root-height overrides from subsequent test branches are not included.

The maintainer has live-tested v0.8.7's eight-pose Standing menu in Gesture Manager and reports it opens successfully; Crouch and Prone also open. This is not a guarantee for all avatar setups. Test on a duplicate avatar and check Unity Console before publishing an avatar.

Support: https://ko-fi.com/wemakeshit  
Source: https://github.com/WeMakeShit/ACM

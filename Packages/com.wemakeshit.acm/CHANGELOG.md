## 0.8.7 — Correct eight-slot pose and dance pagination
- Eight user-added poses fit in one menu with no Next Page; the ninth triggers pagination.
- Default reset is shown for 1–7 poses only. With 8+ poses, turning off a pose toggle resets the parameter to 0.
- Dances now use the same eight-control threshold and never create early pages.
- Validates every generated page for overflow, null controls and missing submenu references.
- Existing menus and avatar assets are not changed by installing the package; run Generate/Update on a duplicate avatar to apply.

# Changelog

## 0.8.6 — Reliable Poses / Dances submenu icons
- Repair icon assignment when existing category submenus are copied into generated ACM assets.
- Add **Apply Missing Poses / Dances Icons to Current ACM Build** to restore icons without regenerating locomotion, dances, toggles or parameters.
- Show whether both bundled default images are imported in Unity.
- Prevent Smart Engine from treating a generated menu with missing Poses/Dances icons as Already Up to Date.
- Validate new generated menus before committing when the required icons are available; failed validation rolls back.
- Preserve manually selected icons and never edit original avatar menus or controllers.

# Changelog

## 0.8.5 — Official Poses and Dances submenu icons
- Added the two supplied transparent images: `ACM Poses.png` and `ACM Dances.png`.
- Uses the existing exact-name icon lookup for the Poses/Dances submenu controls.
- Preserves manually assigned and already-existing icons; no unrelated art is substituted.
- No changes to pose, dance, toggle, Smart Engine or controller generation logic.

## 0.8.4 — Poses and Dances submenu icon recovery
- Reuses matching `ACM Poses.png` and `ACM Dances.png` from either VCC package Icons or previous `Assets/ACM/Icons` folders if present.
- Preserves a prior generated Poses/Dances submenu icon when rebuilding from an installed ACM avatar.
- Applies selected icons when ACM reuses its generated Poses/Dances submenu with an empty icon field, without overwriting existing icons.
- No new artwork generated or unrelated icons substituted.

# Changelog

## 0.8.3 — Original-source recovery hotfix (TEST BUILD)
- Saves Original Root Menu, Original Parameters, Original FX and Original Action selectors in ACM's per-avatar profile.
- Restores these fields from verified profile, original manifest or Smart Engine history, without making guesses.
- Adds a Recover Original Fields from ACM History button, without changing the avatar descriptor or assets.
- Restore Previous also reloads the original selectors when trusted records are available.

## 0.8.2
- Initial VCC package conversion.

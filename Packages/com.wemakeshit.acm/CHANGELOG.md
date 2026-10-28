# Changelog

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

# Visual review

The user approved the dark Diablo-like interface direction. Approval of the direction does not approve every generated asset.

## Concept

Disposition: reviewed design reference. Never embedded as an application background.

- Equipment and inspector form a clear focal path; restrained brass and ivory establish hierarchy.
- Findings: navigation selection does not match the equipment page; inventory dimensions are illustrative rather than native; some generated property text is incorrect. These pixels must not become runtime content.
- Runtime requirement: profile-derived grids, selected page, real item state and native text controls replace every illustrative data element.

## Logo

Status: approved. First candidate rejected for outward blade-like feet, unnecessary noise and a silhouette too close to familiar fantasy crests. Refined candidate uses flat arch supports and one amber seed. Transparent margins and silhouette checked. The 32/64 px application icon retains both supports, clear negative space and the separate seed. Native header placement and the larger navigation mark were inspected in the actual WPF render.

## Inventory objects

Status: approved after the corrections recorded below. 24 illustrations inspected for counts, silhouette and material continuity. Transparent alpha confirmed by pixel inspection. The generated rows are not perfectly256px high, so runtime rectangles follow each actual object rather than blindly slicing equal cells. No names, values or rarity state are baked into the art.

## Class portraits — rejected

The eight generated faces have uniform frontal presentation, similarly dramatic lighting, overworked clothing detail and generic fantasy characterization. The user identified the same weakness. They are excluded from production resources. Hand-authored class vector emblems replace them; no further portrait generation is planned.

## Independent runtime crop review

Status: findings closed after rectangle correction and a second inspection of all25 generated crops. Structural audit PASS alone is not visual approval.

- `kiteShield`: the former runtime top at 531 clipped the source silhouette beginning at 517. Its central top apex lost 14 px.
- `brownBook`: the former runtime top at 761 clipped the source beginning at 754. The upper cover corner lost 7 px. `javelin` also lost 1 px left and 3 px bottom; `axe`, `blueBook`, and `grandCharm` each lost 1 px at a tip.
- `hood`: its former rectangle extended 16 px below its own silhouette and included the next shield's apex. The same row slicing error introduced book fragments below `gloves` and smaller fragments in `roundShield`, `boots`, `mask`, and `plateArmor`.
- Correction applied: union each object's connected visible alpha silhouette, include both separate boot components, add 2 px padding, and exclude adjacent object silhouettes. Both PNG sources retain their original SHA256 hashes.

Semantic checks passed independently: 24 item categories, two gloves, two boots, a visibly open transparent ring hole, continuous bow string and axe/scythe handles, separate blue/brown books, one amber drop inside the logo arch, and no baked text or mutable values. The embedded 32 px and 64 px ICO frames retain the arch/drop silhouette at native size.

The corrected 24 rectangles contain all 25 connected visible components with no clipping and zero intersections with any foreign component bounding box. Alpha above 16/255 defines the visible silhouette for this measurement; faint alpha below that threshold links some source shadows between rows and is not used to assign an object. Every refreshed crop was also inspected on the native dark background. The shield apex and book corners are intact, the glove and hood neighbors are gone, and the ring hole remains transparent. This review covers these assets only; application layout and interaction approval are separate.

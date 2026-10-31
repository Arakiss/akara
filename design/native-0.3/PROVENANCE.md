# Native identity and artwork

The 0.3 interface is implemented in WPF. The concept image is a design reference; no generated screen or generated text is embedded in the application.

The built-in image generation tool was used for the raster assets below. The CLI/API fallback was not used. Exact prompts are preserved alongside this file.

| Asset | Production file | Prompt and disposition |
| --- | --- | --- |
| Open arch and amber seed | `src/Akara.Desktop/Assets/Art/akara-mark.png` | `logo-prompt.txt`, followed by `logo-refinement-prompt.txt`. Reviewed and approved after removing the first candidate's blade-like feet. |
| 24 inventory illustrations | `src/Akara.Desktop/Assets/Art/items-atlas.png` | `item-atlas-prompt.txt`. Reviewed and approved after correcting the crop metadata in `items-atlas.json`. The generated PNG is unchanged. |
| Interface concept | `concept-candidate.png` | `concept-prompt.txt`. Design reference only; its illustrative text and grid are not runtime content. |
| Eight portrait candidates | Excluded | `portraits-prompt.txt`. Rejected for uniform faces, repeated lighting and generic fantasy detail. They are not application resources or release assets. |

Class emblems in `src/Akara.Desktop/Assets/ClassSigils.xaml` are original hand-authored WPF vector drawings. They replace the rejected portraits. Controls, text, item properties, state, and subtle surface texture are drawn at runtime.

The application icon `assets/akara-native.ico` is a multi-resolution format conversion of the approved mark. `scripts/Create-NativeIcon.ps1` records its crop and sizes. The README uses an identical copy of the approved PNG at `assets/akara-mark.png`.

`review.md` records explicit checks and defects, including the independent inspection of all 24 inventory objects and the icon at 32/64 px. The production audit gates at **approved**, with no open art findings. This approval applies to these assets, separately from native layout and behavior verification.

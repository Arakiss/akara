# Desktop languages

English is the first-run default, including on Spanish Windows. Spanish is an explicit choice in **Language → Español**. The language menu is always named in the current language; both choices retain their own names, **English** and **Español**.

The desktop stores `language: en|es` in `desktop-preferences.json` under its data directory. Missing, malformed or unsupported preferences fall back to English. Writes replace a temporary preference file; character saves and backups are separate. `--language en|es` overrides one launch without saving a preference. `--data-dir` isolates preferences along with the other application state.

Switching language rebuilds the native workbench around the same ViewModel. Its in-memory request is replayed to refresh validation, item properties, preset guides and differences. Unreviewed changes remain unreviewed; reviewed valid changes retain their reviewed state. No save is written. Invalid uncommitted input disables language switching so rebuilding the view cannot discard it.

## Text catalog

`src/Akara.Core/Localization/en.json` maps the existing Spanish message IDs to English presentation text. Spanish returns the original message. `EditorText.T` resolves messages and `EditorText.F` formats placeholders using the chosen culture. The catalog is an embedded resource and has no online translation dependency. XAML uses the native `Loc` markup extension; code uses the same catalog through `L`. Native parameter IDs, JSON property names, preset IDs, item bases, affix IDs, user-authored names and paths are never translated.

Native skill, class, quest and waypoint names use their existing English identifiers in English and the existing Spanish name mapping in Spanish. Stat labels use native stat names for lookup. Item names and artwork come from the installed tables and local game files. System file dialogs and standard message-box buttons follow Windows' language.

When adding a message, include a reviewed English catalog entry and preserve all format placeholders. Never pass arbitrary user content through translation. Static preset definitions and stat-label dictionaries keep their message IDs and resolve them when presented, rather than capturing whichever language initialized a static field first.

## Copy brief and review

The reader is a player editing a local character. Controls should state a short action; validation should state the problem and how to correct it. English uses familiar Diablo terms such as Faster Cast Rate, Faster Hit Recovery, weapon set, stash and runeword. Spanish remains direct and conversational. Jamella · Tributo, AKARA, character names and native item identities are protected names. Avoid literal phrases such as “prepare configuration” when the action stages a build in a draft.

Review checks: menus and property controls have natural action labels in both languages; native identifiers and user content remain unchanged; values and decimal separators follow the selected language; pending edits survive switching; first launch and invalid preferences use English. Automated native checks cover those flows using isolated preferences and private save copies. Desktop visual inspection complements those checks.

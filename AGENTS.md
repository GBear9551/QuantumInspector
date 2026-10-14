# QuantumInspector

## Project context

- This is an existing Unity editor-tool project. Read `ProjectSettings/ProjectVersion.txt` for the required Editor version and `Packages/manifest.json` for installed package versions; do not upgrade them as part of routine setup.
- Inspector and graph tooling lives in `Assets/FightSongStudio/QuantumInspector/Editor/`, with verification helpers in its `Verification/` directory. Demo content lives under `Assets/FightSongStudio/`.
- Inspect the working tree before editing and preserve unrelated work. Keep Unity assets paired with their `.meta` files and preserve existing GUIDs.
- Do not edit generated `Library/`, `Temp/`, `Logs/`, `obj/`, `.csproj`, or solution files.

## Agent instructions and skills

- Keep repository-wide guidance in this root `AGENTS.md`. Check for more specific `AGENTS.md` or `AGENTS.override.md` files when working in subdirectories.
- Reusable local skills belong in `.agents/skills/<skill-name>/SKILL.md`, with YAML frontmatter containing `name` and `description`. Read the relevant skill before following its workflow; do not load every skill for every task.
- Use `.agents/skills/unity-cli/SKILL.md` for Unity CLI and Editor operations. This project-local copy matches the installed CLI; prefer it over a duplicate bundled copy for command syntax.
- Use `.agents/skills/unity-pipeline/SKILL.md` for the installed Pipeline package's live Editor integration.
- Use the installed Unity plugin's relevant specialist skill for UI, rendering, physics, packages, and other Unity-specific work. For this editor UI, consult `unity:ui`, then the appropriate UI Toolkit or IMGUI skill for the code being changed.
- `.agents/skills/new-unity-project/SKILL.md` is for explicitly creating a new project; do not apply its bootstrap, monetization, or initial-commit workflow to maintenance of this project.
- Refresh the CLI-managed local skills after CLI or Pipeline updates with `unity skill install codex --local`. Keep generated skill content maintained by that installer rather than editing it by hand. `skills-lock.json` records the separately installed `new-unity-project` skill.

## Unity workflow

- Run commands from the project root. Check `unity status --format json` before Editor operations; target this project's absolute path with `--project-path` when multiple Editors may be open.
- Discover available Editor commands before invoking them. Include `--caller plugin --skill <skill-name>` on every `unity command` invocation, using the skill that guided the operation.
- When an Editor is reachable, modify scenes, prefabs, and serialized assets through live Editor commands rather than hand-editing their YAML. Diagnose Safe Mode or sandbox connection issues before treating an Editor as unavailable.
- Consult `unity:unity-package-management` before changing UPM dependencies. Keep Editor-only code in Editor folders or Editor-only assemblies.
- After C# changes, check compilation and the Editor console. Run the relevant existing verification helpers for the behavior changed, after inspecting their side effects. For UI changes, inspect the actual window as well as automated results.
- For agent-documentation-only changes, validate discovery paths, skill metadata, referenced files, and Git ignore behavior; no game build or Play mode run is needed.

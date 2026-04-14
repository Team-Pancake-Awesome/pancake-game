---
name: "Add Gameplay Feature"
description: "Use when adding a new Unity gameplay feature in the pancake game with Arduino-first input, rage-bait tuning, and clean production architecture."
argument-hint: "Describe the feature, affected scripts, player-facing behavior, and Arduino/debug input constraints."
agent: "Pancake Unity Gameplay Engineer"
---
Add or refactor a gameplay feature for this Unity pancake game.

Requirements:
- Preserve intended rage-bait friction and absurdist humor tone unless I explicitly ask to change it.
- Keep Arduino as the first-class input path.
- Keep keyboard/mouse support debug-only and runtime-toggleable.
- Use clean, production-level architecture with clear decoupling.
- Use readable names and small focused methods.
- Prefer patterns that support future expansion (interfaces, events, ScriptableObjects, strategy/state where appropriate).

Execution steps:
1. Restate the requested feature and classify behavior as player-facing, debug-only, or both.
2. Identify impacted scripts and propose the smallest architecture-safe change set.
3. Implement the changes directly in code.
4. Validate compile/regression signals available in the workspace.
5. Summarize changed files, rationale, risks, and optional next improvements.

Output format:
1. Feature summary
2. File-by-file change rationale
3. Validation notes
4. Follow-up options

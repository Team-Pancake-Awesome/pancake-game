---
description: "Use when writing or refactoring Unity gameplay C# scripts for this pancake game. Covers Arduino-first input architecture, decoupling, readability, data structures, and expansion-ready design."
name: "Unity Gameplay Architecture"
applyTo: "Assets/Scripts/**/*.cs"
---
# Unity Gameplay Architecture Guidelines

## Project Intent
- Preserve the game identity: rage-bait friction and absurdist humor are intentional design choices.
- Treat Arduino as the primary player input path.
- Keep keyboard/mouse input as debug-only fallback.

## Architecture Boundaries
- Keep systems separated: input, gameplay rules, UI, audio, VFX, persistence, and telemetry should not live in one class.
- Depend on contracts (interfaces/events) instead of concrete MonoBehaviour references where practical.
- Prefer composition over inheritance for gameplay feature assembly.
- Use ScriptableObjects for tunable data and balancing parameters rather than hardcoded constants.

## Input Rules
- Build input behind an abstraction layer so gameplay code is device-agnostic.
- Use runtime toggles for debug fallback input modes.
- Never let debug input paths silently override Arduino behavior in production flows.

## Code Quality Standards
- Use explicit method and variable names that communicate intent at a glance.
- Keep methods small and single-purpose; extract helpers when branching grows.
- Use guard clauses to reduce nesting and clarify control flow.
- Prefer data structures that make constraints obvious (for example, enum-driven state, dictionaries for lookup tables, queues for ordered events).

## Extensibility Standards
- Add extension points before special cases (strategy/state/event hooks).
- When introducing a feature, define where future modes/devices/rules can plug in.
- Avoid static cross-system coupling that blocks testing or future replacement.

## Validation Checklist
- Confirm no broken Arduino-first behavior.
- Confirm debug input remains opt-in and runtime-toggleable.
- Confirm no new hard coupling across input/gameplay/UI.
- Confirm names and method boundaries improved or stayed clear.

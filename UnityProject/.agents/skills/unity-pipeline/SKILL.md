---
name: unity-pipeline
description: Control a running Unity Editor or development Player through the Unity CLI and com.unity.pipeline. Use for live Unity scene, GameObject, component, asset, compilation, testing, capture, Play Mode, eval, or hot-reload operations. Requires the `unity` CLI on PATH and a target project with the Pipeline package running.
---

# Unity Pipeline

Use the `unity` CLI to control the target Unity Editor or development Player through
`com.unity.pipeline`. Do not edit Unity-serialized assets such as scenes, prefabs,
materials, animations, or ScriptableObjects as text when Pipeline operations are required.

## Before Unity Operations

1. Read the target project's `AGENTS.md` and applicable Unity rules.
2. Confirm the project path and discover reachable instances with `unity command` or
   `unity command --project-path <path>`.
3. Run `unity command --project-path <path>` without a command name and use the commands
   actually returned by that instance. Do not assume a command exists from prior sessions.
4. If multiple instances are reachable, identify the correct project and explicitly target
   it with `--project-path` or `--instance`. Stop when instances cannot be distinguished.
5. Query the Editor state, current scene, Play Mode, compilation/import activity, Prefab
   Stage, unsaved changes, and relevant Console baseline using available commands.
6. Do not save, discard, switch, or close user-owned unsaved content without authorization.

Run commands as:

```bash
unity command --project-path <path> <name> [args]
```

Editor servers normally use ports `7800-7849`. Runtime servers normally use
`7900-7949` and exist only in development Player builds. Use `--runtime` or
`--runtime-path` only when intentionally targeting a Player.

## Command Selection

Prefer operations in this order:

1. A dedicated command exposed by the current instance.
2. A generic Pipeline authoring command exposed by the current instance.
3. A minimal, task-scoped `eval` only when no suitable command exists.
4. Stop and explain the limitation when neither commands nor `eval` can perform the work
   reliably.

Before using a command, inspect its live help/schema when parameters are uncertain. For
scene or asset mutations, use Unity Undo where supported, set dirty state correctly, and
save only the scene or asset changed by the current task. Re-query the target after every
non-idempotent operation before retrying so duplicate objects or components are not created.

## Compilation And Tests

Keep the Editor ticking before unattended work when the current instance exposes
`set_autotick`:

```bash
unity command --project-path <path> set_autotick --enable true
```

After editing C# files, trigger compilation once and poll its status rather than waiting a
fixed duration:

```bash
unity command --project-path <path> recompile
unity command --project-path <path> recompile_status
```

Connection interruptions during domain reload can be expected. Reconnect to the same
project, continue polling, and stop on a failed terminal result. Inspect reported compiler
errors before running tests.

Use focused tests when possible:

```bash
unity command --project-path <path> list_tests --mode editor
unity command --project-path <path> run_tests --mode editor --filter MyFixture.MyTest
```

For asynchronous runs, poll the matching status command until a terminal state and use the
available cancel command only when cancellation is authorized.

## Scene And Asset Authoring

Use the current instance's dedicated scene, GameObject, component, prefab, material, asset,
and capture commands. If a dedicated operation is unavailable, a minimal Editor `eval` may
use UnityEditor APIs for the exact target only.

For mutations:

- Verify the current scene and edit/play state before changing objects.
- Preserve user-created objects and unrelated unsaved changes.
- Batch related primitive/object creation into one coherent operation when practical.
- Give created objects stable, descriptive names and an explicit parent hierarchy.
- Set transforms and serialized properties through Unity APIs, not YAML editing.
- Save only the task's intended scene or asset.
- Re-query hierarchy, components, transforms, references, and save state afterward.
- Capture a meaningful Scene or Game view when visual layout is part of the request.
- Compare new Console entries against the pre-operation baseline.

Do not use runtime object construction, `AddComponent`, `Transform.Find`, or hard-coded
resource paths to compensate for missing editor-authored content.

## Runtime Eval And Hot Reload

Quick runtime evaluation:

```bash
unity command --project-path <path> eval "return 2 + 2;"
unity command --project-path <path> eval_file Assets/Scratch.cs
```

Eval must be minimal and limited to the current task. Do not create persistent temporary
Editor scripts when a direct command or short eval is sufficient.

For in-place hot reload, the target game must be running in Editor Play Mode or a Mono
development Player. Methods must follow the package's `[HotReload]` requirements:

```bash
unity command --project-path <path> reload_file --filename Assets/Spinner.cs
unity command --project-path <path> hotreload_status
```

Use `reload_file_override` only for projects already following the package's override-file
pattern. Hot reload is not a substitute for persistent source or scene changes and is not
available for IL2CPP Players.

## Async Operations And Failures

Poll compilation, tests, imports, scene loads, Play Mode transitions, bakes, builds, and
detached jobs with their exposed status commands. Use reasonable intervals and a timeout;
never infer completion from elapsed time alone.

When a command fails:

1. Re-check the target instance and current Editor state.
2. Determine whether the prior request partially succeeded.
3. Re-query the exact object or asset before retrying non-idempotent work.
4. Report the command, target, relevant parameters, Editor state, completed portion, and
   remaining work.

Do not hide failures with broad exception handling, silent null checks, hard-coded paths, or
manual edits to Unity serialization.

## Finish

Before reporting completion:

- Confirm all Pipeline commands reached a known terminal state.
- Confirm the intended scene or asset was saved and unrelated content was not saved.
- Confirm the Editor is in the expected Edit/Play Mode state.
- Remove task-created temporary objects, files, and test state through Pipeline.
- Check new relevant Console errors, hierarchy/component state, serialized references, and
  visual output appropriate to the change.
- State the target instance, operations performed, saved assets, validation completed, and
  anything that could not be verified.

For package-specific parameters and command behavior, read the target project's
`Assets/com.unity.pipeline/Documentation~/` files that match the operation, then confirm the
command and schema exposed by the running instance.

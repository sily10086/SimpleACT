"""Read-only, streaming audit of explicitly selected Codex tasks and Unity CLI receipts.

Exports aggregate timings/command names only. Never exports conversation text, shell code,
arguments, tool output, environment, or credentials. Recorded item time is not provider CPU time.
"""
import argparse
import collections
import datetime as dt
import hashlib
import json
import math
import os
from pathlib import Path
import re
import time

VERSION = 1
TOOL_ITEMS = {"CommandExecution", "ImageView", "FileChange", "CollabAgentToolCall", "ContextCompaction"}
USAGE_KEYS = ("input_tokens", "cached_input_tokens", "cache_write_input_tokens", "output_tokens", "reasoning_output_tokens", "total_tokens")
EVAL_PATTERNS = {
    "editor_state_or_compilation": r"EditorApplication\.(?:isPlaying|isCompiling|isUpdating)|scriptCompilationFailed|AssetDatabase\.Refresh",
    "cinemachine_configuration_candidate": r"Cinemachine|gunCinematic|\.Lens\.|\.FramingSize|\.Follow\s*=|\.LookAt\s*=",
    "tmp_or_font_candidate": r"TMP_FontAsset|TryAddCharacters|\.font\s*=|\.fontSize\s*=|\.text\s*=|ForceUpdateCanvases",
    "shader_or_material_candidate": r"Shader\.(?:Get|Set)Global|\.SetFloat\(|\.SetColor\(|\.sharedMaterial|\.EnableKeyword\(",
    "input_or_playback_candidate": r"InputSystem|PlayerInput|\.Seek\(|PlayableDirector|director\.",
    "build_candidate": r"BuildPlayer|BuildPipeline|BuildReport",
}


class SourceReadError(ValueError):
    """A source cannot be attributed safely; the report may retain other valid sources."""


def number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def timestamp_ms(value):
    if not isinstance(value, str):
        return None
    try:
        return dt.datetime.fromisoformat(value.replace("Z", "+00:00")).timestamp() * 1000
    except ValueError:
        return None


def union(intervals):
    result = []
    for start, end in sorted(intervals):
        if end < start:
            continue
        if result and start <= result[-1][1]:
            result[-1][1] = max(end, result[-1][1])
        else:
            result.append([start, end])
    return result


def seconds(intervals):
    return sum(b - a for a, b in union(intervals)) / 1000


def intersect(left, right):
    result = []
    left, right = union(left), union(right)
    i = j = 0
    while i < len(left) and j < len(right):
        a, b = max(left[i][0], right[j][0]), min(left[i][1], right[j][1])
        if a < b:
            result.append([a, b])
        if left[i][1] <= right[j][1]:
            i += 1
        else:
            j += 1
    return result


def metadata_from_record(data, path):
    if not isinstance(data, dict) or data.get("type") != "session_meta":
        raise SourceReadError("invalid_session_metadata")
    payload = data.get("payload")
    if not isinstance(payload, dict) or not isinstance(payload.get("id"), str) or not payload["id"]:
        raise SourceReadError("invalid_session_identity")
    source = payload.get("source")
    subagent = source.get("subagent") if isinstance(source, dict) else None
    spawn = subagent.get("thread_spawn") if isinstance(subagent, dict) else None
    spawn = spawn if isinstance(spawn, dict) else {}
    parent, role = spawn.get("parent_thread_id"), spawn.get("agent_path")
    if parent is not None and not isinstance(parent, str):
        raise SourceReadError("invalid_parent_identity")
    if role is not None and not isinstance(role, str):
        raise SourceReadError("invalid_agent_identity")
    return {"thread_id": payload["id"], "parent_thread_id": parent, "agent_path": role, "file": path}


def read_metadata(path):
    try:
        with path.open("rb") as stream:
            data = json.loads(stream.readline())
        return metadata_from_record(data, path)
    except (OSError, ValueError, TypeError, AttributeError):
        return None


def discover_sessions(directory, root_thread):
    index = {}
    for path in directory.rglob("*.jsonl"):
        metadata = read_metadata(path)
        if metadata and metadata["thread_id"]:
            key = metadata["thread_id"]
            if key in index:
                raise ValueError("Multiple rollout files claim the same thread identity; supply explicit --rollout files after inspection")
            index[key] = metadata
    if root_thread not in index:
        raise ValueError("Requested root thread not found in supplied sessions directory")
    selected = {root_thread}
    while True:
        children = {key for key, value in index.items() if value["parent_thread_id"] in selected}
        expanded = selected | children
        if selected == expanded:
            break
        selected = expanded
    return [index[key] for key in sorted(selected)], len(index)


def opaque(value):
    return isinstance(value, str) and value.startswith("gAAAA")


def analyze_session(path, seen_usage, expected=None):
    digest = hashlib.sha256()
    events = collections.Counter()
    item_counts = collections.Counter()
    functions = collections.Counter()
    turns = {}
    intervals = collections.defaultdict(list)
    seen_items, open_calls = set(), {}
    usage = collections.Counter()
    usage_ids = set()
    invalid = foreign_events = opaque_calls = missing_item_times = missing_usage_identity = 0
    first_time = last_time = None
    with path.open("rb") as stream:
        source_stat = os.fstat(stream.fileno())
        size = source_stat.st_size
        header = stream.readline(size)
        try:
            metadata = metadata_from_record(json.loads(header), path)
        except (ValueError, UnicodeError) as error:
            raise SourceReadError("invalid_session_metadata") from error
        if expected and (metadata["thread_id"] != expected["thread_id"] or metadata["parent_thread_id"] != expected["parent_thread_id"]):
            raise SourceReadError("source_identity_changed_after_selection")
        digest.update(header)
        remaining = size - len(header)
        while remaining:
            raw = stream.readline(remaining)
            if not raw:
                break
            remaining -= len(raw)
            digest.update(raw)
            try:
                data = json.loads(raw)
            except (ValueError, UnicodeError):
                invalid += 1
                continue
            if not isinstance(data, dict) or not isinstance(data.get("type"), str):
                invalid += 1
                continue
            event_time = timestamp_ms(data.get("timestamp"))
            if event_time is not None:
                first_time = event_time if first_time is None else min(first_time, event_time)
                last_time = event_time if last_time is None else max(last_time, event_time)
            kind, payload = data.get("type"), data.get("payload", {})
            if not isinstance(payload, dict):
                invalid += 1
                continue
            if kind == "session_meta":
                repeated = metadata_from_record(data, path)
                if (repeated["thread_id"], repeated["parent_thread_id"]) != (metadata["thread_id"], metadata["parent_thread_id"]):
                    raise SourceReadError("conflicting_session_metadata_in_body")
            if kind in ("event_msg", "response_item") and not isinstance(payload.get("type"), str):
                invalid += 1
                continue
            if kind == "event_msg":
                if payload.get("thread_id") not in (None, metadata["thread_id"]):
                    foreign_events += 1
                    continue
                name = payload.get("type", "unknown")
                events[name] += 1
                turn_id = payload.get("turn_id")
                if turn_id is not None and not isinstance(turn_id, str):
                    invalid += 1
                    continue
                if name == "task_started" and turn_id:
                    start = payload.get("started_at")
                    start = start * 1000 if number(start) else event_time
                    turns.setdefault(turn_id, {"start": start, "end": None, "state": "in_flight"})
                if name in ("task_complete", "turn_aborted") and turn_id:
                    start, end = payload.get("started_at"), payload.get("completed_at")
                    record = turns.setdefault(turn_id, {"start": None, "end": None})
                    if number(start):
                        record["start"] = start * 1000
                    record["end"] = end * 1000 if number(end) else event_time
                    record["state"] = name
                if name == "item_completed":
                    item = payload.get("item", {})
                    if not isinstance(item, dict):
                        invalid += 1
                        continue
                    identity = item.get("id")
                    if identity is not None and not isinstance(identity, str):
                        invalid += 1
                        continue
                    if identity and identity in seen_items:
                        continue
                    if identity:
                        seen_items.add(identity)
                    item_type = item.get("type", "unknown")
                    if not isinstance(item_type, str):
                        invalid += 1
                        continue
                    item_counts[item_type] += 1
                    start, end = payload.get("started_at_ms"), payload.get("completed_at_ms")
                    if not (number(start) and number(end) and end >= start):
                        missing_item_times += 1
                        continue
                    category = "tool" if item_type in TOOL_ITEMS else "reasoning_item" if item_type == "Reasoning" else "assistant_message" if item_type == "AgentMessage" else "other_item"
                    if item_type != "UserMessage":
                        intervals[category].append([start, end])
            elif kind == "response_item":
                item_type = payload.get("type")
                call_id = payload.get("call_id")
                if call_id is not None and not isinstance(call_id, str):
                    invalid += 1
                    continue
                if item_type in ("function_call", "custom_tool_call") and call_id:
                    name = payload.get("name", "unknown")
                    if not isinstance(name, str):
                        invalid += 1
                        continue
                    functions[name] += 1
                    opaque_calls += int(opaque(payload.get("arguments")) or opaque(payload.get("input")))
                    if event_time is not None:
                        open_calls[call_id] = event_time
                elif item_type in ("function_call_output", "custom_tool_call_output") and call_id in open_calls:
                    start = open_calls.pop(call_id)
                    if event_time is not None and event_time >= start:
                        intervals["tool"].append([start, event_time])
            elif kind == "token_usage_record":
                if payload.get("thread_id") not in (None, metadata["thread_id"]):
                    continue
                response_id = payload.get("response_id")
                if response_id is not None and not isinstance(response_id, str):
                    invalid += 1
                    continue
                if payload.get("turn_id") is not None and not isinstance(payload["turn_id"], str):
                    invalid += 1
                    continue
                if not response_id:
                    missing_usage_identity += 1
                    continue
                identity = response_id
                if identity in seen_usage or identity in usage_ids:
                    continue
                source_usage = payload.get("usage")
                if not isinstance(source_usage, dict):
                    invalid += 1
                    continue
                for key in USAGE_KEYS:
                    value = source_usage.get(key)
                    if isinstance(value, int) and not isinstance(value, bool) and value >= 0:
                        usage[key] += value
                usage_ids.add(identity)
        end_stat = os.fstat(stream.fileno())
        if remaining or end_stat.st_size < size:
            raise SourceReadError("source_truncated_during_snapshot")
        if end_stat.st_size == size and end_stat.st_mtime_ns != source_stat.st_mtime_ns:
            raise SourceReadError("source_changed_without_append")
    active, turn_reports = [], []
    states = collections.Counter()
    ordered_turns = sorted(turns.items(), key=lambda pair: pair[1]["start"] or 0)
    for index, (turn_id, record) in enumerate(ordered_turns):
        states[record["state"]] += 1
        start = record["start"]
        end = record["end"] if record["end"] is not None else last_time
        boundary = "explicit_terminal" if record["end"] is not None else "snapshot_last_event"
        if record["end"] is None and index + 1 < len(ordered_turns):
            next_start = ordered_turns[index + 1][1]["start"]
            if number(next_start) and (end is None or next_start < end):
                end, boundary = next_start, "next_turn_started_without_observed_terminal"
        if number(start) and number(end) and end >= start:
            active.append([start, end])
            covered = union([pair for values in intervals.values() for pair in intersect(values, [[start, end]])])
            turn_reports.append({"turn_id": turn_id, "state": record["state"], "end_boundary": boundary,
                                 "started_at_ms": start, "ended_at_ms": end, "wall_seconds": (end - start) / 1000,
                                 "tool_union_seconds": seconds(intersect(intervals.get("tool", []), [[start, end]])),
                                 "unattributed_seconds": max(0, (end - start) / 1000 - seconds(covered))})
    active = union(active)
    timed = {key: intersect(value, active) for key, value in intervals.items()}
    covered = union([pair for value in timed.values() for pair in value])
    report = {
        "source_file": path.name, "thread_id": metadata["thread_id"],
        "parent_thread_id": metadata["parent_thread_id"], "agent_path": metadata["agent_path"],
        "snapshot_bytes": size, "bytes_read": size - remaining, "snapshot_sha256": digest.hexdigest(),
        "invalid_or_partial_records": invalid, "foreign_thread_events_skipped": foreign_events,
        "event_counts": dict(events), "item_counts": dict(item_counts), "function_call_counts": dict(functions),
        "opaque_call_inputs": opaque_calls, "unmatched_tool_calls": len(open_calls),
        "items_without_valid_timing": missing_item_times, "turn_states": dict(states),
        "turns": sorted(turn_reports, key=lambda value: value["wall_seconds"], reverse=True),
        "active_turn_wall_seconds": seconds(active),
        "recorded_item_seconds": {key: seconds(value) for key, value in timed.items()},
        "covered_item_union_seconds": seconds(covered),
        "unattributed_active_seconds": max(0, seconds(active) - seconds(covered)),
        "usage_per_unique_response": dict(usage),
        "usage_records_without_response_identity": missing_usage_identity,
    }
    seen_usage.update(usage_ids)
    return report, active, timed


def command_catalog(repo):
    commands = collections.defaultdict(list)
    directory = repo / "assets/com.unity.pipeline"
    if not directory.is_dir():
        directory = repo / "skill/assets/com.unity.pipeline"
    if not directory.is_dir():
        raise ValueError("Command catalog source must be an installed skill or repository containing com.unity.pipeline")
    for source in directory.rglob("*.cs"):
        text = source.read_text(encoding="utf-8-sig")
        for match in re.finditer(r'\[CliCommand\(\s*"([a-zA-Z0-9_]+)"', text):
            commands[match.group(1)].append(source.relative_to(directory).as_posix())
    return dict(commands)


def summarize(values):
    ordered = sorted(values)
    if not ordered:
        return {"count": 0}
    return {"count": len(ordered), "sum_seconds": sum(ordered), "p50_seconds": ordered[len(ordered) // 2], "p95_seconds": ordered[min(len(ordered) - 1, int(len(ordered) * .95))], "max_seconds": ordered[-1]}


def analyze_receipts(directory, catalog):
    if not directory.is_dir():
        raise ValueError("An explicitly supplied receipt directory does not exist")
    counts, statuses, unknown_names = collections.Counter(), collections.Counter(), 0
    failures = collections.Counter()
    eval_patterns = collections.Counter()
    durations = collections.defaultdict(list)
    files = invalid = invalid_duration = 0
    for path in sorted(directory.glob("*.json")):
        stamp, separator, name = path.stem.partition("-")
        if not separator or not stamp.isdigit() or len(stamp) < 16:
            continue
        files += 1
        label = name if name in catalog else "__unregistered_name__"
        counts[label] += 1
        unknown_names += int(label == "__unregistered_name__")
        elapsed = (path.stat().st_mtime_ns - int(stamp)) / 1e9
        if 0 <= elapsed < 24 * 60 * 60:
            durations[label].append(elapsed)
        else:
            invalid_duration += 1
        try:
            record = json.loads(path.read_text(encoding="utf-8-sig"))
            success = record.get("success")
            data = record.get("data")
            nested = data.get("result", {}) if isinstance(data, dict) else {}
            if name in ("eval", "eval_file") and isinstance(nested, dict) and nested.get("success") is False:
                success = False
            statuses["reported_success" if success is True else "reported_failure" if success is False else "unknown"] += 1
            if success is False:
                failures[label] += 1
            if name == "eval" and isinstance(data, dict):
                parameters = data.get("parameters")
                code = parameters.get("code") if isinstance(parameters, dict) else None
                if isinstance(code, str):
                    eval_patterns["receipts_with_readable_code"] += 1
                    for category, pattern in EVAL_PATTERNS.items():
                        if re.search(pattern, code):
                            eval_patterns[category] += 1
        except (OSError, ValueError, AttributeError):
            invalid += 1
            statuses["invalid_receipt"] += 1
    return {
        "source_directory_name": directory.name, "record_count": files,
        "commands": dict(counts.most_common()), "statuses": dict(statuses),
        "reported_failures_by_command": dict(failures.most_common()),
        "eval_candidate_patterns": dict(eval_patterns),
        "eval_pattern_boundary": "Overlapping lexical candidates, not proof a dedicated command can replace business code. Only aggregate category counts exported. eval_file external contents not opened.",
        "invalid_receipts": invalid, "unregistered_name_count": unknown_names,
        "invalid_proxy_durations": invalid_duration,
        "filename_to_file_mtime_proxy": {name: summarize(values) for name, values in sorted(durations.items())},
        "proxy_boundary": "Includes launch, transport, queue, Editor and filesystem effects. Not instrumented CPU time; not a directly removable cost. Not added to rollout tool wall time.",
    }


def build_report(rollouts, evidence, repo, discovered_count=0):
    started = time.monotonic()
    catalog = command_catalog(repo)
    sessions, all_active, tools, source_errors = [], [], [], []
    seen_usage = set()
    seen_threads = set()
    sources = {}
    for selection in rollouts:
        path = selection["file"] if isinstance(selection, dict) else selection
        path = path.resolve()
        expected = selection if isinstance(selection, dict) else None
        if path in sources and expected and sources[path] and sources[path]["thread_id"] != expected["thread_id"]:
            raise ValueError("Conflicting selected identity for the same source")
        sources[path] = expected or sources.get(path)
    for path, expected in sorted(sources.items()):
        try:
            session, active, timed = analyze_session(path, seen_usage, expected)
        except (SourceReadError, OSError) as error:
            source_errors.append({"source_file": path.name, "error_code": str(error) if isinstance(error, SourceReadError) else "source_io_error"})
            continue
        if session["thread_id"] in seen_threads:
            raise ValueError("Duplicate thread identity in explicitly supplied rollouts")
        seen_threads.add(session["thread_id"])
        sessions.append(session)
        all_active.extend(active)
        tools.extend(timed.get("tool", []))
    receipts = [analyze_receipts(path, catalog) for path in sorted({p.resolve() for p in evidence})]
    calls = collections.Counter()
    for receipt in receipts:
        calls.update(receipt["commands"])
    cm = {name for name, sources in catalog.items() if any("/Cinemachine" in source for source in sources)}
    cm_used = {name: count for name, count in calls.items() if name in cm}
    has_gaps = bool(source_errors) or any(s["invalid_or_partial_records"] for s in sessions) or any(r["invalid_receipts"] for r in receipts)
    return {
        "format_version": VERSION,
        "analyzer_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "generated_at_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "status": "partial" if has_gaps else "complete",
        "source_errors": source_errors,
        "scope": "Explicit root task and actual parent-linked descendants, plus explicitly supplied receipt directories. Unrelated mentions do not include a session.",
        "privacy": "Aggregate metadata only; no conversation, arguments, shell source, stdout, environment or credential values exported. Raw sources remain private.",
        "timing_boundary": "Recorded item durations are host observations, not provider inference/CPU time. Per-thread active sums overlap. Union avoids double-counting parent/child work. Unattributed gaps include scheduling and unavailable detail; they are not labeled pure AI thinking.",
        "usage_boundary": "Per-response usage fields are host records deduplicated by response ID; cached input is included in input tokens, reasoning output in output tokens. Not unique prompt size, billable cost or inference duration.",
        "metadata_files_considered": discovered_count,
        "sessions": sessions, "receipts": receipts,
        "totals": {
            "session_count": len(sessions), "snapshot_bytes": sum(s["snapshot_bytes"] for s in sessions),
            "active_turn_wall_union_seconds": seconds(all_active),
            "sum_thread_active_seconds": sum(s["active_turn_wall_seconds"] for s in sessions),
            "tool_wall_union_seconds": seconds(tools),
            "cli_receipt_count": sum(calls.values()), "cli_command_kinds_observed": len(calls),
            "cli_recorded_calls": dict(calls.most_common()),
        },
        "cinemachine": {"static_entry_names": len(cm), "recorded_kinds_used": len(cm_used), "recorded_calls": sum(cm_used.values()), "used": dict(sorted(cm_used.items())), "not_observed": sorted(cm - set(cm_used)), "boundary": "CM2/3 use the same entry names; static registration is not proof of installation or successful use. Receipt counts include attempts."},
        "catalog_boundary": "Command classification uses current source declarations. It does not reconstruct historical package versions or prove commands were registered in an old running Editor.",
        "scan_wall_seconds": time.monotonic() - started,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sessions-dir", type=Path)
    parser.add_argument("--root-thread")
    parser.add_argument("--rollout", type=Path, action="append", default=[])
    parser.add_argument("--evidence-dir", type=Path, action="append", default=[])
    location = parser.add_mutually_exclusive_group()
    location.add_argument("--repo", type=Path)
    location.add_argument("--skill-root", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Output already exists; choose a new report to preserve original observations")
    rollouts, considered = list(args.rollout), 0
    if bool(args.sessions_dir) != bool(args.root_thread):
        parser.error("--sessions-dir and --root-thread must be supplied together")
    if args.sessions_dir:
        found, considered = discover_sessions(args.sessions_dir, args.root_thread)
        rollouts.extend(found)
    if not rollouts and not args.evidence_dir:
        parser.error("Supply an explicit rollout/root task and/or receipt directory")
    source = args.repo or args.skill_root or Path(__file__).resolve().parents[1]
    report = build_report(rollouts, args.evidence_dir, source, considered)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    print(json.dumps({"output": str(args.output), "sessions": report["totals"]["session_count"], "bytes": report["totals"]["snapshot_bytes"], "cli_receipts": report["totals"]["cli_receipt_count"], "scan_wall_seconds": report["scan_wall_seconds"]}))
    return 2 if report["status"] == "partial" else 0


if __name__ == "__main__":
    raise SystemExit(main())

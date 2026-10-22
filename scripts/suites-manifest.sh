#!/usr/bin/env bash
# vibe-fs — targeted suite command manifest
#
# Usage:
#   ./scripts/suites-manifest.sh            # run every group
#   ./scripts/suites-manifest.sh delegation # run one group by name
#
# Environment:
#   REPO           clone/worktree to test. Defaults to the repository root that
#                  contains this script (git rev-parse --show-toplevel).
#   LOGDIR         where per-group logs and summary.txt are written.
#                  Defaults to "$REPO/.test-logs/suites".
#   SUITE_TIMEOUT  per-group timeout in seconds (default 1200).
#
# Contract:
#   * REPO must point at a clone/worktree whose `dist/` was produced by
#       node scripts/build.mjs
#     The staleness gate refuses to run against a stale build, so build FIRST.
#     `scripts/build.mjs` compiles the impact closure only and takes the
#     build lock itself (`.fable-build/build.lock`); there is no external
#     lock to wrap it in, and `--clean` is not a valid argument (a full
#     rebuild is forbidden — see scripts/checks/aggregate-retired.mjs).
#   * The build lock is NOT held here. Tests never run under the build lock.
#     Compilation is a global singleton and must happen inside ONE build
#     invocation, so any test that compiles internally is dropped, and the
#     integration/release tiers are explicitly disabled below.
#   * Every run is `timeout`-wrapped. No paid / real model calls: the unit
#     runner's offline phase only.
#   * `--skip-staleness-check` is deliberately NOT used: it is for build-tooling
#     work, not for evidence claims.
#
# Groups map to `requirements/<group>/tests/*.test.mjs`; the runner is
# `requirements/verification-system/tests/run.mjs`, which is scoped by the
# TESTS_MJS_FILES environment variable.
#
# Output: one log per group under $LOGDIR, plus a summary with per-suite
# pass/fail names.

set -u -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Default REPO to the repository root that owns this script.
if [ -z "${REPO:-}" ]; then
  REPO="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null || true)"
  if [ -z "$REPO" ]; then
    echo "FATAL: REPO is not set and '$SCRIPT_DIR' is not inside a git repository" >&2
    exit 2
  fi
fi

if [ -z "${LOGDIR:-}" ]; then
  # Runtime data belongs to the primary worktree's .Herdchestra (AGENTS.md:
  # 全部项目运行数据统一放在主工作树的 .Herdchestra/，linked worktree 共用
  # 这份本机状态). Resolve it through the existing scripts/lib/state-root.mjs
  # helper so a linked worktree writes to the SAME place as the main tree.
  # This script only queries Git topology; it writes no Git state.
  STATE_ROOT="$(node -e "
    import('$SCRIPT_DIR/lib/state-root.mjs').then(async m => {
      process.stdout.write(await m.stateRoot('$REPO'));
    }).catch(() => process.exit(1));
  ")" || {
    echo "FATAL: cannot resolve the application state root for $REPO" >&2
    echo "       Set LOGDIR explicitly to override." >&2
    exit 2
  }
  LOGDIR="$STATE_ROOT/takeover/suites"
fi
SUITE_TIMEOUT="${SUITE_TIMEOUT:-1200}"
RUNNER="requirements/verification-system/tests/run.mjs"

# Groups that map to the files the 8 branch commits touch.
# Format: key|directory holding the group's *.test.mjs files.
# The second field is a DIRECTORY (relative to REPO), not a display name.
SUITE_GROUPS=(
  "delegation|requirements/delegation/tests/"
  "effect-accounting|requirements/effect-accounting/tests/"
  "action-affordance|requirements/action-affordance/tests/"
  "repository-programming|requirements/repository-programming/tests/"
  "js-semantic-surface|requirements/js-semantic-surface/tests/"
  "session-ontology|requirements/session-ontology/tests/"
  "managed-session-lifecycle|requirements/managed-session-lifecycle/tests/"
  "managed-chat-execution|requirements/managed-chat-execution/tests/"
  "interaction-authority|requirements/interaction-authority/tests/"
  "host-boundary|requirements/host-boundary/tests/"
  "advisor|requirements/advisor/tests/"
  "knowledge-reuse|requirements/knowledge-reuse/tests/"
  "prefix-stability|requirements/prefix-stability/tests/"
  "participant-horizon|requirements/participant-horizon/tests/"
  "durable-convergence|requirements/durable-convergence/tests/"
  "provider-projection|requirements/provider-projection/tests/"
  "process-execution|requirements/process-execution/tests/"
  "execution-failure-policy|requirements/execution-failure-policy/tests/"
  "provider-attempt-recovery|requirements/provider-attempt-recovery/tests/"
  "execution-model-routing|requirements/execution-model-routing/tests/"
)

# Files that are known-flaky / pre-existing on the baseline. They are NOT
# excluded by default: a candidate failure in one of them is only meaningful
# if the BASELINE passed the same file. Once the baseline run is in hand and
# shows the identical outcome, set EXCLUDE_KNOWN_FLAKY=1 to drop them.
KNOWN_FLAKY_DEFAULT=""

EXCLUDE_KNOWN_FLAKY="${EXCLUDE_KNOWN_FLAKY:-0}"

usage() {
  echo "usage: REPO=<clone> [LOGDIR=<dir>] [SUITE_TIMEOUT=<sec>] $0 [group ...]" >&2
  echo "groups: $(printf '%s ' "${SUITE_GROUPS[@]%%|*}")" >&2
}

if [ "${1:-}" = "-h" ] || [ "${1:-}" = "--help" ]; then usage; exit 0; fi

if [ ! -d "$REPO" ]; then
  echo "FATAL: REPO=$REPO is not a directory" >&2
  exit 2
fi
if [ ! -f "$REPO/$RUNNER" ]; then
  echo "FATAL: $REPO/$RUNNER not found" >&2
  exit 2
fi
if [ ! -d "$REPO/dist" ]; then
  echo "FATAL: $REPO/dist missing - build first (node scripts/build.mjs)" >&2
  exit 2
fi

mkdir -p "$LOGDIR"
SUMMARY="$LOGDIR/summary.txt"
: > "$SUMMARY"

# Resolve the requested groups. With no arguments every group is selected;
# without this the run would silently select nothing.
REQUESTED=("$@")
if [ ${#REQUESTED[@]} -eq 0 ]; then
  REQUESTED=()
  for g in "${SUITE_GROUPS[@]}"; do REQUESTED+=("${g%%|*}"); done
fi

# Owner rule: every compilation is a global singleton and must run inside ONE
# build invocation. These test files compile internally (fable / dotnet), so
# they must NOT be run outside the lock.
# They are dropped here rather than executed against the prebuilt dist.
COMPILE_EXCLUDED=(
  "requirements/repository-programming/tests/050.test.mjs"
)

# effect-accounting/001 and host-boundary/026 also call compileOwnerProject,
# but ONLY inside an `integrationTest(...)` gate. With the integration tier
# disabled those cases are skipped, so no compile happens. The tier is
# explicitly disabled here so that an inherited WXS_TIER_INTEGRATION can never
# make a compile escape the slot.
unset WXS_TIER_INTEGRATION
unset WXS_TIER_RELEASE

cd "$REPO" || exit 2

for g in "${SUITE_GROUPS[@]}"; do
  key="${g%%|*}"
  files="${g#*|}"

  want=0
  for r in "${REQUESTED[@]}"; do
    if [ "$r" = "$key" ]; then want=1; break; fi
  done
  [ "$want" = 1 ] || continue

  # Resolve the group to a concrete, comma-separated file list.
  list="$(find "$REPO/$files" -maxdepth 1 -name '*.test.mjs' 2>/dev/null | sort | sed "s#^$REPO/##")"
  if [ -z "$list" ]; then
    echo "SKIP $key (no test files under $files)" | tee -a "$SUMMARY"
    continue
  fi

  # Drop tests that compile internally (owner rule: no compilation outside the lock).
  kept=""
  for f in $(printf '%s\n' "$list"); do
    skip=0
    for ex in "${COMPILE_EXCLUDED[@]}"; do
      if [ "$f" = "$ex" ]; then skip=1; break; fi
    done
    [ "$skip" = 1 ] && { echo "    (dropped, compiles internally: $f)"; continue; }
    kept="${kept}${kept:+
}$f"
  done
  list="$kept"
  if [ -z "$list" ]; then
    echo "SKIP $key (nothing left after dropping compiling tests)" | tee -a "$SUMMARY"
    continue
  fi

  csv="$(printf '%s\n' "$list" | paste -sd, -)"

  if [ "$EXCLUDE_KNOWN_FLAKY" = "1" ] && [ -n "$KNOWN_FLAKY_DEFAULT" ]; then
    filtered=""
    for f in $(printf '%s\n' "$list"); do
      case ",$KNOWN_FLAKY_DEFAULT," in
        *",$f,"*) ;;
        *) filtered="${filtered}${filtered:+,}$f" ;;
      esac
    done
    csv="$filtered"
  fi

  log="$LOGDIR/$key.log"
  echo "=== $key : $csv"
  echo "--- log: $log"

  start=$(date +%s)
  TESTS_MJS_FILES="$csv" timeout "$SUITE_TIMEOUT" node "$RUNNER" > "$log" 2>&1
  rc=$?
  end=$(date +%s)

  # Per-suite pass/fail names, as reported by the runner.
  names="$(grep -E '^(not ok|ok) [0-9]+ ' "$log" 2>/dev/null || true)"
  npass="$(printf '%s\n' "$names" | grep -c '^ok ' || true)"
  nfail="$(printf '%s\n' "$names" | grep -c '^not ok ' || true)"

  {
    echo "group=$key files=$csv"
    echo "exit=$rc duration_s=$((end-start)) pass=$npass fail=$nfail"
    if [ "$nfail" -gt 0 ]; then
      echo "FAILED:"
      printf '%s\n' "$names" | grep '^not ok ' || true
    fi
    echo
  } >> "$SUMMARY"

  echo "    exit=$rc pass=$npass fail=$nfail ($((end-start))s)"
done

echo
echo "=== summary -> $SUMMARY"
cat "$SUMMARY"

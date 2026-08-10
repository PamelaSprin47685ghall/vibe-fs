/**
 * manifest.mjs — the e2e suite is the case files, read from the directory.
 *
 * VERIFY-004 Release gate: scenario 清单必须是单一事实来源。用于日志或断言的数量常量必须从清单
 * 派生，不得独立维护 — and its 禁止退化清单 item 11, 「数量常量与清单各自维护」.
 *
 * What this replaces was a hand-written array of 16 paths in `tests/e2e/run.mjs` with
 * `const CANARY_COUNT = 17` above it. The integer had drifted TWICE, in opposite directions: the
 * migration ledger recorded the array at 19 against the same 17, then package K retired three
 * scenarios and left 16. Correcting 17 to 16 would have been a one-character fix to an instance of
 * a class — the next case added or retired reintroduces it, because the edit that moves the list
 * is never the edit that remembers the integer.
 *
 * Deriving from the directory removes both hand-maintained facts at once. There is no count to
 * drift, and there is no list to fall behind the files: a new `*.test.mjs` file registers itself
 * by existing, and a retired one deregisters by being deleted. Drift stops being something a
 * reviewer must catch and becomes something the tree cannot express.
 *
 * ── three decisions a reader should not have to reverse-engineer ────────────
 *
 * 1. ORDER IS ALPHABETICAL AND MEANS NOTHING. It is `walk`'s sort, kept only so that two runs on
 *    the same tree produce the same list. The order scenarios actually START in is decided by
 *    `tests/e2e/run.mjs`, which shuffles per iteration precisely because VERIFY-004
 *    requires 「每轮独立 shuffle 启动顺序」 to expose implicit order dependencies — a green run in
 *    one fixed order only proves that order works. So nothing here may be read as a designed
 *    sequence, and no scenario may be given a position on the assumption that another ran first.
 *
 * 2. AN EMPTY OR MISSING DIRECTORY IS AN ERROR, NOT AN EMPTY SUITE. Returning `[]` would let the
 *    release gate run three iterations over zero scenarios and report success — a harness that
 *    greens on a missing suite is more dangerous than no harness. The refusal names the absolute
 *    directory and the suffix it looked for, because the two ways this fails (the directory
 *    moved, the convention changed) need different fixes and the message is the only thing that
 *    tells them apart.
 *
 * 3. A FILE THAT CLAIMS TO BE A SCENARIO CASE MUST MATCH THE CONVENTION, and that is checked rather
 *    than assumed. Historical `*-canary.mjs` names are no longer accepted; cases are `*.test.mjs`
 *    under `tests/e2e/cases/`. `nonConformingCanaryNames` still flags stems that claim canary
 *    identity without the configured suffix, for harness regression trees.
 *
 *    The refusal for this one is a harness test (`tests/integration/harness/single-source-cases.mjs`),
 *    not a throw at import. A throw is right for an empty directory because continuing makes the
 *    gate vacuously green; a misnamed file makes the suite narrower, which a failing harness case
 *    reports without taking every scenario run down over a helper somebody named unluckily.
 *
 * The paths are ABSOLUTE. This module resolves its own root from `import.meta.url`, so it already
 * knows them; handing back repo-relative paths would add a dependency on the caller's cwd that the
 * derivation itself does not have.
 */

import { walk } from '../../../scripts/lib/walk.mjs';
import { availableParallelism } from 'node:os';
import { basename } from 'node:path';
import { fileURLToPath } from 'node:url';

/** The suffix that makes a file an e2e case. One spelling, one place. */
export const CANARY_SUFFIX = '.test.mjs';

/** Where e2e cases live, resolved from this module rather than from `cwd`. */
export const CANARY_DIR = fileURLToPath(new URL('../cases/', import.meta.url));

/**
 * Every case under `dir`, sorted; throws if there are none.
 *
 * Takes the directory as a parameter so the harness can point it at a tree that has none and see the
 * refusal. A rule only ever exercised against the tree where it happens to pass is the pseudo-gate
 * shape package W is removing.
 */
export function readCanaryTests(dir = CANARY_DIR) {
  const found = walk(dir, [CANARY_SUFFIX]);

  if (found.length === 0) {
    throw new Error(
      `scenario-manifest: no ${CANARY_SUFFIX} file under ${dir}. The e2e suite is derived from ` +
        'that directory, so an empty result is a moved directory or a changed naming convention — ' +
        'not a suite of zero, which would make the release gate pass without running anything.',
    );
  }

  return Object.freeze(found);
}

/**
 * Files under `dir` whose stem ends in `canary` but whose name is not the configured suffix form.
 *
 * The stem is the name up to its first dot, so `foo-canary.test.mjs` is read as claiming to be a
 * canary-shaped case and named wrongly, rather than as a file about something called `test`.
 */
export function nonConformingCanaryNames(dir = CANARY_DIR) {
  return walk(dir, undefined)
    .map((file) => basename(file))
    .filter((name) => /canary$/i.test(name.split('.')[0]) && !name.endsWith(CANARY_SUFFIX))
    .sort();
}

/** The suite. `CANARY_TESTS.length` is the only cardinality; there is no second one to disagree. */
export const CANARY_TESTS = readCanaryTests();

/**
 * How many scenarios may be in flight at once (ARCH-009 by analogy).
 *
 * ARCH-009 scopes to the business layer, so `Promise.all` over the whole suite in a harness script
 * is not a violation of it. But the clause's REASON is attributed to VERIFY-004, and excessive
 * OpenCode process fan-out manufactures precisely the resource contention VERIFY-004 forbids
 * masking with longer windows: failure then depends on machine load rather than on logic, and slow
 * becomes indistinguishable from dead.
 *
 * Each canary runs at least an OpenCode Host process plus its scenario/mock-provider process, so one
 * slot consumes two available processors. The default is `floor(os.availableParallelism() / 2)`,
 * respecting CPU affinity and cgroup limits, with a minimum of one and capped by the number of
 * scenarios. `MAX_PARALLEL_CANARIES` remains the explicit override for machine-specific calibration.
 *
 * It lives here and not in `time-budget.js` because it is not a duration.
 */
export const CANARY_MAX_PARALLEL = Math.min(
  Math.max(1, Math.floor(availableParallelism() / 2)),
  CANARY_TESTS.length,
);

/**
 * How many scenarios may be INSIDE STARTUP at once (the launch stagger's width).
 *
 * The stagger is causal: a launch waits for an earlier launch's readiness bark, never for a
 * timer. What this number decides is how far back that bark is — with 1, launches are strictly
 * serialized, so the suite cannot finish faster than the sum of every startup: measured 31 cases
 * × ~1.6s ≈ 50s of pure launch chain, against ~21s of actual work at 8 slots. The pool sat at
 * 3.4 of 8 for exactly that reason.
 *
 * Widening it does not reintroduce the thundering herd the stagger exists to prevent: three
 * concurrent `opencode serve` boots is still a bound, and the readiness ladder's per-stage budget
 * (`READINESS_STAGE_MS`) is the criterion that would fail if the machine could not carry them.
 * It stays well under `CANARY_MAX_PARALLEL` so steady-state parallelism, not startup, remains
 * what the pool bound governs.
 */
export const CANARY_STARTUP_WIDTH = Math.min(3, CANARY_MAX_PARALLEL);

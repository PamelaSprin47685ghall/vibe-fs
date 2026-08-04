/**
 * canary-manifest.js — the canary suite is the canary files, read from the directory.
 *
 * VERIFY-004 Release gate: canary 清单必须是单一事实来源。用于日志或断言的数量常量必须从清单
 * 派生，不得独立维护 — and its 禁止退化清单 item 11, 「数量常量与清单各自维护」.
 *
 * What this replaces was a hand-written array of 16 paths in `run-canary-staggered.mjs` with
 * `const CANARY_COUNT = 17` above it. The integer had drifted TWICE, in opposite directions: the
 * migration ledger recorded the array at 19 against the same 17, then package K retired three
 * canaries and left 16. Correcting 17 to 16 would have been a one-character fix to an instance of
 * a class — the next canary added or retired reintroduces it, because the edit that moves the list
 * is never the edit that remembers the integer.
 *
 * Deriving from the directory removes both hand-maintained facts at once. There is no count to
 * drift, and there is no list to fall behind the files: a new `*-canary.mjs` file registers itself
 * by existing, and a retired one deregisters by being deleted. Drift stops being something a
 * reviewer must catch and becomes something the tree cannot express.
 *
 * ── three decisions a reader should not have to reverse-engineer ────────────
 *
 * 1. ORDER IS ALPHABETICAL AND MEANS NOTHING. It is `walk`'s sort, kept only so that two runs on
 *    the same tree produce the same list. The order canaries actually START in is decided by
 *    `run-canary-staggered.mjs`, which shuffles per iteration precisely because VERIFY-004
 *    requires 「每轮独立 shuffle 启动顺序」 to expose implicit order dependencies — a green run in
 *    one fixed order only proves that order works. So nothing here may be read as a designed
 *    sequence, and no canary may be given a position on the assumption that another ran first.
 *
  * 2. AN EMPTY OR MISSING DIRECTORY IS AN ERROR, NOT AN EMPTY SUITE. Returning `[]` would let the
  *    release gate run three iterations over zero canaries and report success — a harness that
  *    greens on a missing suite is more dangerous than no harness. The refusal names the absolute
  *    directory and the suffix it looked for, because the two ways this fails (the directory
  *    moved, the convention changed) need different fixes and the message is the only thing that
  *    tells them apart.
 *
 * 3. A FILE THAT CLAIMS TO BE A CANARY MUST MATCH THE CONVENTION, and that is checked rather than
 *    assumed. `foo_canary.mjs` and `foo-canary.test.mjs` would both silently not register — the
 *    same silent omission as a stale array, one level up, and invisible in exactly the direction
 *    that matters: the suite would be smaller and still green. `nonConformingCanaryNames` reads
 *    the claim from the STEM: a stem ending in `canary` says "I am a canary" and must therefore be
 *    spelled `<subject>-canary.mjs`, while `canary-driver.mjs` or `canary-lib.mjs` names its
 *    subject and claims nothing.
 *
 *    The refusal for this one is a gate case (`tests/gate-single-source-cases.mjs`), not a throw
 *    at import. A throw is right for an empty directory because continuing makes the gate
 *    vacuously green; a misnamed file makes the suite narrower, which a failing gate case reports
 *    without taking every canary run down over a helper somebody named unluckily.
 *
 * The paths are ABSOLUTE. This module resolves its own root from `import.meta.url`, so it already
 * knows them; handing back repo-relative paths would add a dependency on the caller's cwd that the
 * derivation itself does not have.
 */

import { walk } from '../../scripts/lib/walk.mjs';
import { basename } from 'node:path';
import { fileURLToPath } from 'node:url';

/** The suffix that makes a file a canary. One spelling, one place. */
export const CANARY_SUFFIX = '-canary.mjs';

/** Where canaries live, resolved from this module rather than from `cwd`. */
export const CANARY_DIR = fileURLToPath(new URL('./tests/', import.meta.url));

/**
 * Every canary under `dir`, sorted; throws if there are none.
 *
 * Takes the directory as a parameter so the gate can point it at a tree that has none and see the
 * refusal. A rule only ever exercised against the tree where it happens to pass is the pseudo-gate
 * shape package W is removing.
 */
export function readCanaryTests(dir = CANARY_DIR) {
  const found = walk(dir, [CANARY_SUFFIX]);

  if (found.length === 0) {
    throw new Error(
      `canary-manifest: no ${CANARY_SUFFIX} file under ${dir}. The canary suite is derived from ` +
        'that directory, so an empty result is a moved directory or a changed naming convention — ' +
        'not a suite of zero, which would make the release gate pass without running anything.',
    );
  }

  return Object.freeze(found);
}

/**
 * Files under `dir` whose stem ends in `canary` but whose name is not `<subject>-canary.mjs`.
 *
 * The stem is the name up to its first dot, so `foo-canary.test.mjs` is read as claiming to be a
 * canary and named wrongly, rather than as a file about something called `test`.
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
 * How many canaries may be in flight at once (ARCH-009 by analogy).
 *
 * ARCH-009 scopes to the business layer, so `Promise.all` over the whole suite in a harness script
 * is not a violation of it — recorded as such in the shock-anneal archive. But the clause's REASON is
 * attributed to VERIFY-004, and excessive OpenCode process fan-out manufactures precisely the
 * resource contention VERIFY-004 forbids masking with longer windows: failure then depends on
 * machine load rather than on logic, and slow becomes indistinguishable from dead.
 *
 * Eight is the declared pressure. A canary that cannot report causal progress inside the fixed
 * silence window is missing an event; reducing concurrency would only hide that missing edge.
 * `MAX_PARALLEL_CANARIES` still overrides for a machine that can take more.
 *
 * It lives here and not in `time-budget.js` because it is not a duration. W5 put it there and the
 * budget gate's own pin refused it: a table contracted as 「全部 wall-clock 兜底的单一来源」 cannot
 * hold a concurrency count without that contract decaying into 「W5 需要的常量」.
 */
export const CANARY_MAX_PARALLEL = 8;

/**
 * gate-degradation-cases.mjs — W7: every forbidden degradation has a case, and that is checked.
 *
 * VERIFY-004 lists thirteen degradations under `### 禁止退化清单` (docs/proof/verify.md). W7's charge is a
 * failing test per item, and a completeness gate over the set — because a registered case file that
 * contributes nothing looks, in the gate's output, exactly like one whose cases all pass. The
 * placeholder this package started from (`gate-readiness-cases.mjs` was an empty array) is the
 * concrete shape: 「零用例」 and 「全部通过」 are byte-identical in the report.
 *
 * ── the registry is the gate, and it is checked in both directions ──────────
 *
 * `DEGRADATION_COVERAGE` binds each degradation id to the names of the cases that cover it. The
 * completeness case then proves three things, each of which a lazier design would skip:
 *
 *   1. every id in `DEGRADATIONS` has at least one covering case   (no degradation uncovered)
 *   2. the registry has no id the clause stopped forbidding        (no orphaned citation)
 *   3. every cited case NAME exists in the collected suite          (the citation is real)
 *
 * (3) is the load-bearing one and the reason this file imports every case array. A name is a string;
 * nothing about it proves the case exists. An empty case file, or a case renamed, leaves the registry
 * pointing at a name that resolves to nothing — and (3) is what turns that into a failure instead of a
 * silent green. This is the same discipline as `degradation-list.mjs` checking its ids against the
 * clause both ways: a binding only proves anything if both ends are held.
 *
 * The covering cases are mostly elsewhere — the unit runner, the verdict feed, the readiness ladder,
 * the budget relations — because that is where each degradation's mechanism lives. Pool / launcher
 * degradations that belonged to the retired multi-canary runner are covered here as source-level
 * One World topology checks (sole entry, no shuffle-repeat pool).
 */

import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { assertEq, assertTrue } from './lib.mjs';
import { DEGRADATIONS } from '../../e2e/support/degradation-list.mjs';
import { cases } from './cases.mjs';
import { budgetCases } from './budget-cases.mjs';
import { readinessCases } from './readiness-cases.mjs';
import { unitRunnerCases } from './unit-runner-cases.mjs';
import { singleSourceCases } from './single-source-cases.mjs';
import { pathCriterionCases } from './path-criterion-cases.mjs';

const REPO_ROOT = fileURLToPath(new URL('../../../', import.meta.url));
const SOLE_ENTRY = 'tests/e2e/entry.test.mjs';
const VERDICT_FEED_TEST = 'tests/unit/verdict-feed.test.mjs';

const readSource = (relative) => readFileSync(`${REPO_ROOT}${relative}`, 'utf8');

/**
 * The verdict feed's own cases live under `node:test`, not in a gate case array, so their names are
 * read from source rather than imported. D3's covering case is there: a hang that keeps printing is
 * the exact shape that would turn a verdict feed back into a wall-clock timer, and that case proves
 * `test:stdout` is recorded as background, never as progress.
 */
const verdictFeedCaseNames = () =>
  [...readSource(VERDICT_FEED_TEST).matchAll(/test\('([^']+)'/g)].map((match) => match[1]);

/** Every case name the gate suite can run, so a citation can be resolved against reality. */
const collectedCaseNames = () =>
  new Set(
    [
      ...cases,
      ...budgetCases,
      ...readinessCases,
      ...unitRunnerCases,
      ...singleSourceCases,
      ...pathCriterionCases,
      ...degradationCases,
    ]
      .map((testCase) => testCase.name)
      .concat(verdictFeedCaseNames()),
  );

/**
 * Degradation id → the names of the cases that cover it.
 *
 * Keyed by id, not by ordinal, for the reason `degradation-list.mjs` names its ids: insert an item at
 * position 3 in the SSOT and every ordinal above it shifts, silently re-pointing each citation at its
 * neighbour. A named id cannot drift without the parser throwing.
 */
const DEGRADATION_COVERAGE = new Map([
  [
    'VERIFY_004_D_WALL_CLOCK_AS_ONLY_HANG_CRITERION',
    [
      'VERIFY-004 a hung test that keeps printing is ended by the verdict-silence window',
      'VERIFY-004 no budget is 兜底-only for a criterion that has a causal signal',
    ],
  ],
  [
    'VERIFY_004_D_RAW_TRAFFIC_RENEWS_WATCHDOG',
    [
      'VERIFY-004 a hung test that keeps printing is ended by the verdict-silence window',
      'VERIFY_004_bytes_moving_is_recorded_and_does_not_renew',
    ],
  ],
  ['VERIFY_004_D_BACKGROUND_LANE_RENEWS_WATCHDOG', ['VERIFY_004_bytes_moving_is_recorded_and_does_not_renew']],
  [
    'VERIFY_004_D_WATCHDOG_DUMP_REDUCED_TO_EXIT_CODE',
    ['VERIFY-004 a hung test that keeps printing is ended by the verdict-silence window'],
  ],
  ['VERIFY_004_D_WATCHDOG_TIMER_HOLDS_EVENT_LOOP', ['VERIFY-004 a clean run is not held to the end of the silence window']],
  [
    'VERIFY_004_D_WINDOW_GUARDED_ONLY_BY_TOTAL_TIMEOUT',
    ['VERIFY-004 the stage budget is tighter than the total startup ceiling'],
  ],
  [
    'VERIFY_004_D_DECLARED_HEARTBEAT_NOT_WIRED',
    ['VERIFY-004 verdicts actually renew the window, so legitimate slow work is not killed'],
  ],
  [
    'VERIFY_004_D_FIXED_SLEEP_REPLACES_CAUSAL_BARK',
    ['VERIFY-004 One World sole entry has no multi-canary shuffle-repeat pool'],
  ],
  [
    'VERIFY_004_D_READY_TIMEOUT_OR_EARLY_EXIT_PASSES',
    ['VERIFY-004 One World sole entry has no multi-canary shuffle-repeat pool'],
  ],
  [
    'VERIFY_004_D_RELEASE_GATE_BECOMES_AT_MOST_N_ROUNDS',
    ['VERIFY-004 One World sole entry has no multi-canary shuffle-repeat pool'],
  ],
  [
    'VERIFY_004_D_COUNT_CONSTANT_MAINTAINED_APART_FROM_LIST',
    ['VERIFY-004 no cardinality is maintained beside the collection it counts'],
  ],
  ['VERIFY_004_D_STATIC_GATE_PATH_DOES_NOT_EXIST', ['VERIFY-004 every path criterion in the harness resolves on disk']],
  ['VERIFY_004_D_WINDOW_WIDENED_TO_HIDE_A_RACE', ['VERIFY-004 no budget is 兜底-only for a criterion that has a causal signal']],
]);

export const degradationCases = [
  {
    name: 'VERIFY-004 One World sole entry has no multi-canary shuffle-repeat pool',
    fn: () => {
      // Covers the pool/launcher degradations that belonged to the retired multi-canary runner
      // (fixed-sleep bark stagger, ready-timeout-as-pass, release-gate --repeat 1..3). One World
      // replaces that topology with a sole entry: there is no stagger to sleep-replace, no pool
      // pass condition to omit ready terms from, and no --repeat release gate to raise into
      // until-pass. Detection at the source — an absence has no input that exhibits it.
      assertTrue(
        existsSync(`${REPO_ROOT}${SOLE_ENTRY}`),
        `${SOLE_ENTRY} must exist as the sole top-level E2E entry`,
      );
      assertTrue(
        !existsSync(`${REPO_ROOT}tests/e2e/run.mjs`),
        'tests/e2e/run.mjs (multi-canary launcher) must be gone',
      );
      assertTrue(
        !existsSync(`${REPO_ROOT}tests/e2e/support/manifest.mjs`),
        'tests/e2e/support/manifest.mjs (canary suite list) must be gone',
      );

      const pkg = JSON.parse(readSource('package.json'));
      assertTrue(
        typeof pkg.scripts?.['test:e2e'] === 'string' && pkg.scripts['test:e2e'].includes('entry.test.mjs'),
        'package.json test:e2e must point at entry.test.mjs',
      );
      assertTrue(
        typeof pkg.scripts?.['check:release'] === 'string' && !pkg.scripts['check:release'].includes('--repeat'),
        'check:release must not reintroduce a --repeat release-gate pool',
      );

      const entry = readSource(SOLE_ENTRY);
      assertTrue(!/\bshuffle\b/.test(entry), 'the sole entry must not shuffle a canary pool');
      assertTrue(!entry.includes('--repeat'), 'the sole entry must not implement a repeat release gate');
      assertTrue(!entry.includes('MAX_PARALLEL'), 'the sole entry must not enforce a canary concurrency pool');
      assertTrue(!entry.includes('STARTUP_WIDTH'), 'the sole entry must not stagger launches by startup width');
    },
  },

  {
    name: 'VERIFY-004 internal expectations are background progress',
    fn: () => {
      const source = readSource('tests/e2e/support/strict-mock-provider.js');

      assertTrue(
        source.includes('blocking: entry.internal !== true'),
        'Blogger and other internal lanes must never renew the blocking watchdog',
      );
      assertTrue(
        !source.includes('blocking: true'),
        'an unconditional blocking classification lets background loops mask a dead path',
      );
    },
  },

  {
    name: 'VERIFY-004 flow waits do not start a competing total timeout',
    fn: () => {
      const source = readSource('tests/e2e/support/scenario-driver.mjs');

      assertTrue(
        !source.includes('waitForExpectation(step.wait, step.timeoutMs || WATCHDOG_TIMEOUT_MS)'),
        'the fixed watchdog owns silence; a per-wait total window races healthy causal progress',
      );
      assertTrue(
        !source.includes('awaitSessionsByAgent(scenario, agent, step.timeoutMs || WATCHDOG_TIMEOUT_MS)'),
        'child discovery must use the same watchdog rather than a second total deadline',
      );
      assertTrue(
        !source.includes('timeoutMs: step.timeoutMs || WATCHDOG_TIMEOUT_MS'),
        'turn terminals must renew the same watchdog at each causal checkpoint',
      );
      assertTrue(
        !source.includes('}, step.timeoutMs || WATCHDOG_TIMEOUT_MS)'),
        'event waits must not race the fixed watchdog with a total deadline',
      );

      const turnSource = readSource('tests/e2e/support/scenario-turn.js');
      assertTrue(
        !turnSource.includes('timeoutMs: opts.timeoutMs || WATCHDOG_TIMEOUT_MS'),
        'Turn must leave its local timeout absent unless the scenario explicitly declares one',
      );

      const providerSource = readSource('tests/e2e/support/strict-mock-provider.js');
      assertTrue(
        !providerSource.includes('timeoutMs = WATCHDOG_TIMEOUT_MS'),
        'provider wait helpers must not default every flow wait to the silence window as a total deadline',
      );
    },
  },

  {
    name: 'VERIFY-004 every forbidden degradation has a covering case, and every citation resolves',
    fn: () => {
      // The completeness gate. Three checks, each closing a different hole:
      //
      //   clause id with no case   → a degradation nobody tests
      //   registry id not in clause → a citation of something the SSOT no longer forbids
      //   cited name with no case   → an empty or renamed case file, still claiming coverage
      //
      // The third is why this file imports every case array. Without it the registry is prose.
      const clauseIds = DEGRADATIONS.map((degradation) => degradation.id);
      const registryIds = [...DEGRADATION_COVERAGE.keys()];

      assertEq(
        registryIds.length,
        clauseIds.length,
        `the registry covers ${registryIds.length} degradations but the clause lists ${clauseIds.length}`,
      );

      for (const id of clauseIds) {
        const covering = DEGRADATION_COVERAGE.get(id);
        assertTrue(
          covering && covering.length > 0,
          `${id} has no covering case — the degradation is untested`,
        );
      }

      for (const id of registryIds) {
        assertTrue(
          clauseIds.includes(id),
          `${id} is cited by the registry but the clause no longer forbids it`,
        );
      }

      const names = collectedCaseNames();
      for (const [id, covering] of DEGRADATION_COVERAGE) {
        for (const caseName of covering) {
          assertTrue(
            names.has(caseName),
            `${id} cites a case that does not exist in the suite: '${caseName}' — an empty or ` +
              'renamed case file would otherwise keep claiming coverage',
          );
        }
      }
    },
  },
];

/**
 * gate-degradation-cases.mjs — W7: every forbidden degradation has a case, and that is checked.
 *
 * VERIFY-004 lists thirteen degradations under `### 禁止退化清单` (spec/10.md). W7's charge is a
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
 * the budget relations — because that is where each degradation's mechanism lives. Three have no home
 * yet (fixed-sleep launch stagger, ready-timeout-as-pass, release-gate-rounds) and are written here
 * as source-level cases, the established shape for an absence: an absence has no input that exhibits
 * it, so it is asserted at the source. They are detection, not prevention — the comment on each says
 * so rather than overclaiming.
 */

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { assertEq, assertTrue } from './gate-lib.mjs';
import { DEGRADATIONS } from './degradation-list.mjs';
import { cases } from './gate-cases.mjs';
import { budgetCases } from './gate-budget-cases.mjs';
import { readinessCases } from './gate-readiness-cases.mjs';
import { unitRunnerCases } from './gate-unit-runner-cases.mjs';
import { singleSourceCases } from './gate-single-source-cases.mjs';
import { pathCriterionCases } from './gate-path-criterion-cases.mjs';

const REPO_ROOT = fileURLToPath(new URL('../../../', import.meta.url));
const LAUNCHER = 'scripts/run-canary-staggered.mjs';
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
    [
      'VERIFY-004 the launcher re-arms per stage and keeps the total ceiling separate',
      'VERIFY-004 the stage budget is tighter than the total startup ceiling',
    ],
  ],
  [
    'VERIFY_004_D_DECLARED_HEARTBEAT_NOT_WIRED',
    ['VERIFY-004 verdicts actually renew the window, so legitimate slow work is not killed'],
  ],
  ['VERIFY_004_D_FIXED_SLEEP_REPLACES_CAUSAL_BARK', ['VERIFY-004 launch stagger is causal bark, not a fixed sleep']],
  [
    'VERIFY_004_D_READY_TIMEOUT_OR_EARLY_EXIT_PASSES',
    ['VERIFY-004 a canary that never reaches ready, or exits before it, is failed not passed'],
  ],
  ['VERIFY_004_D_RELEASE_GATE_BECOMES_AT_MOST_N_ROUNDS', ['VERIFY-004 the release gate is exactly three rounds, never until-pass']],
  [
    'VERIFY_004_D_COUNT_CONSTANT_MAINTAINED_APART_FROM_LIST',
    [
      'VERIFY-004 no cardinality is maintained beside the collection it counts',
      'VERIFY-004 the canary suite is exactly the -canary.mjs files on disk',
    ],
  ],
  ['VERIFY_004_D_STATIC_GATE_PATH_DOES_NOT_EXIST', ['VERIFY-004 every path criterion in the harness resolves on disk']],
  ['VERIFY_004_D_WINDOW_WIDENED_TO_HIDE_A_RACE', ['VERIFY-004 no budget is 兜底-only for a criterion that has a causal signal']],
]);

export const degradationCases = [
  {
    name: 'VERIFY-004 launch stagger is causal bark, not a fixed sleep',
    fn: () => {
      // Covers VERIFY_004_D_FIXED_SLEEP_REPLACES_CAUSAL_BARK. DETECTION, not prevention: nothing
      // stops an edit adding a sleep; what this arranges is that the edit cannot land without a
      // red line naming the degradation it reintroduces.
      //
      // The positive half is the mechanism: canary N launches only once canary N-1 has barked, so the
      // stagger is an event, not a duration. The negative half is the degradation: a fixed per-launch
      // sleep is exactly what this replaces, and `setTimeout` is the only primitive a launch stagger
      // would use — the timers that remain in this file are the watchdog's, all fed by the ladder.
      const source = readSource(LAUNCHER);

      assertTrue(
        source.includes('await currentPrevBark'),
        'launch N must wait on the previous canary\'s bark, or the stagger is not event-driven',
      );
      assertTrue(
        source.includes('const onBark = () => triggerBark()'),
        'the bark that releases the next launch must be the readiness signal, not a timer firing',
      );
      assertTrue(
        !/setTimeout\([^)]*\)\s*;\s*\n\s*console\.log\("\[Launch\]/.test(source),
        'a fixed sleep before [Launch] is the degradation this case forbids',
      );
      assertTrue(
        !/await new Promise\(\(?resolve\)? => setTimeout\(resolve/.test(source),
        'a sleep promise used as a launch gate is a fixed sleep by another name',
      );
    },
  },

  {
    name: 'VERIFY-004 canary launch enforces its declared concurrency bound',
    fn: () => {
      const source = readSource(LAUNCHER);

      assertTrue(
        source.includes('await Promise.all(Array.from({ length: MAX_PARALLEL }, runWorker))'),
        'the launcher must create exactly MAX_PARALLEL workers',
      );
      assertTrue(
        source.includes('results[index] = await runCanary(file, onBark)'),
        'each worker must finish its current canary before taking another slot',
      );
      assertTrue(
        !source.includes('canaryPromises.push'),
        'collecting one live promise per canary bypasses MAX_PARALLEL',
      );
    },
  },

  {
    name: 'VERIFY-004 internal expectations are background progress',
    fn: () => {
      const source = readSource('tests/e2e/strict-mock-provider.js');

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
      const source = readSource('tests/e2e/canary-driver.mjs');

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

      const turnSource = readSource('tests/e2e/scenario-turn.js');
      assertTrue(
        !turnSource.includes('timeoutMs: opts.timeoutMs || WATCHDOG_TIMEOUT_MS'),
        'Turn must leave its local timeout absent unless the scenario explicitly declares one',
      );
    },
  },

  {
    name: 'VERIFY-004 a canary that never reaches ready, or exits before it, is failed not passed',
    fn: () => {
      // Covers VERIFY_004_D_READY_TIMEOUT_OR_EARLY_EXIT_PASSES. The pass line is a conjunction, and
      // every failure shape must be one of its negated terms: a canary that times out reaching ready,
      // or exits before barking, has to land in the else branch and take the suite to exit 1. The
      // degradation is a pass condition that omits one of these terms — 「就绪超时或就绪前退出被当作
      // 通过」 — which reads as a green suite over a canary that never started.
      const source = readSource(LAUNCHER);

      assertTrue(
        source.includes('r.code === 0 && r.barked && !r.barkTimeout && !r.processTimeout && !r.exitedBeforeBark && !readyGateFailures.has(r.file)'),
        'the pass condition must reject every not-ready shape: barkTimeout, processTimeout, exitedBeforeBark, and the ready-gate failure set',
      );
      assertTrue(
        /else \{\s*\n\s*failed = true;/.test(source),
        'anything short of the full conjunction must set failed, not fall through to a pass',
      );
      assertTrue(
        /if \(failed\) \{[\s\S]*?process\.exit\(1\);/.test(source),
        'a failed iteration must exit 1; a suite that reports failure and exits 0 is the degradation',
      );
      assertTrue(
        source.includes('exited before [setupScenario] ready'),
        'an early exit must be named as a failure reason, not absorbed into a generic code',
      );
    },
  },

  {
    name: 'VERIFY-004 the release gate is exactly three rounds, never until-pass',
    fn: () => {
      // Covers VERIFY_004_D_RELEASE_GATE_BECOMES_AT_MOST_N_ROUNDS. Two halves, matching the clause's
      // two shapes. 「最多 N 轮」: the round count is bounded at 3 and a value above it is refused, so
      // it cannot be raised to 「run until it passes」 by configuration. 「重跑直到通过」: the loop runs
      // a FIXED number of iterations and exits 1 on the first failed one — there is no while-not-green
      // anywhere, because a gate that retries a failure into a pass is not a gate.
      const source = readSource(LAUNCHER);

      assertTrue(
        source.includes('repeats < 1 || repeats > 3'),
        'CANARY_REPEAT must be bounded at 3, or the release gate becomes 「at most N rounds」 for arbitrary N',
      );
      assertTrue(
        source.includes('for (let rep = 1; rep <= repeats; rep++)'),
        'the round loop must be a fixed-count for, bounded by the validated repeat count',
      );
      assertTrue(
        !/while\s*\(\s*(!?failed|true|.*pass)/.test(source),
        'a while loop keyed on failure or success is 「重跑直到通过」 — the release gate must not retry',
      );
      assertTrue(
        /if \(failed\) \{[\s\S]*?process\.exit\(1\);/.test(source),
        'the first failed round must stop the gate; continuing past a failure is the until-pass shape',
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

/**
 * gate-readiness-cases.mjs — the startup ladder, under test (VERIFY-004, W5).
 *
 * The clause requires the startup window to have a causal criterion of its own:
 *
 *   启动阶段（进程拉起到就绪）必须有独立的就绪判据，不得只靠兜底 wall-clock 覆盖。
 *   禁止：存在一段「只有总超时保护的时间窗」
 *
 * A ladder over log lines has one failure mode that matters more than the rest, and it is the
 * reason this file leads with source-derived evidence rather than hand-written expectations: the
 * ladder advances only on the NEXT expected marker, so a list whose ORDER disagrees with the order
 * production prints stalls at the first disagreement and fails every canary at the stage budget.
 * That failure is indistinguishable, from the launcher's diagnostic, from a genuinely wedged host.
 * It was measured here — the first draft listed `workspace` before `provider` because that is the
 * order the two read naturally, while `scenario-parallel.js` prints `provider.start took` at :88 and
 * `prepareWorkspace took` at :94. A readiness gate that reports 「stuck at 1/9」 for a healthy
 * startup is worse than no readiness gate, because the reader stops looking at the host.
 *
 * So the order is not asserted against a copy of itself. It is derived from where the harness
 * actually prints each marker and compared, which makes the next reordering of production prints a
 * failure here instead of a suite-wide hang whose cause is nowhere near its symptom.
 */

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { assertEq, assertTrue } from './lib.mjs';
import { ReadinessLadder, READINESS_STAGES } from '../../e2e/support/readiness.js';
import { CANARY_READY_MS, READINESS_STAGE_MS } from '../../e2e/support/time-budget.js';

const REPO_ROOT = fileURLToPath(new URL('../../../', import.meta.url));

/** The two files that print the stage lines. Nothing else may own a stage marker. */
const STAGE_SOURCES = ['tests/e2e/support/scenario-parallel.js', 'tests/e2e/support/process-host.js'];

const readSource = (relative) => readFileSync(`${REPO_ROOT}${relative}`, 'utf8');

/**
 * Where each stage marker is printed, as `{ file, line }` per stage name.
 *
 * Collected across both sources so a marker that has moved between them, or been printed twice, is
 * visible as data rather than as a stalled climb at runtime.
 */
const printSites = () => {
  const sites = new Map();

  for (const relative of STAGE_SOURCES) {
    readSource(relative)
      .split('\n')
      .forEach((text, index) => {
        for (const stage of READINESS_STAGES) {
          if (!text.includes(stage.marker)) continue;
          const previous = sites.get(stage.name) ?? [];
          sites.set(stage.name, [...previous, { file: relative, line: index + 1 }]);
        }
      });
  }

  return sites;
};

/** The line in `scenario-parallel.js` that reports the nested `host.start`, as a position proxy. */
const hostStartLine = () =>
  readSource(STAGE_SOURCES[0])
    .split('\n')
    .findIndex((text) => text.includes('[setupScenario] host.start took')) + 1;

/** Feed a ladder a whole sequence of markers, one accumulated buffer at a time. */
const climb = (stageNames) => {
  const ladder = new ReadinessLadder();
  let buffer = '';

  for (const name of stageNames) {
    const stage = READINESS_STAGES.find((candidate) => candidate.name === name);
    buffer += `${stage.marker} 12ms\n`;
    ladder.observe(buffer);
  }

  return ladder;
};

const allStageNames = READINESS_STAGES.map((stage) => stage.name);

export const readinessCases = [
  {
    name: 'VERIFY-004 the ladder stages are ordered the way production prints them',
    fn: () => {
      // The load-bearing case, and the one the measured defect would have failed. Within one file
      // the print order IS the completion order, so the ladder's relative order must match it.
      const sites = printSites();

      for (const relative of STAGE_SOURCES) {
        const owned = READINESS_STAGES.map((stage, ladderIndex) => ({
          name: stage.name,
          ladderIndex,
          line: sites.get(stage.name)?.find((site) => site.file === relative)?.line,
        })).filter((entry) => entry.line !== undefined);

        const byLadder = owned.map(({ name, line }) => `${name}@${line}`).join(' → ');
        const bySource = [...owned]
          .sort((a, b) => a.line - b.line)
          .map(({ name, line }) => `${name}@${line}`)
          .join(' → ');

        assertEq(
          byLadder,
          bySource,
          `${relative} prints these stages in a different order than the ladder expects them; the ` +
            'climb would stall at the first disagreement and every canary would fail at the stage ' +
            'budget while the host was healthy',
        );
      }
    },
  },

  {
    name: 'VERIFY-004 every stage marker is printed exactly once by the harness',
    fn: () => {
      // Deliberately the guard that lets `readiness.js` NOT anchor to the exact log format. Matching
      // substrings means a reworded timing line still advances the ladder; it also means a DELETED
      // line silently never advances it. This case converts that second case into a gate failure, so
      // the cost of the loose match is paid here rather than by a canary hanging at 3/9.
      const sites = printSites();

      for (const stage of READINESS_STAGES) {
        const found = sites.get(stage.name) ?? [];

        assertEq(
          found.length,
          1,
          `stage '${stage.name}' (marker ${JSON.stringify(stage.marker)}) is printed ` +
            `${found.length} time(s) at ${JSON.stringify(found)}; zero means the ladder can never ` +
            'pass it, more than one means which print advances it depends on ordering',
        );
      }
    },
  },

  {
    name: 'VERIFY-004 the host stages are nested where the ladder places them',
    fn: () => {
      // The cross-file half of the order check. All Host stages are printed inside
      // `process-host.js`, so within-file monotonicity cannot see that they belong between
      // `workspace` and `events`. The `host.start took` line is where that nesting is observable.
      const sites = printSites();
      const lineIn = (name) =>
        sites.get(name).find((site) => site.file === STAGE_SOURCES[0]).line;

      const nested = hostStartLine();
      assertTrue(nested > 0, 'scenario-parallel.js must still report host.start, or nesting is unprovable');

      assertTrue(
        lineIn('workspace') < nested && nested < lineIn('events'),
        `host.start is reported at :${nested}, outside workspace@${lineIn('workspace')}..` +
          `events@${lineIn('events')}; the ladder places all Host stages between them`,
      );

      const hostStages = READINESS_STAGES.map((stage, index) => ({ name: stage.name, index })).filter(
        ({ name }) => sites.get(name).some((site) => site.file === STAGE_SOURCES[1]),
      );

      assertEq(
        hostStages.map(({ name }) => name).join(','),
        'host-bootstrap,port-bound,host-global,host-project-events,host-project',
        'the stages owned by process-host.js changed; the nesting argument above no longer applies',
      );
      assertEq(
        hostStages.map(({ index }) => index).join(','),
        `${hostStages[0].index},${hostStages[0].index + 1},${hostStages[0].index + 2},${hostStages[0].index + 3},${hostStages[0].index + 4}`,
        'the five Host stages must be adjacent in the ladder, since nothing is printed between them',
      );
    },
  },

  {
    name: 'VERIFY-004 no stage marker is a substring of another',
    fn: () => {
      // Substring matching plus a marker contained in another marker means one line advances two
      // stages, which makes the climb depend on which stage is checked first. Cheap to forbid, and
      // it is the shape a future stage named by extending an existing prefix would take.
      for (const outer of READINESS_STAGES) {
        for (const inner of READINESS_STAGES) {
          if (outer.name === inner.name) continue;
          assertTrue(
            !outer.marker.includes(inner.marker),
            `stage '${outer.name}' contains the marker of '${inner.name}'; one printed line would ` +
              'advance both and the ladder would skip a criterion',
          );
        }
      }
    },
  },

  {
    name: 'VERIFY-004 one buffer carrying several stages drains all of them',
    fn: () => {
      // Output arrives in blocks, so advancing one step per call would leave the ladder permanently
      // behind a child that prints three stages into one pipe write — and 「behind」 here means the
      // stage budget expires on work already done.
      const ladder = new ReadinessLadder();
      const advanced = ladder.observe(
        READINESS_STAGES.slice(0, 3)
          .map((stage) => `${stage.marker} 5ms`)
          .join('\n'),
      );

      assertEq(advanced.join(','), 'provider,workspace,host-bootstrap', 'a multi-stage buffer must drain');
      assertTrue(!ladder.isReady, 'three of nine stages is not ready');
    },
  },

  {
    name: 'VERIFY-004 a reprinted earlier stage does not reset the climb',
    fn: () => {
      // 「反复重连的 SSE 读者」 one layer earlier: a retried health check that reset the ladder would
      // renew the startup budget forever, and a host looping on a failing probe would look like a
      // host making progress. Monotonicity is what makes the per-stage budget a bound at all.
      const ladder = climb(['provider', 'workspace', 'port-bound']);
      const reached = ladder.describe();

      assertEq(
        ladder.observe(`${READINESS_STAGES[0].marker} 5ms`).join(','),
        '',
        'an earlier marker must advance nothing',
      );
      assertEq(ladder.describe().replace(/silent for \d+ms/, ''), reached.replace(/silent for \d+ms/, ''),
        'the position must be unchanged by a reprint');
    },
  },

  {
    name: 'VERIFY-004 a later marker arriving alone does not skip the stages before it',
    fn: () => {
      // The converse, and the reason the launcher can feed the whole accumulated buffer safely: the
      // ladder is a sequence, not a set. A child that printed only its last line would otherwise be
      // declared ready having proven nothing about the port, the provider or the event stream.
      const ladder = new ReadinessLadder();

      assertEq(ladder.observe(`${READINESS_STAGES[5].marker}`).join(','), '', 'ready alone proves nothing');
      assertTrue(!ladder.isReady, 'the ready bark alone must not satisfy the ladder');

      assertTrue(climb(allStageNames).isReady, 'the full climb in order must reach ready');
    },
  },

  {
    name: 'VERIFY-004 the diagnostic names the stage reached and the stage awaited',
    fn: () => {
      // Two different facts, and the pair is what makes a startup failure actionable: 「reached
      // provider, awaiting port-bound」 points at the Host's listen call, 「reached nothing」 points
      // at module load. The launcher's old message — "failed to emit ready" — was true of the
      // symptom and silent about both.
      assertTrue(
        /reached \(nothing\) \(0\/9\), awaiting provider/.test(new ReadinessLadder().describe()),
        `a fresh ladder must say it reached nothing: ${new ReadinessLadder().describe()}`,
      );

      const stalled = climb(['provider', 'workspace']).describe();
      assertTrue(
        /reached workspace \(2\/9\), awaiting host-bootstrap/.test(stalled),
        `a stalled climb must name both sides: ${stalled}`,
      );
      assertTrue(/silent for \d+ms/.test(stalled), `the dump must state the silence: ${stalled}`);

      const done = climb(allStageNames).describe();
      assertTrue(/awaiting \(ready\)/.test(done), `a finished climb must not claim to await a stage: ${done}`);
    },
  },

  {
    name: 'VERIFY-004 the stage budget is tighter than the total startup ceiling',
    fn: () => {
      // Detection of the one degradation no static check can prevent: 「延长静默窗口以掩盖竞态」.
      // Stated as the relation rather than the value so raising the stage budget past the ceiling —
      // which would restore the flat window the clause forbids — fails with the reason attached.
      // This relation case does not read the retired multi-canary launcher; the budgets live in
      // time-budget.js regardless of which process feeds the ladder.
      assertTrue(
        READINESS_STAGE_MS < CANARY_READY_MS,
        `the stage budget (${READINESS_STAGE_MS}ms) must be tighter than the total ceiling ` +
          `(${CANARY_READY_MS}ms), or one stage may consume the whole startup and nothing inside ` +
          'the window is a criterion',
      );
      assertEq(READINESS_STAGES.length, 9, 'the ladder is nine stages; the diagnostics above pin that shape');
    },
  },
];

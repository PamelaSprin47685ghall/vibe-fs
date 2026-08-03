/**
 * gate-budget-cases.mjs — the budget gate must be red on the defect it was written for.
 *
 * VERIFY-004 requires the wall-clock fallbacks to be centrally defined; `shock-anneal.md`
 * requires more than that of the gate enforcing it — 「门禁必须红过一次才算存在」. These cases are
 * that requirement made permanent: each negative case is a real defect shape measured in this
 * tree before package W1 migrated it, written into a temp file so the gate's refusal is proven
 * without editing the repo.
 *
 * `auditBudgets` is imported rather than the script being spawned, so a case can point the
 * whole rule set at a directory it built. The four pseudo-gates this package replaces were all
 * exercised only against the real tree, where they happened to be green — nobody had ever seen
 * one refuse anything.
 */

import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { assertEq, assertTrue, tmpScenarioDir } from './gate-lib.mjs';
import { auditBudgets, scopedFiles } from '../../../scripts/budget-gate.mjs';
import * as budget from '../time-budget.js';

/**
 * Write one file into a fresh temp dir and audit exactly it.
 *
 * Auditing a single explicit file rather than a walked directory keeps the anti-drift rule out
 * of the way: that rule reports every table constant the scanned set does not reference, which
 * for a one-file fixture is all of them. Those are filtered out here and asserted separately.
 */
const auditFixture = (source) => {
  const file = join(tmpScenarioDir(), 'fixture.mjs');
  writeFileSync(file, source);
  return auditBudgets([file]).filter((violation) => !violation.detail.includes('referenced nowhere'));
};

/** The violations a fixture produces, as `line detail` strings, for readable assertions. */
const reported = (source) => auditFixture(source).map(({ line, detail }) => `${line} ${detail}`);

const rejects = (source, fragment) => {
  const violations = auditFixture(source);
  assertTrue(violations.length > 0, `expected a violation for:\n${source}`);
  assertTrue(
    violations.some((violation) => violation.detail.includes(fragment)),
    `expected a violation mentioning '${fragment}', got: ${violations.map((v) => v.detail).join(' | ')}`,
  );
  return violations;
};

const accepts = (source) => {
  const violations = auditFixture(source);
  assertEq(violations.length, 0, `expected no violation, got: ${violations.map((v) => v.detail).join(' | ')}`);
};

export const budgetCases = [
  {
    name: 'VERIFY-004 the real tree declares no timing budget outside time-budget.js',
    fn: () => {
      // The positive half. Runs the same audit `npm run gate:budget` runs, so `test:harness`
      // cannot be green while the gate is red — the split that let a scenario be committed
      // unformatted while the harness passed (see gate-source-cases).
      const files = scopedFiles();
      assertTrue(files.length > 0, 'the scope patterns must match real files');

      const violations = auditBudgets(files);
      assertEq(
        violations.length,
        0,
        violations.map(({ file, line, detail }) => `${file}:${line} ${detail}`).join('\n  '),
      );
    },
  },

  {
    name: 'VERIFY-004 a literal passed to a timer primitive is refused',
    fn: () => {
      // Measured at `watchdog.js:84` and `scenario-parallel.js:162`: the same 3000ms diagnostic
      // race, written twice, in two files, by two edits that could not see each other. That is
      // the concrete cost of a scattered budget — not that 3000 is wrong, but that changing it
      // requires knowing both places exist.
      const violations = rejects(
        `const fn = () => {};\nsetTimeout(fn, 5000);\n`,
        'passed straight to a timer primitive',
      );
      assertEq(violations[0].line, 2, 'the message must name the line');
      assertTrue(violations[0].file.endsWith('fixture.mjs'), 'and the file');

      rejects(`setInterval(() => {}, 30000);\n`, 'passed straight to a timer primitive');
      rejects(`run({ signal: AbortSignal.timeout(300000) });\n`, 'AbortSignal.timeout');
    },
  },

  {
    name: 'VERIFY-004 a timeout property or parameter default is refused',
    fn: () => {
      // Measured at `scenario-http.js:85,104,114`, `process-host.js:77,103`,
      // `stability-checker.js:86,152,170`. The parameter-default form is the one worth pinning:
      // it reads like an API detail rather than a budget, which is how eight of them
      // accumulated in files nobody thought of as owning a timeout.
      rejects(`const opts = { timeoutMs: 4000 };\n`, 'a timeout property');
      rejects(`export const f = (ms = 4000) => ms;\nconst g = (timeout = 7000) => timeout;\n`, 'a timeout property');
      rejects(`const t = opts.startTimeoutMs || 5000;\n`, 'a timeout property');
    },
  },

  {
    name: 'VERIFY-004 a bound-declaring name may not be assigned a literal',
    fn: () => {
      // Measured at `run-canary-staggered.mjs:35`, `runner.mjs:27,30`,
      // `process-host-utils.js:21-25`, `process-host.js:35-37` — where 2000 was declared twice
      // under two spellings in two files.
      const violations = rejects(
        `const FOO_TIMEOUT_MS = 7000;\n`,
        'assigned to FOO_TIMEOUT_MS, a name that declares itself a bound',
      );
      assertEq(violations[0].line, 1);

      rejects(`const silenceWindow = 2500;\n`, 'assigned to silenceWindow');
      rejects(`let deadline = 9000;\n`, 'assigned to deadline');
      rejects(`const graceMs = 1500;\n`, 'assigned to graceMs');
    },
  },

  {
    name: 'VERIFY-004 the name in the message is the name that was assigned',
    fn: () => {
      // Measured while writing this gate: an earlier expression pattern crossed commas, so
      // `{ termGraceMs = 500, killGraceMs = 1000 }` credited the 1000 to `termGraceMs`. The
      // message's whole job is telling the author which constant to reach for, and a message
      // naming the wrong one sends them to edit a value that is already correct.
      const messages = reported(`const f = ({ termGraceMs = 500, killGraceMs = 1000 } = {}) => termGraceMs + killGraceMs;\n`);
      assertEq(messages.length, 1, `exactly one violation: ${messages.join(' | ')}`);
      assertTrue(messages[0].includes('assigned to killGraceMs'), messages[0]);
      assertTrue(!messages[0].includes('termGraceMs'), 'the sub-threshold sibling must not be blamed');
    },
  },

  {
    name: 'VERIFY-004 one literal is reported once even in two timing positions',
    fn: () => {
      // `const startTimeout = opts.startTimeoutMs || 5000` is simultaneously a timeout fallback
      // and a bound-declaring assignment. The count is what a reader uses to judge whether the
      // migration is finished, so double-reporting would make a finished migration look
      // unfinished — and an unfinished one look worse than it is.
      const messages = reported(`const startTimeout = opts.startTimeoutMs || 5000;\n`);
      assertEq(messages.length, 1, `expected one violation, got: ${messages.join(' | ')}`);
    },
  },

  {
    name: 'VERIFY-004 a string may not restate a budget as a duration',
    fn: () => {
      // Measured at `run-canary-staggered.mjs:244-245`: two failure messages read "within 10s"
      // while the timer read 10000. Three facts, one value, and the sentence is the half an
      // operator actually reads — so the drift would be invisible in exactly the direction that
      // matters. Derived from the table, not a hand-written phrase list, so a budget added later
      // is covered without anyone remembering this rule exists.
      //
      // The fixtures BUILD their forbidden strings from the table rather than spelling them,
      // which is not evasion of this file's own rule but obedience to it: the case file is in
      // scope, so writing "within 10s" here would be the very duplication under test. It also
      // makes the negative cases track the table — retuning a budget cannot leave a fixture
      // testing a value nothing uses.
      const seconds = budget.CANARY_READY_MS / 1000;
      const violations = rejects(
        `const reason = "ready timeout (failed to emit ready within ${seconds}s)";\n`,
        'interpolate the constant instead',
      );
      assertEq(violations[0].line, 1);
      assertTrue(violations[0].detail.includes('CANARY_READY_MS'), violations[0].detail);

      rejects(`const label = "silent for ${budget.WATCHDOG_TIMEOUT_MS / 1000}s";\n`, 'WATCHDOG_TIMEOUT_MS');
      rejects(`const label = \`raced host.stop for ${budget.DIAGNOSTIC_RACE_MS}ms\`;\n`, 'DIAGNOSTIC_RACE_MS');
    },
  },

  {
    name: 'VERIFY-004 a poll slice under the threshold is not a budget',
    fn: () => {
      // The discriminator, stated as an acceptance. A slice must poll faster than the budget
      // bounding it or it races that budget, so a legitimate slice is sub-threshold by
      // construction: `canary-driver.mjs` slices at 500 under a 3000 silence budget, the listen
      // poll at 50, the socket retry at 30. Without this case the gate could tighten to "no
      // numbers near timers" and nobody would notice it had stopped being a semantic rule.
      accepts(`await new Promise((r) => setTimeout(r, 500));\nconst POLL_INTERVAL_MS = 50;\n`);
      accepts(`const opts = { timeoutMs: 999 };\n`);
    },
  },

  {
    name: 'VERIFY-004 comments and unrelated numbers are not budgets',
    fn: () => {
      // A gate that flagged its own explanation would be worked around within a day, and this
      // repo's prose is full of measured millisecond figures — the paragraphs above cite several
      // on purpose. Ports, dates and sizes are the other half: `9999` in a provider URL and
      // `100000` in a context-limit fixture are not durations.
      accepts(
        `// the watchdog raced host.stop for ${budget.DIAGNOSTIC_RACE_MS}ms before this was centralized\n` +
          `/* ${budget.CANARY_READY_MS / 1000}s ready window */\n` +
          `const x = 1;\n`,
      );
      accepts(`const url = 'http://127.0.0.1:9999/v1';\nconst limit = { context: 100000 };\n`);
    },
  },

  {
    name: 'VERIFY-004 every budget in the table is consumed somewhere in scope',
    fn: () => {
      // Anti-drift. The failure mode this repo keeps producing is not a wrong rule but a rule
      // with no call site: `buildAttemptExecutionProfile` sat at zero callers for eight packages
      // while its clause read CONTRADICTS, and `epochCold` passed every mutation it existed to
      // catch. A constant in this table that nothing imports has the same shape — it looks like
      // the single source and governs nothing.
      const dead = auditBudgets(scopedFiles()).filter((v) => v.detail.includes('referenced nowhere'));
      assertEq(dead.length, 0, dead.map(({ detail }) => detail).join('\n  '));
    },
  },

  {
    name: 'VERIFY-004 the budget module is the only file exempt from its own rules',
    fn: () => {
      // The exclusion list is two entries and both are load-bearing, so both are asserted
      // rather than trusted. A third entry added later would be the exemption channel this gate
      // refuses to have; it would show up here as a scoped file that is silently unscanned.
      const files = scopedFiles();
      assertEq(
        files.filter((file) => file.endsWith('time-budget.js')).length,
        0,
        'the budget module must not audit itself',
      );
      assertTrue(
        files.includes('tests/unit/runner.mjs'),
        'the unit runner is in scope: its two budgets are the ones VERIFY-004 names directly',
      );
      assertTrue(
        files.some((file) => file.startsWith('scripts/')) && files.some((file) => file.startsWith('tests/e2e/')),
        'both roots must be walked',
      );
    },
  },

  {
    name: 'VERIFY-004 no importer of the deleted watchdog-constants.js survives',
    fn: () => {
      // The deleted module held WATCHDOG_TIMEOUT_MS alone and was imported by seven files.
      // Deleting it while leaving one importer behind fails at module load — but only for
      // whichever canary imported it, and 11 of 16 canaries are currently red for unrelated
      // reasons, so that failure would have been attributed to the noise it was hiding in.
      //
      // Matched as an import SPECIFIER, not as a word: prose naming the retired module (this
      // comment, the migration ledger) is not an importer, and a check that could not tell the
      // difference would be satisfied by deleting the explanation.
      const importsRetired = /from\s*['"][^'"]*watchdog-constants/;
      const stale = scopedFiles().filter((file) => importsRetired.test(readFileSync(file, 'utf8')));
      assertEq(stale.length, 0, `still importing the deleted module: ${stale.join(', ')}`);
    },
  },

  {
    name: 'VERIFY-004 every centralized budget holds its measured value',
    fn: () => {
      // DETECTION, not prevention — and the distinction is the point. 「延长静默窗口或测试超时以
      // 掩盖竞态」 cannot be statically prevented: raising a number is always legal code. What can
      // be arranged is that raising one is impossible to do quietly, because this assertion
      // fails and the diff shows a number changing next to the clause forbidding it. The value
      // of the case is the conversation it forces, not the change it blocks.
      //
      // The WHOLE object is compared, not field by field. AGENTS.md §6: mjs has no compile-time
      // rename protection, so `assertEq(budget.WATCHDOG_TIMEOUT_MS, 3000)` on a renamed or
      // deleted field reads `undefined` and reports a value mismatch — which looks like a
      // retuning to review, when it is a vanished budget. Comparing the object catches added,
      // removed and renamed constants in the same assertion.
      const expected = {
        LITERAL_BUDGET_THRESHOLD_MS: 1000,
        WATCHDOG_TIMEOUT_MS: 3000,
        DIAGNOSTIC_RACE_MS: 3000,
        CANARY_READY_MS: 10000,
        READINESS_STAGE_MS: 4000,
        CANARY_TIMEOUT_MS: 90000,
        WAIT_FACT_WINDOW_MS: 120000,
        FORK_COMPLETION_WINDOW_MS: 10000,
        FORK_RECONCILE_SLICE_MS: 2000,
        PER_TEST_TIMEOUT_MS: 1000,
        SUITE_BACKSTOP_MS: 300000,
        UNIT_VERDICT_SILENCE_MS: 3000,
        DEFAULT_AWAIT_TIMEOUT_MS: 1000,
        DEFAULT_NEVER_TIMEOUT_MS: 5000,
        GATE_PROBE_TIMEOUT_MS: 3000,
        GATE_HOST_START_TIMEOUT_MS: 1000,
        TEARDOWN_IDLE_MS: 2000,
        SIGTERM_GRACE_MS: 5000,
        SIGKILL_GRACE_MS: 1000,
        PROCESS_TREE_TIMEOUT_MS: 2000,
        SOCKET_CHECK_TIMEOUT_MS: 2000,
        HOST_START_TIMEOUT_MS: 5000,
        ORPHAN_MIN_AGE_MS: 5000,
        LEDGER_ENTRY_TTL_MS: 1800000,
        SCENARIO_SUITE_WINDOW_MS: 500000,
        STABILITY_SCENARIO_TIMEOUT_MS: 30000,
        STABILITY_GATE_WINDOW_MS: 300000,
        STABILITY_MIN_RUN_MS: 5000,
        ENFORCER_POLL_SLICE_MS: 500,
      };

      const actual = Object.fromEntries(
        Object.entries(budget).filter(([, value]) => typeof value === 'number'),
      );

      // Sorted so the comparison is over the SET of budgets, not their declaration order —
      // moving a constant between sections is a readability edit, not a budget change.
      const canonical = (table) =>
        JSON.stringify(Object.fromEntries(Object.entries(table).sort(([a], [b]) => (a < b ? -1 : 1))), null, 1);

      assertEq(canonical(actual), canonical(expected), 'the budget table changed');
    },
  },

  {
    name: 'VERIFY-004 no budget is 兜底-only for a criterion that has a causal signal',
    fn: () => {
      // Not a value check but a relation, and the one relation VERIFY-004 states outright: the
      // silence budget must be the tighter bound, because the wall-clock ceiling 「不得是唯一或
      // 首要的判据」. A future edit that raises WATCHDOG_TIMEOUT_MS toward CANARY_TIMEOUT_MS
      // erodes exactly that ordering, and the value pin above would report it as one number
      // changing without saying why it matters.
      assertTrue(
        budget.WATCHDOG_TIMEOUT_MS < budget.CANARY_TIMEOUT_MS,
        'the silence budget must be tighter than the process fallback, or the fallback becomes primary',
      );
      assertTrue(
        budget.WATCHDOG_TIMEOUT_MS < budget.WAIT_FACT_WINDOW_MS,
        'same for the waitFact window: it is a fallback, the watchdog is the criterion',
      );
      assertTrue(
        budget.PER_TEST_TIMEOUT_MS < budget.SUITE_BACKSTOP_MS,
        'a per-test bound at or above the suite ceiling would make the suite ceiling the only hang criterion',
      );
      // W4's ordering, and the reason the silence window is not simply the per-test bound: a verdict
      // arrives only after a test finishes, and node:test's timeout is a verdict line rather than an
      // abort line, so an overrunning test keeps running. A window at or below the per-test bound
      // would declare that legitimate overrun a hang.
      assertTrue(
        budget.UNIT_VERDICT_SILENCE_MS > budget.PER_TEST_TIMEOUT_MS,
        'the verdict-silence window must cover one whole test plus jitter, or an overrun reads as a hang',
      );
      assertTrue(
        budget.UNIT_VERDICT_SILENCE_MS < budget.SUITE_BACKSTOP_MS,
        'the silence window is the primary criterion; the suite ceiling is only 兜底 (VERIFY-004)',
      );
      assertTrue(
        budget.LITERAL_BUDGET_THRESHOLD_MS <= budget.WATCHDOG_TIMEOUT_MS,
        'the gate threshold must not exceed the tightest budget, or that budget itself would read as a poll slice',
      );
      // W5's ordering. `CANARY_READY_MS` was the startup criterion and is now the total 兜底; a
      // per-stage budget at or above it would restore the flat window the clause forbids, since one
      // stage could then consume the whole startup and nothing inside it would be a criterion.
      assertTrue(
        budget.READINESS_STAGE_MS < budget.CANARY_READY_MS,
        'a stage budget at or above the total startup 兜底 makes the ladder decorative (VERIFY-004)',
      );
    },
  },
];

/**
 * gate-budget-cases.mjs — budget table relations retained after scripts/budget-gate.mjs removal.
 *
 * 0.5.3 retired the budget-gate scanner. Cases that only imported that scanner were removed.
 * Cases that assert time-budget.js relations (no script dependency) stay for VERIFY-004 coverage.
 */

import { assertEq, assertTrue } from './lib.mjs';
import * as budget from '../../e2e/support/time-budget.js';

export const budgetCases = [
  {
    name: 'VERIFY-004 every centralized budget holds its measured value',
    fn: () => {
      const expected = {
        LITERAL_BUDGET_THRESHOLD_MS: 1000,
        WATCHDOG_TIMEOUT_MS: 5000,
        DIAGNOSTIC_RACE_MS: 3000,
        CANARY_READY_MS: 10000,
        READINESS_STAGE_MS: 4000,
        CANARY_TIMEOUT_MS: 90000,
        WAIT_FACT_WINDOW_MS: 120000,
        FORK_COMPLETION_WINDOW_MS: 10000,
        FORK_RECONCILE_SLICE_MS: 2000,
        PER_TEST_TIMEOUT_MS: 2500,
        SUITE_BACKSTOP_MS: 300000,
        UNIT_VERDICT_SILENCE_MS: 5000,
        UNIT_RUNNER_PROBE_PER_TEST_MS: 2000,
        UNIT_RUNNER_PROBE_SILENCE_MS: 7000,
        UNIT_RUNNER_PROBE_TIGHT_SILENCE_MS: 3500,
        DEFAULT_AWAIT_TIMEOUT_MS: 1000,
        DEFAULT_NEVER_TIMEOUT_MS: 5000,
        GATE_PROBE_TIMEOUT_MS: 3000,
        GATE_HOST_START_TIMEOUT_MS: 5000,
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

      const canonical = (table) =>
        JSON.stringify(Object.fromEntries(Object.entries(table).sort(([a], [b]) => (a < b ? -1 : 1))), null, 1);

      assertEq(canonical(actual), canonical(expected), 'the budget table changed');
    },
  },

  {
    name: 'VERIFY-004 no budget is 兜底-only for a criterion that has a causal signal',
    fn: () => {
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
      assertTrue(
        budget.UNIT_VERDICT_SILENCE_MS > budget.PER_TEST_TIMEOUT_MS,
        'the verdict-silence window must cover one whole test plus jitter, or an overrun reads as a hang',
      );
      assertTrue(
        budget.UNIT_VERDICT_SILENCE_MS < budget.SUITE_BACKSTOP_MS,
        'the silence window is the primary criterion; the suite ceiling is only 兜底 (VERIFY-004)',
      );
      assertTrue(
        budget.UNIT_VERDICT_SILENCE_MS === budget.WATCHDOG_TIMEOUT_MS,
        budget.UNIT_RUNNER_PROBE_SILENCE_MS > budget.UNIT_RUNNER_PROBE_PER_TEST_MS,
        budget.UNIT_RUNNER_PROBE_TIGHT_SILENCE_MS <= budget.UNIT_RUNNER_PROBE_SILENCE_MS,
        'every suite dog starves on the same 3s window as e2e canary',
      );
      assertTrue(
        budget.LITERAL_BUDGET_THRESHOLD_MS <= budget.WATCHDOG_TIMEOUT_MS,
        'the gate threshold must not exceed the tightest budget, or that budget itself would read as a poll slice',
      );
      assertTrue(
        budget.READINESS_STAGE_MS < budget.CANARY_READY_MS,
        'a stage budget at or above the total startup 兜底 makes the ladder decorative (VERIFY-004)',
      );
    },
  },
];

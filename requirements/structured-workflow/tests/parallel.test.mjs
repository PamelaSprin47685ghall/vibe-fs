// tests/unit/Kernel/parallel.test.mjs — ARCH-009.
//
// `Parallel.mapBounded` is the only concurrency primitive the business layer may
// use, and `guide-contract.test.mjs` asserts no unbounded `Parallel.map*` sibling
// exists beside it. ARCH-009 is what those two facts come from.
//
// The clause was written BECAUSE of this file. Writing these tests surfaced a
// shared primitive with a real behavioural contract and no clause backing any of
// it, so the tests briefly carried a `mapBounded_` prefix — a name asserting
// nothing, since `ssot-lint` only reads `spec/` and would never catch a fabricated
// clause id in a test name. Rather than leave that gap, ARCH-009 now states the
// contract and every test here names it (supersedes record #2).
//
// Why it matters beyond correctness: unbounded fan-out makes a canary fail on
// machine load rather than on logic, and VERIFY-002 forbids papering over a race
// by raising a timeout. A bound that silently did not bind would move every
// scheduling failure into that category.
//
// Rebuilt from `tests-next/Flow/FlowTests.fs`, which stopped compiling at package
// X9. Three of its assertions are here (order, exception propagation, empty
// input); the concurrency ceiling and the cancellation cases are new.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as parallel from '../../../dist/Foundation/ParallelSurface.js'

const liveToken = parallel.liveToken
const cancelledToken = parallel.cancelledToken

/** Short enough to stay well inside the runner's 1000ms per-test ceiling. */
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

/** Runs `action` while recording how many calls were ever in flight at once. */
const withConcurrencyProbe = (action) => {
  const state = { inFlight: 0, peak: 0 }
  const probed = async (item, cancellation) => {
    state.inFlight += 1
    state.peak = Math.max(state.peak, state.inFlight)
    try {
      return await action(item, cancellation)
    } finally {
      state.inFlight -= 1
    }
  }
  return { probed, state }
}

// ── results are positional, never completion-ordered ────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_results_follow_input_order_not_completion_order', async () => {
  // The property a caller depends on: `results[i]` is the result for `items[i]`.
  // Here the FIRST item is the SLOWEST, so a implementation that appended on
  // completion would return the exact reverse.
  const items = [1, 2, 3, 4, 5, 6]
  const reverseDelays = async (item) => {
    await sleep((7 - item) * 6)
    return item * 10
  }

  assert.deepEqual(await parallel.mapBounded(6, reverseDelays, items), [10, 20, 30, 40, 50, 60])
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_order_holds_when_the_bound_forces_several_waves', async () => {
  // With max=2 over 6 items the work runs in three waves, so ordering has to
  // survive being reassembled across them rather than within one batch.
  const items = [1, 2, 3, 4, 5, 6]
  const jittered = async (item) => {
    await sleep(item % 2 === 0 ? 12 : 3)
    return `r${item}`
  }

  assert.deepEqual(await parallel.mapBounded(2, jittered, items), ['r1', 'r2', 'r3', 'r4', 'r5', 'r6'])
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_a_synchronous_action_still_yields_a_result_list', async () => {
  // Not every action awaits. An implementation keying results off a resolved
  // promise's callback ordering would degrade here without failing loudly.
  assert.deepEqual(await parallel.mapBounded(3, async (item) => item + 1, [1, 2, 3]), [2, 3, 4])
})

// ── the bound actually binds ─────────────────────────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_concurrency_never_exceeds_the_declared_maximum', async () => {
  const { probed, state } = withConcurrencyProbe(async (item) => {
    await sleep(15)
    return item
  })

  const items = [1, 2, 3, 4, 5, 6, 7, 8]
  const results = await parallel.mapBounded(3, probed, items)

  assert.deepEqual(results, items)
  assert.equal(state.peak, 3, 'the semaphore must admit exactly `maxConcurrency` at a time')
  assert.equal(state.inFlight, 0, 'every permit must be released')
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_a_maximum_of_one_serialises_the_work', async () => {
  // The degenerate bound is worth pinning separately: it is what a caller reaches
  // for when an action is not safe to run concurrently at all.
  const completions = []
  const { probed, state } = withConcurrencyProbe(async (item) => {
    await sleep(4)
    completions.push(item)
    return item
  })

  await parallel.mapBounded(1, probed, [1, 2, 3, 4])

  assert.equal(state.peak, 1)
  assert.deepEqual(completions, [1, 2, 3, 4], 'serialised work completes in submission order')
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_a_bound_above_the_item_count_is_not_an_error', async () => {
  const { probed, state } = withConcurrencyProbe(async (item) => {
    await sleep(8)
    return item
  })

  assert.deepEqual(await parallel.mapBounded(100, probed, [1, 2]), [1, 2])
  assert.equal(state.peak, 2, 'peak is bounded by the work available, not by the permit count')
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_a_rejection_returns_early_while_siblings_keep_running', async () => {
  // `Promise.all` semantics, pinned because it is easy to assume otherwise: the
  // call rejects as soon as ONE action throws, but the actions already admitted
  // are not cancelled — they run to completion in the background.
  //
  // A caller that treats the rejection as "nothing further will happen" is wrong.
  // There is no cancellation here to make it right either: `mapBounded` passes the
  // token to each action, so stopping siblings is the action's job, not the
  // combinator's.
  const state = { inFlight: 0, completed: [] }
  const action = async (item) => {
    state.inFlight += 1
    try {
      await sleep(20)
      if (item === 2) throw new Error('boom')
      state.completed.push(item)
      return item
    } finally {
      state.inFlight -= 1
    }
  }

  await assert.rejects(() => parallel.mapBounded(2, action, [1, 2, 3, 4]), /boom/)

  // At the moment the rejection surfaces, later items are still in flight.
  assert.equal(state.inFlight > 0, true, 'siblings are still running when the rejection returns')
  assert.deepEqual(state.completed, [1], 'only the work that finished before the throw is done')

  // Draining proves the `try/finally` around `Release()` did its job: every permit
  // came back. Without it a failing action would leak one, and a later call would
  // deadlock at a lower effective bound — a hang rather than an error.
  await sleep(120)
  assert.equal(state.inFlight, 0, 'every permit is released, including the thrown one')
  assert.deepEqual(state.completed, [1, 3, 4], 'siblings ran to completion after the rejection')

  // The semaphore is per call, so a fresh call is unaffected by the failed one.
  assert.deepEqual(await parallel.mapBounded(2, async (item) => item, [1, 2, 3]), [1, 2, 3])
})

// ── rejecting a nonsensical bound ────────────────────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_a_non_positive_maximum_is_refused_rather_than_defaulted', async () => {
  // Zero cannot mean "unbounded" and cannot mean "one". Both readings are a guess
  // at the caller's intent, and one of them silently removes the bound.
  for (const invalid of [0, -1, -100]) {
    await assert.rejects(
      () => parallel.mapBounded(invalid, async (item) => item, [1, 2]),
      /maxConcurrency must be greater than 0/,
      `maxConcurrency=${invalid} must be refused`,
    )
  }
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_an_empty_input_short_circuits_before_the_bound_is_checked', async () => {
  // Empty input returns before any permit is taken. Worth pinning because it is
  // the one path where the action is never called at all.
  let calls = 0
  const counted = async (item) => {
    calls += 1
    return item
  }

  assert.deepEqual(await parallel.mapBounded(2, counted, []), [])
  assert.equal(calls, 0)
})

// ── cancellation is observed at the permit, not inside the action ────────────

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_a_live_token_lets_the_work_run', async () => {
  assert.deepEqual(await parallel.mapBounded(2, async (item) => item * 3, [1, 2, 3], liveToken()), [3, 6, 9])
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_an_already_cancelled_token_stops_the_work_before_it_starts', async () => {
  // `WaitAsync` throws on a cancelled token before the action is invoked. That
  // ordering is the point: cancellation must not depend on the action choosing to
  // check, because a long-running action would then ignore it entirely.
  let calls = 0
  const counted = async (item) => {
    calls += 1
    return item
  }

  await assert.rejects(
    () => parallel.mapBounded(2, counted, [1, 2, 3], cancelledToken()),
    /cancel/i,
    'a cancelled token must reject rather than resolve with partial results',
  )
  assert.equal(calls, 0, 'no action may run under an already-cancelled token')
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_the_cancellation_token_reaches_the_action', async () => {
  // The action receives the token as its second argument, so a nested call can
  // propagate it. Losing it here is how an inner fan-out becomes uncancellable.
  const token = liveToken()
  const seen = []
  const capturing = async (item, cancellation) => {
    seen.push(cancellation)
    return item
  }

  await parallel.mapBounded(2, capturing, [1, 2], token)

  assert.equal(seen.length, 2)
  assert.equal(seen[0], token)
  assert.equal(seen[1], token)
})

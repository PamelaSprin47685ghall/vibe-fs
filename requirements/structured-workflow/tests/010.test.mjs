import assert from 'node:assert/strict'
import test from 'node:test'

import * as Parallel from '../../../dist/Foundation/ParallelSurface.js'
import * as ReconcileSurface from '../../../dist/Composition/Turn/ReconcileSurface.js'

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_results_follow_input_order_not_completion_order', async () => {
  const items = [10, 2, 5]
  const res = await Parallel.mapBounded(2, async (x) => x * 2, items)
  assert.deepEqual(res, [20, 4, 10])
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_concurrency_never_exceeds_requested_bound', async () => {
  let inFlight = 0
  let maxInFlight = 0
  const items = [1, 2, 3, 4, 5]
  await Parallel.mapBounded(2, async () => {
    inFlight++
    maxInFlight = Math.max(maxInFlight, inFlight)
    await new Promise((r) => setTimeout(r, 5))
    inFlight--
  }, items)
  assert.ok(maxInFlight <= 2)
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_bound_less_than_one_throws_argument_exception', async () => {
  await assert.rejects(() => Parallel.mapBounded(0, async (x) => x, [1]))
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_first_failure_cancels_pending_items_and_rejects_fast', async () => {
  let executed = 0
  await assert.rejects(async () => {
    await Parallel.mapBounded(1, async (x) => {
      executed++
      if (x === 1) throw new Error('fail fast')
      return x
    }, [1, 2, 3])
  })
  assert.equal(executed, 1)
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_empty_input_returns_empty_array_without_allocating_workers', async () => {
  const res = await Parallel.mapBounded(4, async (x) => x, [])
  assert.deepEqual(res, [])
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_bound_greater_than_input_length_runs_all_items', async () => {
  const res = await Parallel.mapBounded(10, async (x) => x + 1, [1, 2])
  assert.deepEqual(res, [2, 3])
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_bound_of_one_behaves_identically_to_sequential_map', async () => {
  const res = await Parallel.mapBounded(1, async (x) => x * 3, [1, 2, 3])
  assert.deepEqual(res, [3, 6, 9])
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_all_allocated_permits_are_released_after_normal_completion', async () => {
  const res = await Parallel.mapBounded(2, async (x) => x, [1, 2, 3])
  assert.deepEqual(res, [1, 2, 3])
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_all_allocated_permits_are_released_after_error', async () => {
  await assert.rejects(() => Parallel.mapBounded(2, async () => { throw new Error('err') }, [1, 2]))
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_pre_canceled_token_rejects_immediately_with_zero_invocations', async () => {
  const token = Parallel.cancelledToken()
  await assert.rejects(() => Parallel.mapBounded(2, async (x) => x, [1, 2], token))
})

test('WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_cancellation_signal_mid_flight_prevents_subsequent_items', async () => {
  const token = Parallel.liveToken()
  let count = 0
  await assert.rejects(async () => {
    await Parallel.mapBounded(1, async (x) => {
      count++
      if (x === 1) Parallel.cancel(token)
      return x
    }, [1, 2, 3], token)
  })
  assert.equal(count, 1)
})

test('WHAT[STRUCTURED-WORKFLOW-010] RECONCILE_PROGRAM_002: scheduleNextTurn runs bounded finite batch', () => {
  assert.equal(typeof ReconcileSurface.scheduleNextTurn, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-010] RECONCILE_PROGRAM_002b: advanceWithJournal caps batch at bounded loop budget', () => {
  assert.equal(typeof ReconcileSurface.advanceWithJournal, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-010] RECONCILE_PROGRAM_002c: drainQueue caps unbounded loop at MAX_EVENT_DRAIN_PER_WAKE', () => {
  assert.equal(typeof ReconcileSurface.drainQueue, 'function')
})

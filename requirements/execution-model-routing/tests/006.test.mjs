import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const {
  createRuntime,
  acquireExecutionAdmission,
  executionAdmissionTarget,
  commitExecutionAdmission,
  tryLease,
  endProviderStep,
  takeProviderRunTarget,
  snapshotOccupied,
  capacitySnapshot,
} = routing

const target = (model = 'provider/shared', reasoning = 'none') => ({ model, reasoning })
const key = (value) => `${value.model}|${value.reasoning}`
const acquireManaged = async (runtime, sessionId, physicalUserMessageId, role, participant, lenderSessionId = null) => {
  const acquisition = await acquireExecutionAdmission(
    runtime,
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    lenderSessionId,
  )
  if (acquisition.kind !== 'Acquired') return { kind: acquisition.kind, target: null }

  const projected = executionAdmissionTarget(runtime, acquisition.lease)
  const observed = {
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    target: projected,
  }
  const settlement = commitExecutionAdmission(runtime, acquisition.lease, observed)
  assert.ok(['Applied', 'AlreadyApplied'].includes(settlement.kind))
  return { kind: 'Acquired', target: projected }
}
const acquireTarget = async (...args) => {
  const outcome = await acquireManaged(...args)
  assert.equal(outcome.kind, 'Acquired')
  return outcome.target
}

const templateUrl = new URL('../../../resources/wanxiangshu.mjs', import.meta.url)

test('WHAT[EMR-006] EMR_006_recommended_template_prefers_previous_candidate_when_provider_has_capacity', async () => {
  const { default: scheduler } = await import(`${templateUrl.href}?previous=${Date.now()}`)
  const { invokeScheduler } = await import('../../../dist/OpenCode/Host/ModelRoutingSurface.js')
  const route = (role, running, previous = null) => invokeScheduler(scheduler, role, running, previous)
  const previous = { model: 'neuralwatt/glm-5.2-flex', reasoning: 'high' }

  assert.deepEqual(route('coder', [], previous), previous)

  const neuralwattFull = Array.from({ length: 4 }, () => ({
    model: 'neuralwatt/another-model',
    reasoning: 'none',
  }))
  assert.equal(
    route('coder', neuralwattFull, previous).model,
    'cursor/cursor-grok-4.6-xhigh',
    'when the previous provider is full, the next template candidate applies',
  )
})

test('WHAT[EMR-006] EMR_006_same_physical_message_retry_reuses_target_without_scheduler_rerun', async () => {
  const seen = []
  const runtime = createRuntime((_role, running) => {
    seen.push(running.map((item) => `${item.model}|${item.reasoning}`))
    return target()
  })

  await acquireTarget(runtime, 'session-a', 'msg-1', 'coder', 'alice')
  await acquireTarget(runtime, 'session-a', 'msg-1', 'coder', 'alice')
  await acquireTarget(runtime, 'session-a', 'msg-2', 'coder', 'alice')

  assert.deepEqual(seen, [[], ['provider/shared|none']], 'same physical material reuses without a scheduler rerun; the superseding fresh schedule observes the replaced occupancy')
})

test('WHAT[EMR-006] EMR_006_new_physical_message_supersedes_old_A_B_occupancy_without_idle', async () => {
  const runtime = createRuntime((role) => target(`provider/${role}`))

  const a = await acquireTarget(runtime, 'session', 'msg-a', 'coder', 'alice')
  assert.equal(a.model, 'provider/coder')
  assert.equal(snapshotOccupied(runtime).length, 1)

  const b = await acquireTarget(runtime, 'session', 'msg-b', 'inspector', 'alice')
  assert.equal(b.model, 'provider/inspector')
  assert.equal(snapshotOccupied(runtime).length, 1, 'one reusable session can own only one current physical execution slot')
  assert.equal(tryLease(runtime, 'session', 'msg-a', 'coder', 'alice', null), null, 'superseded physical material no longer owns a lease')
  assert.equal(key(tryLease(runtime, 'session', 'msg-b', 'inspector', 'alice', null)), 'provider/inspector|none')
})

test('WHAT[EMR-006] EMR_006_supersede_replaces_the_exact_capacity_owner', async () => {
  const runtime = createRuntime((role) => target(`provider/${role}`))

  await acquireTarget(runtime, 'session', 'msg-a', 'coder', 'alice')
  await acquireTarget(runtime, 'session', 'msg-b', 'inspector', 'alice')

  const snapshot = capacitySnapshot(runtime)
  assert.equal(snapshot.tokens.length, 1)
  assert.deepEqual(snapshot.tokens[0].owner, {
    sessionId: 'session',
    physicalUserMessageId: 'msg-b',
    role: 'inspector',
    participant: 'alice',
  })
  assert.deepEqual(snapshot.executions, [
    {
      sessionId: 'session',
      physicalUserMessageId: 'msg-b',
      role: 'inspector',
      participant: 'alice',
    },
  ])
})

test('WHAT[EMR-006] EMR_006_only_the_current_run_witness_resolves_a_target', async () => {
  let scheduled = 0
  const runtime = createRuntime(() => target(`provider-${++scheduled}/model`))

  await acquireTarget(runtime, 'session', 'msg-a', 'manager', 'alice')
  endProviderStep(runtime, 'session', 'msg-a', 'run-a')

  const current = await acquireTarget(runtime, 'session', 'msg-b', 'manager', 'alice')
  endProviderStep(runtime, 'session', 'msg-b', 'run-b')

  assert.equal(takeProviderRunTarget(runtime, 'run-a'), null, 'superseded run cannot poison the current target')
  assert.equal(takeProviderRunTarget(runtime, 'run-b').model, current.model)
  assert.equal(takeProviderRunTarget(runtime, 'run-b'), null, 'the exact witness is single-consumption')
  assert.equal(takeProviderRunTarget(runtime, 'no-such-run'), null, 'an unknown run resolves nothing')
})

test('WHAT[EMR-006] EMR_006_same_physical_message_cannot_change_role', async () => {
  const runtime = createRuntime(() => target())
  await acquireTarget(runtime, 'session', 'msg-1', 'coder', 'alice')

  await assert.rejects(
    acquireTarget(runtime, 'session', 'msg-1', 'inspector', 'alice'),
    /physical execution .* changed role/i,
  )
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-006] EMR_006_same_physical_message_cannot_change_participant', async () => {
  const runtime = createRuntime(() => target())
  await acquireTarget(runtime, 'session', 'msg-1', 'coder', 'alice')

  await assert.rejects(
    acquireTarget(runtime, 'session', 'msg-1', 'coder', 'bob'),
    /physical execution .* changed participant/i,
  )
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-006] EMR_006_lease_is_stable_only_for_one_physical_user_material', async () => {
  let calls = 0
  const runtime = createRuntime(() => target(`provider/model-${++calls}`, 'low'))

  const first = await acquireTarget(runtime, 'session', 'msg-1', 'coder', 'alice')
  const retry = await acquireTarget(runtime, 'session', 'msg-1', 'coder', 'alice')

  assert.equal(first.model, 'provider/model-1')
  assert.equal(retry.model, 'provider/model-1')
  assert.equal(calls, 1)

  const nextMaterial = await acquireTarget(runtime, 'session', 'msg-2', 'coder', 'alice')
  assert.equal(nextMaterial.model, 'provider/model-2', 'new physical material gets a fresh lease even without idle')
  assert.equal(calls, 2)
})

test('WHAT[EMR-006] EMR_006_fresh_physical_receives_the_replaced_target_as_previous', async () => {
  const seenPrevious = []
  let next = 0
  const runtime = createRuntime((_role, _running, previous) => {
    seenPrevious.push(previous)
    return previous ?? target(`provider/model-${++next}`, 'low')
  })

  const first = await acquireTarget(runtime, 'continued', 'msg-1', 'coder', 'alice')
  assert.equal(first.model, 'provider/model-1')

  const continued = await acquireTarget(runtime, 'continued', 'msg-2', 'coder', 'alice')
  assert.deepEqual(continued, first, 'the atomically replaced active physical supplies the previous target')
  assert.deepEqual(seenPrevious, [null, first])
})

test('WHAT[EMR-006] EMR_006_released_session_supplies_no_previous_target', async () => {
  const seenPrevious = []
  let next = 0
  const runtime = createRuntime((_role, _running, previous) => {
    seenPrevious.push(previous)
    return previous ?? target(`provider/model-${++next}`, 'low')
  })

  await acquireTarget(runtime, 'continued', 'msg-1', 'coder', 'alice')
  routing.releasePhysicalExecution(runtime, 'continued', 'msg-1')

  const rebuilt = await acquireTarget(runtime, 'continued', 'msg-2', 'coder', 'alice')
  assert.equal(rebuilt.model, 'provider/model-2', 'no session-history cache survives an exact terminal release')
  assert.deepEqual(seenPrevious, [null, null])
})

test('WHAT[EMR-006] EMR_006_unrelated_session_supplies_no_previous_target', async () => {
  const seenPrevious = []
  let next = 0
  const runtime = createRuntime((_role, _running, previous) => {
    seenPrevious.push(previous)
    return previous ?? target(`provider/model-${++next}`, 'low')
  })

  const first = await acquireTarget(runtime, 'session-a', 'msg-1', 'coder', 'alice')
  assert.equal(first.model, 'provider/model-1')

  const fresh = await acquireTarget(runtime, 'session-b', 'msg-1', 'coder', 'bob')
  assert.equal(fresh.model, 'provider/model-2', 'another session never inherits a previous target')
  assert.deepEqual(seenPrevious, [null, null])
})

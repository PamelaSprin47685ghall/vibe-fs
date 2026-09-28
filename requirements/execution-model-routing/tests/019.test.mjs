import assert from 'node:assert/strict'
import test from 'node:test'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const firstTarget = { model: 'provider/first', reasoning: 'high' }
const changedTarget = { model: 'other/changed', reasoning: 'none' }
const acquire = (runtime, physical) => routing.beginExecutionAdmission(runtime, 'devops-session', physical, 'devops', 'devops', null)
const commit = (runtime, acquired, physical) => routing.commitExecutionAdmission(runtime, acquired.lease, {
  sessionId: 'devops-session', physicalUserMessageId: physical,
  role: 'devops', participant: 'devops', target: routing.executionAdmissionTarget(runtime, acquired.lease),
})

test('WHAT[execution-model-routing-019] local DevOps binding rejects conflicting explicit rebind while original target remains available', async () => {
  const runtime = routing.createRuntime(() => firstTarget)
  for (const physical of ['first', 'next', 'third']) {
    const acquired = await acquire(runtime, physical)
    assert.equal(acquired.kind, 'Acquired')
    assert.deepEqual(routing.executionAdmissionTarget(runtime, acquired.lease), firstTarget)
    assert.deepEqual(commit(runtime, acquired, physical), { kind: 'Applied' })
    assert.throws(() => routing.bindDevopsTarget(runtime, 'devops-session', changedTarget), /immutable/)
    assert.throws(() => routing.commitExecutionAdmission(runtime, acquired.lease, {
      sessionId: 'devops-session', physicalUserMessageId: physical,
      role: 'devops', participant: 'devops', target: changedTarget,
    }), /immutable/)
    assert.deepEqual(routing.boundDevopsTarget(runtime, 'devops-session'), firstTarget)
    routing.releasePhysicalExecution(runtime, 'devops-session', physical)
  }
})

test('WHAT[execution-model-routing-019] changed scheduler cannot overwrite a previously bound DevOps target', { todo: 'GAP-129: current availability check allows changing the supposedly immutable binding; 34-D1 needs decision' }, async () => {
  let selected = firstTarget
  const runtime = routing.createRuntime(() => selected)
  const first = await acquire(runtime, 'first')
  assert.equal(first.kind, 'Acquired')
  assert.deepEqual(commit(runtime, first, 'first'), { kind: 'Applied' })
  routing.releasePhysicalExecution(runtime, 'devops-session', 'first')

  selected = changedTarget
  try {
    const next = await acquire(runtime, 'next')
    if (next.kind === 'Acquired') {
      assert.deepEqual(routing.executionAdmissionTarget(runtime, next.lease), firstTarget)
    } else {
      assert.equal(next.kind, 'Queued')
      routing.cancelPendingExecution(runtime, 'devops-session')
    }
  } catch (error) {
    if (error?.code === 'ERR_ASSERTION') throw error
    assert.match(String(error), /immutable/)
  } finally {
    routing.releasePhysicalExecution(runtime, 'devops-session', 'next')
  }
  assert.deepEqual(routing.boundDevopsTarget(runtime, 'devops-session'), firstTarget)
})

test.todo('WHAT[execution-model-routing-019] fixed road binding persists across real restart and physical-session replacement without model drift (GAP-129)')

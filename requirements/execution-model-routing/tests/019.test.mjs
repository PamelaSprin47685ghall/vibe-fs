import assert from 'node:assert/strict'
import test from 'node:test'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const templateUrl = new URL('../../../resources/wanxiangshu.mjs', import.meta.url)

test('WHAT[execution-model-routing-019] fixed DevOps model binding is immutable and cannot be changed via resume', async () => {
  const { default: route } = await import(`${templateUrl.href}?test=${Date.now()}`)
  const boundDevopsTarget = { model: 'provider/fixed-devops', reasoning: 'high' }

  // On resume/continuation, previous target must be strictly preserved
  const resumedTarget = route('devops', [boundDevopsTarget], boundDevopsTarget)
  assert.deepEqual(resumedTarget, boundDevopsTarget, 'DevOps model binding must remain immutable on resume')

  // Real ModelRoutingSurface runtime execution verification:
  const runtime = routing.createRuntime(route)
  const sessionId = 'ses_devops_road_1'

  // Round 1: DevOps first physical execution admission acquires target A
  const acq1 = await routing.acquireExecutionAdmission(
    runtime,
    sessionId,
    'msg_devops_1',
    'devops',
    'devops',
    null,
  )
  assert.equal(acq1.kind, 'Acquired')
  const targetA = routing.executionAdmissionTarget(runtime, acq1.lease)
  assert.ok(targetA && targetA.model, 'DevOps must acquire a valid model target')

  const commitOutcome = routing.commitExecutionAdmission(runtime, acq1.lease, {
    sessionId,
    physicalUserMessageId: 'msg_devops_1',
    role: 'devops',
    participant: 'devops',
    target: targetA,
  })
  assert.ok(['Applied', 'AlreadyApplied'].includes(commitOutcome.kind))

  // Simulate execution completed and release physical execution (first round ends)
  const rel = routing.releasePhysicalExecution(runtime, sessionId, 'msg_devops_1')
  assert.equal(rel.kind, 'Applied')

  // Round 2 (Resume / Fresh physical user message):
  // Even after physical release (when activePhysicalTarget was retired), the bound target is strictly locked to target A!
  const acq2 = await routing.acquireExecutionAdmission(
    runtime,
    sessionId,
    'msg_devops_2',
    'devops',
    'devops',
    null,
  )
  assert.equal(acq2.kind, 'Acquired')
  const target2 = routing.executionAdmissionTarget(runtime, acq2.lease)
  assert.deepEqual(target2, targetA, 'Fresh DevOps admission on resume must strictly inherit and lock target A')

  // Attempting to bind or commit a conflicting target must be rejected fail-closed
  const conflictTarget = { model: 'other/conflict-model', reasoning: 'none' }
  assert.throws(
    () => {
      routing.bindDevopsTarget(runtime, sessionId, conflictTarget)
    },
    (err) => {
      assert.match(String(err), /DevOps model binding is immutable/)
      return true
    },
    'Re-binding with a conflicting target must throw and fail closed',
  )

  assert.throws(
    () => {
      routing.commitExecutionAdmission(runtime, acq2.lease, {
        sessionId,
        physicalUserMessageId: 'msg_devops_2',
        role: 'devops',
        participant: 'devops',
        target: conflictTarget,
      })
    },
    (err) => {
      assert.match(String(err), /DevOps model binding is immutable/)
      return true
    },
    'Committing with a conflicting target must fail closed',
  )
})

test('WHAT[execution-model-routing-019] DevOps model automatically rotates when bound model quota is exhausted', async () => {
  const { default: route, markProviderFailed, clearFailedProviders } = await import(`${templateUrl.href}?test=${Date.now()}`)
  clearFailedProviders()
  const runtime = routing.createRuntime(route)
  const sessionId = 'ses_devops_quota_test'

  // 1. First execution acquires target from pool
  const acq1 = await routing.acquireExecutionAdmission(
    runtime,
    sessionId,
    'msg_q1',
    'devops',
    'devops',
    null,
  )
  assert.equal(acq1.kind, 'Acquired')
  const target1 = routing.executionAdmissionTarget(runtime, acq1.lease)
  routing.commitExecutionAdmission(runtime, acq1.lease, {
    sessionId,
    physicalUserMessageId: 'msg_q1',
    role: 'devops',
    participant: 'devops',
    target: target1,
  })
  routing.releasePhysicalExecution(runtime, sessionId, 'msg_q1')

  // 2. Mark target1's provider as failed (simulating quota exhausted)
  const provider1 = target1.model.slice(0, target1.model.indexOf('/'))
  markProviderFailed(provider1)

  // 3. Next execution should automatically acquire the next candidate from the pool rather than throwing
  const acq2 = await routing.acquireExecutionAdmission(
    runtime,
    sessionId,
    'msg_q2',
    'devops',
    'devops',
    null,
  )
  assert.equal(acq2.kind, 'Acquired')
  const target2 = routing.executionAdmissionTarget(runtime, acq2.lease)
  assert.notEqual(target2.model, target1.model, 'DevOps must rotate away from exhausted provider')

  // Committing target2 must succeed and update the bound target
  const commit2 = routing.commitExecutionAdmission(runtime, acq2.lease, {
    sessionId,
    physicalUserMessageId: 'msg_q2',
    role: 'devops',
    participant: 'devops',
    target: target2,
  })
  assert.ok(['Applied', 'AlreadyApplied'].includes(commit2.kind))
  assert.deepEqual(routing.boundDevopsTarget(runtime, sessionId), target2, 'Bound target must update to target2')
  clearFailedProviders()
})

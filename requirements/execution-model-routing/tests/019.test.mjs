import assert from 'node:assert/strict'
import test from 'node:test'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const templateUrl = new URL('../../../resources/wanxiangshu.mjs', import.meta.url)

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

test('WHAT[execution-model-routing-019] durable DevOps target seeding is fail-closed and idempotent', async () => {
  const { default: route } = await import(`${templateUrl.href}?test=${Date.now()}`)
  const runtime = routing.createRuntime(route)
  const fixedTarget = { model: 'provider/fixed-devops', reasoning: 'high' }

  // 1. A road's durable target seeds the binding, and the binding reads back exactly that target.
  // Red if seeding stops writing (returns null), writes a different target, or silently skips a well-formed road target.
  routing.seedDevOpsModelTarget(runtime, 'ses_seed_bound', 'provider/fixed-devops:high')
  assert.deepEqual(
    routing.boundDevopsTarget(runtime, 'ses_seed_bound'),
    fixedTarget,
    'a seeded road target must be readable as the fixed DevOps target',
  )

  // 2. Reseeding the identical target is an idempotent no-op.
  // Red if a repeat seed throws (duplicate rejection) or drifts the stored target.
  routing.seedDevOpsModelTarget(runtime, 'ses_seed_bound', 'provider/fixed-devops:high')
  assert.deepEqual(routing.boundDevopsTarget(runtime, 'ses_seed_bound'), fixedTarget, 'repeat seed of the same target must stay idempotent')

  // 3. A conflicting target is refused, and the refusal must not overwrite the fixed binding.
  // Red if the immutability guard is weakened so a later seed silently replaces the target.
  assert.throws(
    () => routing.seedDevOpsModelTarget(runtime, 'ses_seed_bound', 'provider/other-model:low'),
    /DevOps model binding is immutable/,
    'a conflicting seed must be refused',
  )
  assert.deepEqual(
    routing.boundDevopsTarget(runtime, 'ses_seed_bound'),
    fixedTarget,
    'a refused reseed must leave the fixed target untouched',
  )

  // 4. Malformed durable targets are refused, and refuse without seeding anything.
  // Red if the parser is loosened (default reasoning, non-qualified model, blank reasoning) or if a failed parse still writes a partial binding.
  assert.throws(() => routing.seedDevOpsModelTarget(runtime, 'ses_seed_malformed', ''), /durable DevOps model target/)
  assert.throws(() => routing.seedDevOpsModelTarget(runtime, 'ses_seed_malformed', 'provider/fixed-devops'), /durable DevOps model target/)
  assert.throws(() => routing.seedDevOpsModelTarget(runtime, 'ses_seed_malformed', 'noslashmodel:high'), /provider\/model/)
  assert.throws(() => routing.seedDevOpsModelTarget(runtime, 'ses_seed_malformed', 'provider/fixed-devops:'), /reasoning/)
  assert.equal(
    routing.boundDevopsTarget(runtime, 'ses_seed_malformed'),
    null,
    'a malformed target must not seed any binding',
  )

  // 5. A session with no seeded target has no binding.
  // Red if the query returns a default/guessed target, or if bindings are prewarmed for unknown sessions.
  assert.equal(
    routing.boundDevopsTarget(runtime, 'ses_never_seeded'),
    null,
    'a session without a seeded target must have no binding',
  )
})

test('WHAT[execution-model-routing-019] durable target seeding belongs only to the supplied runtime', () => {
  const first = routing.createRuntime(() => firstTarget)
  const second = routing.createRuntime(() => changedTarget)
  routing.seedDevOpsModelTarget(first, 'same-devops-session', 'provider/first:high')
  assert.deepEqual(routing.boundDevopsTarget(first, 'same-devops-session'), firstTarget)
  assert.equal(routing.boundDevopsTarget(second, 'same-devops-session'), null)
  routing.seedDevOpsModelTarget(second, 'same-devops-session', 'other/changed:none')
  assert.deepEqual(routing.boundDevopsTarget(second, 'same-devops-session'), changedTarget)
  assert.deepEqual(routing.boundDevopsTarget(first, 'same-devops-session'), firstTarget)
})

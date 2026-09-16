// CONTEXT-COMPRESSION-025: a StopPhysicalRun decision must take effect before
// the next provider admission for that exact execution.
//
// The barrier under test is the production hook path: BlogSurface calls the
// same EnforcerContinuation.applyPhysicalStop the real transform apply stage
// calls at step 11 of the capability chain. Admission evidence comes from the
// process-shared ModelRouting runtime — the same runtime
// SessionExecutionBinding.enterBoundProviderStep consults. The only stub is
// the Host termination capability itself, controllable by the test.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as blog from '../../../dist/Enforcer/BlogSurface.js'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

let counter = 0

const bindExecution = async (role) => {
  counter += 1
  const ids = {
    session: `ses-stop-${counter}`,
    physical: `msg-stop-${counter}`,
  }
  const acquired = await routing.acquireSharedExecutionAdmission(
    ids.session,
    ids.physical,
    role,
    role,
    null,
  )
  assert.equal(acquired.kind, 'Acquired')
  const target = routing.sharedExecutionAdmissionTarget(acquired.lease)
  const committed = routing.commitSharedExecutionAdmission(acquired.lease, {
    sessionId: ids.session,
    physicalUserMessageId: ids.physical,
    role,
    participant: role,
    target,
  })
  assert.deepEqual(committed, { kind: 'Applied' })
  return ids
}

test('WHAT[CONTEXT-COMPRESSION-025] stop decision fences provider admission before the abort resolves', async (t) => {
  await routing.initialize()
  const ids = await bindExecution('blogger')
  const other = await bindExecution('engineer')

  // Positive control: admission is live for the exact execution before stop.
  await routing.sharedEnterProviderStep(ids.session, ids.physical, [])

  let releaseAbort
  const terminate = () => new Promise((resolve) => { releaseAbort = resolve })

  blog.applyPhysicalStop(terminate, ids.session, ids.physical, 'blogger-protocol-repair-exhausted')
  await new Promise((resolve) => setImmediate(resolve))

  // While the physical abort is still unresolved, the next provider admission
  // for this exact execution is already fenced out.
  await assert.rejects(
    routing.sharedEnterProviderStep(ids.session, ids.physical, []),
    /no active execution binding/,
  )

  // The barrier is execution-exact: a different execution admits normally.
  await routing.sharedEnterProviderStep(other.session, other.physical, [])

  releaseAbort({ ok: true })
  await new Promise((resolve) => setImmediate(resolve))

  // A resolved abort never reopens the stopped execution.
  await assert.rejects(
    routing.sharedEnterProviderStep(ids.session, ids.physical, []),
    /no active execution binding/,
  )
})

test('WHAT[CONTEXT-COMPRESSION-025] abort rejection never reopens the stopped execution', async () => {
  await routing.initialize()
  const ids = await bindExecution('blogger')

  const terminate = () => Promise.resolve({ ok: false, error: 'host refused abort' })

  blog.applyPhysicalStop(terminate, ids.session, ids.physical, 'blogger-protocol-repair-exhausted')
  await assert.rejects(
    routing.sharedEnterProviderStep(ids.session, ids.physical, []),
    /no active execution binding/,
  )

  await new Promise((resolve) => setImmediate(resolve))
  await assert.rejects(
    routing.sharedEnterProviderStep(ids.session, ids.physical, []),
    /no active execution binding/,
  )
})

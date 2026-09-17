import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

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
}

{
const { default: test } = await import("node:test");
const { assertFatalBoundary } = await import("../../structured-workflow/tests/support/m6-boundary-proof.mjs");


test('WHAT[CONTEXT-COMPRESSION-025] Blogger fatal binds exact request settlement and one injected fuse', () => assertFatalBoundary('context-compression'))
}

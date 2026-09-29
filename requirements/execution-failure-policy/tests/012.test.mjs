import assert from 'node:assert/strict'
import test from 'node:test'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const admitExecution = async (session, physical, role) => {
  const acquired = await routing.acquireSharedExecutionAdmission(session, physical, role, role, null)
  assert.equal(acquired.kind, 'Acquired')
  assert.deepEqual(routing.commitSharedExecutionAdmission(acquired.lease, {
    sessionId: session, physicalUserMessageId: physical, role, participant: role,
    target: routing.sharedExecutionAdmissionTarget(acquired.lease),
  }), { kind: 'Applied' })
}

test('WHAT[execution-failure-policy-012] real admission is barred before abort outcome and stays barred after rejection or exception', async () => {
  await routing.initialize()
  for (const outcome of ['success', 'refusal', 'exception']) {
    const target = { session: `stop-${outcome}`, physical: `message-${outcome}` }
    const other = { session: `other-${outcome}`, physical: `other-message-${outcome}` }
    await admitExecution(target.session, target.physical, 'blogger')
    await admitExecution(other.session, other.physical, 'engineer')
    await routing.sharedEnterProviderStep(target.session, target.physical, [])
    let completeAbort
    let rejectAbort
    const pending = new Promise((resolve, reject) => { completeAbort = resolve; rejectAbort = reject })
    const calls = []
    blog.applyPhysicalStop((session, reason) => { calls.push({ session, reason }); return pending }, target.session, target.physical, 'request-protocol-stop')
    assert.deepEqual(calls, [{ session: target.session, reason: 'request-protocol-stop' }])
    await assert.rejects(routing.sharedEnterProviderStep(target.session, target.physical, []), /no active execution binding/)
    await routing.sharedEnterProviderStep(other.session, other.physical, [])
    if (outcome === 'exception') rejectAbort(new Error('physical abort failed'))
    else completeAbort(outcome === 'success' ? { ok: true } : { ok: false, error: 'abort refused' })
    await new Promise((resolve) => setImmediate(resolve))
    await assert.rejects(routing.sharedEnterProviderStep(target.session, target.physical, []), /no active execution binding/)
    assert.deepEqual(routing.releasePhysical(other.session, other.physical), { kind: 'Applied' })
  }
})

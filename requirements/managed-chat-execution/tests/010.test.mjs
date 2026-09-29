import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as recoveryHost from '../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'
import * as status from '../../../dist/Execution/Session/ChatExecution/StatusSurface.js'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const withHost = async (action) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-exact-cancel-'))
  const host = await recoveryHost.bootRecoveryHost(directory, 'absent')
  try {
    await action(host)
  } finally {
    recoveryHost.disposeRecoveryHost(host)
    rmSync(directory, { recursive: true, force: true })
  }
}

test('WHAT[managed-chat-execution-010] actual recovery Host cancellation settles one exact execution and preserves its neighbours', async () => {
  await withHost(async (host) => {
    await recoveryHost.seedAccepted(host, 'ses-cancel', 'msg-before-provider')
    await recoveryHost.seedProviderStarted(host, 'ses-cancel', 'msg-started', 'provider-started')
    await recoveryHost.seedAccepted(host, 'ses-other', 'msg-before-provider')

    const before = recoveryHost.executionStatus(host, 'ses-cancel', 'msg-started')
    const other = recoveryHost.executionStatus(host, 'ses-other', 'msg-before-provider')
    assert.equal(before.phase, 'ProviderStarted')
    assert.equal(other.phase, 'Accepted')
    const cancelled = await recoveryHost.signalCancelled(host, 'ses-cancel', 'msg-before-provider')
    assert.equal(cancelled.lifecycle, 'Terminal')
    assert.equal(cancelled.disposition, 'Cancelled')
    assert.deepEqual(recoveryHost.executionStatus(host, 'ses-cancel', 'msg-started'), before)
    assert.deepEqual(recoveryHost.executionStatus(host, 'ses-other', 'msg-before-provider'), other)

    const replayed = await recoveryHost.signalCancelled(host, 'ses-cancel', 'msg-before-provider')
    assert.deepEqual(replayed, cancelled)
    const afterStart = await recoveryHost.signalCancelled(host, 'ses-cancel', 'msg-started')
    assert.equal(afterStart.lifecycle, 'Terminal')
    assert.equal(afterStart.disposition, 'Cancelled')
    assert.deepEqual(recoveryHost.executionStatus(host, 'ses-other', 'msg-before-provider'), other)
  })
})

test.todo('WHAT[managed-chat-execution-010] public logical cancel and session delete enumerate every key and await durable terminal plus real capacity release, including held append (GAP-126)')

test('WHAT[managed-chat-execution-010] actual Host session deletion settles every admitted key and preserves another session', async () => {
  let deliverEvent
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const admit = (sessionId, physical) => hooks['chat.message']({ sessionID: sessionId, messageID: physical, agent: 'engineer' }, {
      message: { id: physical, sessionID: sessionId, role: 'user', agent: 'engineer' }, parts: [],
    })
    await admit('ses-deleted', 'msg-deleted-first')
    await admit('ses-deleted', 'msg-deleted-next')
    await admit('ses-preserved', 'msg-preserved')
    const preserved = status.query(runtime.journal, 'ses-preserved', 'msg-preserved')
    assert.deepEqual(preserved, { accepted: true, providerStarted: false, terminal: false, disposition: null })

    deliverEvent({ type: 'session.deleted', properties: { sessionID: 'ses-deleted' } })
    await hooks.dispose()

    for (const physical of ['msg-deleted-first', 'msg-deleted-next']) {
      assert.deepEqual(status.query(runtime.journal, 'ses-deleted', physical), {
        accepted: true, providerStarted: false, terminal: true, disposition: 'Cancelled',
      })
    }
    assert.deepEqual(status.query(runtime.journal, 'ses-preserved', 'msg-preserved'), preserved)
    const capacity = routing.sharedCapacitySnapshot()
    assert.equal(capacity.executions.some((execution) => execution.sessionId === 'ses-deleted'), false)
    assert.equal(capacity.tokens.some((token) => token.owner.sessionId === 'ses-deleted'), false)
    assert.equal(capacity.waiters.some((waiter) => waiter.sessionId === 'ses-deleted'), false)
  }, { events: { listen: (callback) => { deliverEvent = callback; return () => {} } } })
})

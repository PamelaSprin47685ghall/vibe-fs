import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as recoveryHost from '../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'

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

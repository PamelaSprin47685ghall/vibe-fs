import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as recoveryHost from '../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'

test('WHAT[provider-attempt-recovery-023] session idle sweep selectively targets only Accepted without ProviderStarted executions of that session', async (t) => {
  const tempDir = mkdtempSync(join(tmpdir(), 'wxs-par023-'))
  t.after(() => {
    rmSync(tempDir, { recursive: true, force: true })
  })

  // 1. Boot recovery host with accept port outcome
  const handle = await recoveryHost.bootRecoveryHost(tempDir, 'accept')
  t.after(() => {
    recoveryHost.disposeRecoveryHost(handle)
  })

  const sessionIdA = 'ses-par023-a'
  const sessionIdB = 'ses-par023-b'

  // Execution 1: ses-a, Accepted, NO ProviderStarted (target obligation shape)
  await recoveryHost.seedAccepted(handle, sessionIdA, 'msg-accepted-1')

  // Execution 2: ses-a, Accepted + ProviderStarted (already reached provider, not target)
  await recoveryHost.seedProviderStarted(handle, sessionIdA, 'msg-started-2', 'run-started-2')

  // Execution 3: ses-b, Accepted, NO ProviderStarted (different session)
  await recoveryHost.seedAccepted(handle, sessionIdB, 'msg-accepted-3')

  // Trigger idle sweep for sessionIdA
  const sweepOutcomeA = await recoveryHost.signalSessionQuiesced(handle, sessionIdA)

  // Assert: exactly 1 call occurred (execution 1 was swept; execution 2 was ignored because it has started; execution 3 was ignored because of different session)
  assert.equal(sweepOutcomeA.calls, 1, 'idle sweep must resume exactly Accepted without ProviderStarted executions for that session')
  assert.equal(sweepOutcomeA.manuals.length, 0, 'accepted recovery port must resolve without manual intervention')

  // 2. Boot second recovery host with absent port outcome
  const tempDirAbsent = mkdtempSync(join(tmpdir(), 'wxs-par023-absent-'))
  t.after(() => {
    rmSync(tempDirAbsent, { recursive: true, force: true })
  })
  const handleAbsent = await recoveryHost.bootRecoveryHost(tempDirAbsent, 'absent')
  t.after(() => {
    recoveryHost.disposeRecoveryHost(handleAbsent)
  })

  // Seed Accepted without ProviderStarted
  await recoveryHost.seedAccepted(handleAbsent, sessionIdA, 'msg-accepted-absent')

  // When port is absent, sweep cannot resume -> decisively resolves to manual intervention without hanging
  const sweepOutcomeAbsent = await recoveryHost.signalSessionQuiesced(handleAbsent, sessionIdA)
  assert.equal(sweepOutcomeAbsent.calls, 0, 'absent port must make 0 resume calls')
  assert.equal(sweepOutcomeAbsent.manuals.length, 1, 'unresumed accepted execution must report manual intervention')
  assert.equal(sweepOutcomeAbsent.manuals[0].sessionId, sessionIdA)
  assert.equal(sweepOutcomeAbsent.manuals[0].physicalUserMessageId, 'msg-accepted-absent')
})

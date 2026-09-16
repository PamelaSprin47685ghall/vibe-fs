import assert from 'node:assert/strict'
import fs from 'node:fs'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as diag from '../../../dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js'
import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as recoveryHost from '../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'

const acceptedFact = fs.readFileSync(new URL('./fixtures/chat-execution-v1.json', import.meta.url), 'utf8')

const withRecoveryHost = async (label, portOutcome, action) => {
  const directory = mkdtempSync(join(tmpdir(), `wxs-chat-recovery-${label}-`))
  const host = await recoveryHost.bootRecoveryHost(directory, portOutcome)

  try {
    await action(host)
  } finally {
    recoveryHost.disposeRecoveryHost(host)
    rmSync(directory, { recursive: true, force: true })
  }
}

const sessionOf = (suffix) => `ses-recovery-${suffix}`
const physicalOf = (suffix) => `msg-recovery-${suffix}`

test('WHAT[CHATEXEC-013] diagnostic query derives nonterminal and physical-attempt counts from canonical projection', () => {
  const projected = chatExecution.fold([acceptedFact])
  assert.equal(projected.ok, true)

  const result = diag.queryReliability(
    diag.createCounters(),
    projected.value,
    { waiters: [], activeCount: 0, counters: { duplicate: 0, stale: 0, conflict: 0 } },
    { resumes: [], requeues: [], manualInterventions: [] },
  )

  assert.deepEqual(result.execution, {
    acceptedWithoutTerminal: 1,
    providerStartedWithoutTerminal: 0,
    physicalAttemptsByLogicalRun: [{ logicalRunId: 'run-chat-fixture', physicalAttempts: 1 }],
  })
  assert.equal(Object.isFrozen(result.execution), true)
})

test('WHAT[CHATEXEC-013] exact terminal settlement revokes the manual', async () => {
  await withRecoveryHost('terminal', 'absent', async (host) => {
    const sessionId = sessionOf('terminal')
    const physicalId = physicalOf('terminal')
    const providerRun = 'provider-recovery-terminal'

    await recoveryHost.seedProviderStarted(host, sessionId, physicalId, providerRun)

    const before = await recoveryHost.resumeAccepted(host, sessionId, physicalId)
    assert.equal(before.manuals.length, 1)

    const settled = await recoveryHost.finalizeCompleted(host, sessionId, physicalId, providerRun)

    assert.deepEqual(settled.manuals, [])
    assert.equal(settled.lifecycle, 'Terminal')
    assert.equal(settled.disposition, 'Completed')
    assert.equal(settled.sessionId, sessionId)
    assert.equal(settled.physicalUserMessageId, physicalId)
  })
})

test('WHAT[CHATEXEC-013] pre-provider cancellation settlement revokes the manual', async () => {
  await withRecoveryHost('accepted-cancel', 'absent', async (host) => {
    const sessionId = sessionOf('accepted-cancel')
    const physicalId = physicalOf('accepted-cancel')

    await recoveryHost.seedAccepted(host, sessionId, physicalId)

    const before = await recoveryHost.resumeAccepted(host, sessionId, physicalId)
    assert.equal(before.manuals.length, 1)

    const settled = await recoveryHost.signalCancelled(host, sessionId, physicalId)

    assert.deepEqual(settled.manuals, [])
    assert.equal(settled.lifecycle, 'Terminal')
    assert.equal(settled.disposition, 'Cancelled')
    assert.equal(settled.sessionId, sessionId)
    assert.equal(settled.physicalUserMessageId, physicalId)
  })
})

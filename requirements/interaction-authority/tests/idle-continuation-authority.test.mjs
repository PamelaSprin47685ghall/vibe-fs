// HOST-004 / PROMPT-003: idle-derived Manager continuation uses the current
// AuthorityRoot and one quiescence permit admits at most one durable send.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentFact,
  agentJournal,
  authorityRoot,
  caseOf,
  logicalRunId,
  managerLifeId,
  promptDispatcher,
  providerRun,
  sessionId,
  stream,
  transportReceipt,
} from '../../verification-system/tests/support/domain.mjs'

import * as HostSessionNudge from '../../../dist/Interaction/Dispatch/OpenCode/SessionNudge.js'

const quiescenceModule = await import('../../../dist/OpenCode/Host/SessionQuiescenceGate.js')
const { SessionQuiescenceGate } = quiescenceModule
const beginAttempt = Object.entries(quiescenceModule).find(([k]) => k.startsWith('SessionQuiescenceGate__BeginProviderAttempt_'))?.[1]
const observeIdle = Object.entries(quiescenceModule).find(([k]) => k.startsWith('SessionQuiescenceGate__ObserveIdle_'))?.[1]

const seedRoot = async (journal, sid) => {
  const result = await agentJournal.appendAgent(
    stream.session(sid),
    undefined,
    agentFact('AuthorityRootAccepted', {
      SessionId: sid,
      LogicalRunId: logicalRunId('logical-root'),
      AuthorityRootUserMessageId: authorityRoot('user-root'),
      AuthorityKind: 'HumanRoot',
      SelectedAgent: 'fast-manager',
      PeerAgent: 'deep-manager',
      CanonicalRole: 'manager',
      SelectedTier: 'fast',
    }),
    journal,
  )
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
}

const capturingPort = (sends) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    sends.push({ session, text, options })
    return promptDispatcher.admittedWithReceipt(transportReceipt(`receipt-${sends.length}`))
  },
  AbortSession: async () => ({ tag: 0, fields: [] }),
  AbortChildren: async () => {},
  CreateChildSession: async () => ({ tag: 1, fields: ['unused'] }),
  ListChildren: async () => ({ tag: 0, fields: [[]] }),
})

test('WHAT[INTERACTION-AUTHORITY-012] HOST_004_idle_manager_continuation_consumes_one_permit_and_claims_once', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'idle-authority-'))
  const created = await agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))

  try {
    const sid = sessionId('ses-idle-authority')
    await seedRoot(created.journal, sid)

    const gate = new SessionQuiescenceGate()
    beginAttempt(gate, sid)
    const permit = observeIdle(gate, sid)
    const sends = []
    const port = capturingPort(sends)

    const first = await HostSessionNudge.trySendIdleManagerEncouragement(
      gate,
      permit,
      port,
      sid,
      '# You can continue.\n',
      undefined,
      created.journal,
      managerLifeId('life-idle'),
      providerRun('provider-idle'),
    )
    assert.equal(caseOf(first), 'Sent')
    assert.equal(sends.length, 1, 'fresh idle occasion sends exactly once')

    const second = await HostSessionNudge.trySendIdleManagerEncouragement(
      gate,
      permit,
      port,
      sid,
      '# You can continue.\n',
      undefined,
      created.journal,
      managerLifeId('life-idle'),
      providerRun('provider-idle'),
    )
    assert.equal(caseOf(second), 'Superseded')
    assert.equal(sends.length, 1, 'consumed permit never performs a second transport send')
  } finally {
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
})

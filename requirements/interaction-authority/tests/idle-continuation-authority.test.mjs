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
import { managerIdleOccasionKey } from './support/manager-idle.mjs'

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

test('WHAT[INTERACTION-AUTHORITY-012] HOST_004_idle_manager_continuation_is_idempotent_per_terminal_and_unbounded_across_terminals', async () => {
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
      'pre-t1',
      providerRun('run-idle-1'),
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
      'pre-t1',
      providerRun('run-idle-1'),
    )
    assert.equal(caseOf(second), 'Superseded')
    assert.equal(sends.length, 1, 'consumed permit never performs a second transport send')

    beginAttempt(gate, sid)
    const newTerminalPermit = observeIdle(gate, sid)
    const samePhase = await HostSessionNudge.trySendIdleManagerEncouragement(
      gate,
      newTerminalPermit,
      port,
      sid,
      '# You can continue.\n',
      undefined,
      created.journal,
      managerLifeId('life-idle'),
      'pre-t1',
      providerRun('run-idle-2'),
    )
    assert.equal(sends.length, 2, 'a fresh ProviderRun/idle under the same plan-commitment condition must earn another encouragement')

    beginAttempt(gate, sid)
    const nextPhasePermit = observeIdle(gate, sid)
    const postT1 = await HostSessionNudge.trySendIdleManagerEncouragement(
      gate,
      nextPhasePermit,
      port,
      sid,
      '# You can continue after T1.\n',
      undefined,
      created.journal,
      managerLifeId('life-idle'),
      'post-t1',
      providerRun('run-idle-3'),
    )
    assert.equal(sends.length, 3, 'a later terminal after a business phase change also earns encouragement')
  } finally {
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[INTERACTION-AUTHORITY-012] HOST_004_manager_idle_process_dedupe_is_per_terminal_not_per_life_condition', () => {
  const first = managerIdleOccasionKey(
    'ses-manager-idle-process',
    'life-manager-idle-process',
    'pre-t1',
    'run-manager-idle-1',
  )
  const sameTerminalReplay = managerIdleOccasionKey(
    'ses-manager-idle-process',
    'life-manager-idle-process',
    'pre-t1',
    'run-manager-idle-1',
  )
  const freshTerminalSameCondition = managerIdleOccasionKey(
    'ses-manager-idle-process',
    'life-manager-idle-process',
    'pre-t1',
    'run-manager-idle-2',
  )

  assert.equal(first, sameTerminalReplay, 'same terminal replay must keep the same process dedupe key')
  assert.notEqual(
    first,
    freshTerminalSameCondition,
    'a fresh terminal in the same Life/pre-T1 condition must have a fresh process dedupe key',
  )
})

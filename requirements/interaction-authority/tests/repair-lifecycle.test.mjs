import assert from 'node:assert/strict'
import test from 'node:test'
import * as turns from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'
import * as auth from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const text = (value) => [{ type: 'text', text: value }]

test('WHAT[INTERACTION-AUTHORITY-019] repair claim does not turn an in-flight repair into exhaustion', () => {
  assert.equal(turns.repairDefectDecision(false, false, null, []), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, false, null, []), 'AwaitRepairTerminal')
  assert.equal(turns.repairDefectDecision(true, false, 'tool-calls', []), 'AwaitRepairTerminal')
})

test('WHAT[INTERACTION-AUTHORITY-019] fresh invalid repair terminals re-open the gate reminder', () => {
  assert.equal(turns.repairDefectDecision(true, true, 'stop', []), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, true, 'length', text('partial')), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, true, 'stop', text('done')), 'NoRepair')
})

test('WHAT[INTERACTION-AUTHORITY-019] a currently-repairing attempt never completes as exhausted or failed repair', () => {
  // Translate the raw DU cases through the registered completed-turn surface:
  // currentAttemptIsRepair + unfinished → AwaitRepairTerminal, terminal+stop+empty
  // → NoRepair, terminal+length → RequestRepair. A repair never lands in an
  // exhausted state.
  assert.equal(turns.repairDefectDecision(true, false, null, []), 'AwaitRepairTerminal')
  assert.equal(turns.repairDefectDecision(true, true, 'stop', text('ok')), 'NoRepair')
  assert.equal(turns.repairDefectDecision(true, true, 'length', []), 'RequestRepair')
})

test('WHAT[INTERACTION-AUTHORITY-019] duplicate gate-nudge claim is AlreadyAdmitted — single physical send', async () => {
  // Claim admission is the durable fact: a first claim opens the occasion,
  // a second concurrent attempt on the same digest returns AlreadyAdmitted at
  // the durable-authority level, so the port records exactly one send.
  const base = mkdtempSync(join(tmpdir(), 'wxs-ia-019-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(
      base, 'writer-019', 'rt-019', 4244, '2026-01-01T00:00:00Z',
    )
    assert.equal(opened.ok, true)
    try {
      const owner = await dispatch.acceptHumanRootSelection(
        opened.journal, 'ses_019_owner', 'msg-019-owner',
        {
          kind: 'RootSelection',
          ownerSession: null, ownerLogicalRun: null, ownerAuthorityRoot: null,
          participantIdentity: { participant: 'manager', role: 'manager', selectedTier: 'deep', persona: 'Lead', personaCatalogVersion: 1, origin: 'ResolvedAtRoot' },
        },
      )
      assert.equal(owner.ok, true, owner.ok ? '' : owner.error)
      const captured = []
      const results = await dispatch.sendGateNudgesConcurrently(
        { SubscribeTerminal: () => ({ Dispose: () => {} }), SendPrompt: async (s, p, o) => { captured.push(p); return dispatch.admittedWithReceipt('r-019') } },
        opened.journal,
        'ses_019',
        'repair nudge',
        'BusyAgentNudge',
        'interaction-repair',
        'run-019',
        owner.profile,
      )
      assert.equal(results.length, 2)
      assert.equal(results[0].ok, true, JSON.stringify(results[0]))
      assert.equal(results[1].ok, true, 'concurrent duplicate joins, never fails')
      assert.equal(captured.length, 1, 'one physical send — AlreadyAdmitted dedup')
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

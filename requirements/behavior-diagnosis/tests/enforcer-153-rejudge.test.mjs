// ENFORCER-153: Blogger recovery facts from semantic claim + transcript —
// proved through registered surfaces only (no deep internal imports).
import test from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as turns from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const text = (value) => [{ type: 'text', text: value }]

const capturingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, payload, options) => {
    captured.push({ session, text: payload, options })
    return dispatch.admittedWithReceipt('receipt-153')
  },
})

const managerOwner = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    participant: 'manager',
    role: 'manager',
    selectedTier: 'deep',
    persona: 'Lead',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

 test('WHAT[BD-017] repeated invalid turns re-open repair and stable terminals complete it', () => {
  // decideRepairDefect is exercised only through the registered
  // CompletedTurnSurface name mapping: in-flight/currentRepair attempts await
  // terminal, fresh invalid terminals re-request, repairs never exhaust.
  assert.equal(turns.repairDefectDecision(false, false, null, []), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, false, null, []), 'AwaitRepairTerminal')
  assert.equal(turns.repairDefectDecision(true, false, 'tool-calls', []), 'AwaitRepairTerminal')
  assert.equal(turns.repairDefectDecision(true, true, 'length', []), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, true, 'stop', text('done')), 'NoRepair')
})

test('WHAT[BD-017] concurrent gate nudge deduplicates at the dispatch boundary', async () => {
  // Two nudges on the same terminal occasion must collapse to exactly one
  // physical send; a second admission observes the first result. This is the
  // AlreadyAdmitted-not-Failed contract proven at the physical boundary.
  const base = mkdtempSync(join(tmpdir(), 'wxs-enf153-nudge-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(
      base,
      'writer-153',
      'rt-153',
      4242,
      '2026-01-01T00:00:00Z',
    )
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const owner = await dispatch.acceptHumanRootSelection(
        opened.journal,
        'ses_153_owner',
        'msg-153-owner',
        managerOwner,
      )
      assert.equal(owner.ok, true, owner.ok ? '' : owner.error)

      const captured = []
      const results = await dispatch.sendGateNudgesConcurrently(
        capturingPort(captured),
        opened.journal,
        'ses_153',
        'nudge text',
        'BusyAgentNudge',
        'interaction-repair',
        'run-153',
        owner.profile,
      )
      assert.equal(results.length, 2)
      assert.equal(results[0].ok, true, JSON.stringify(results[0]))
      assert.equal(results[1].ok, true, 'second nudge on same occasion joins, never fails')
      assert.equal(captured.length, 1, `a deduplicated nudge sends once, got ${captured.length}`)
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('WHAT[BD-017] authority gate-nudge admission is required before any physical send', async () => {
  // Without an agent-owner profile the surface's profileOf resolves an error:
  // the nudge is refused before any physical SendPrompt reaches the port.
  const base = mkdtempSync(join(tmpdir(), 'wxs-enf153-gate-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(
      base,
      'writer-153g',
      'rt-153g',
      4243,
      '2026-01-01T00:00:00Z',
    )
    assert.equal(opened.ok, true)
    try {
      const captured = []
      const results = await dispatch.sendGateNudgesConcurrently(
        capturingPort(captured),
        opened.journal,
        'ses_noroot_153',
        'nudge',
        'BusyAgentNudge',
        'interaction-repair',
        'run-153',
        { authorityKind: 'AgentOwnerRoot', identitySeed: { kind: 'NoSuchSeed' } },
      )
      assert.equal(results.every((r) => !r.ok), true)
      assert.ok(results.every((r) => /identity seed|seed kind/i.test(r.error ?? '')), JSON.stringify(results))
      assert.equal(captured.length, 0, 'no profile → no physical send')
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

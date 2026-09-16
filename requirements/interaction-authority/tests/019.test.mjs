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

const hash = (value) => `H(${value})`
const personas = {
  coder: 'Coder',
  manager: 'Lead',
}
const rootSelection = (agent) => {
  const role = agent === 'predictor' ? 'inspector' : agent
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      participant: agent,
      role,
      selectedTier: 'deep',
      persona: personas[agent] ?? 'Unknown',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  }
}
const inheritedSeed = (agent, physical) => {
  const owner = auth.createAuthorityRoot(
    hash,
    'rt_owner',
    'ses_owner',
    'HumanRoot',
    `owner_${physical}`,
    rootSelection('manager'),
  )
  assert.equal(owner.ok, true, owner.error)
  const inherited = auth.issueInheritedIdentitySeed(agent, owner.value)
  assert.equal(inherited.ok, true, inherited.error)
  return inherited.value
}
const createdRoot = auth.createAuthorityRoot(
  hash,
  'rt_join',
  'ses_jg',
  'AgentOwnerRoot',
  'root-jg',
  inheritedSeed('coder', 'root-jg'),
)
assert.equal(createdRoot.ok, true, createdRoot.error)
const root = createdRoot.value

test('WHAT[INTERACTION-AUTHORITY-019] gate_nudge_is_exact_terminal_idempotent_and_unbounded_across_fresh_terminals', () => {
  let state = auth.registerAuthority(root, auth.empty)
  const digest1 = auth.gateNudgePayloadDigest('missing-final-report', 'run-1')
  assert.equal(auth.gateNudgeAlreadyAdmitted('ses_jg', root.logicalRun, 'InteractionRepair', 'missing-final-report', 'run-1', state), false)
  state = auth.registerClaim(
    auth.claimContinuation('pk-repair', 'ses_jg', 'InteractionRepair', root, digest1),
    state,
  )
  assert.equal(auth.gateNudgeAlreadyAdmitted('ses_jg', root.logicalRun, 'InteractionRepair', 'missing-final-report', 'run-1', state), true)
  assert.equal(auth.gateNudgeAlreadyAdmitted('ses_jg', root.logicalRun, 'InteractionRepair', 'missing-final-report', 'run-2', state), false)

  state = auth.abandonClaim('pk-repair', state)
  assert.equal(
    auth.gateNudgeAlreadyAdmitted('ses_jg', root.logicalRun, 'InteractionRepair', 'missing-final-report', 'run-1', state),
    false,
    'a definitely-not-sent abandoned claim must not spend the gate reminder occasion',
  )

  state = auth.registerClaim(
    auth.claimContinuation('pk-repair-retry', 'ses_jg', 'InteractionRepair', root, digest1),
    state,
  )
  state = auth.acceptClaim('pk-repair-retry', 'msg-repair-retry', state)
  assert.equal(
    auth.gateNudgeAlreadyAdmitted('ses_jg', root.logicalRun, 'InteractionRepair', 'missing-final-report', 'run-1', state),
    true,
    'physical acceptance permanently admits the exact terminal occasion',
  )
})

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
  assert.equal(turns.repairDefectDecision(true, false, null, []), 'AwaitRepairTerminal')
  assert.equal(turns.repairDefectDecision(true, true, 'stop', text('ok')), 'NoRepair')
  assert.equal(turns.repairDefectDecision(true, true, 'length', []), 'RequestRepair')
})

test('WHAT[INTERACTION-AUTHORITY-019] duplicate gate-nudge claim is AlreadyAdmitted — single physical send', async () => {
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

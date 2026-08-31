// INTERACTION-AUTHORITY proof — JoinGuard admission and bounded repair family.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const hash = (value) => `H(${value})`
const personas = {
  'fast-coder': 'Coder',
  'fast-manager': 'Coordinator',
}
const rootSelection = (agent) => {
  const [selectedTier, canonicalRole] = agent.split('-')
  const peerTier = selectedTier === 'fast' ? 'deep' : 'fast'
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: agent,
      peerAgent: `${peerTier}-${canonicalRole}`,
      canonicalRole,
      selectedTier,
      persona: personas[agent] ?? 'Unknown',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  }
}
const inheritedSeed = (agent, physical) => {
  const owner = authority.createAuthorityRoot(
    hash,
    'rt_owner',
    'ses_owner',
    'HumanRoot',
    `owner_${physical}`,
    rootSelection('fast-manager'),
  )
  assert.equal(owner.ok, true, owner.error)
  const inherited = authority.issueInheritedIdentitySeed(agent, owner.value)
  assert.equal(inherited.ok, true, inherited.error)
  return inherited.value
}
const createdRoot = authority.createAuthorityRoot(
  hash,
  'rt_join',
  'ses_jg',
  'AgentOwnerRoot',
  'root-jg',
  inheritedSeed('fast-coder', 'root-jg'),
)
assert.equal(createdRoot.ok, true, createdRoot.error)
const root = createdRoot.value

test('WHAT[INTERACTION-AUTHORITY-019] gate_nudge_is_exact_terminal_idempotent_and_unbounded_across_fresh_terminals', () => {
  let state = authority.registerAuthority(root, authority.empty)
  const digest1 = authority.gateNudgePayloadDigest('missing-final-report', 'run-1')
  assert.equal(authority.gateNudgeAlreadyAdmitted('ses_jg', root.logicalRun, 'InteractionRepair', 'missing-final-report', 'run-1', state), false)
  state = authority.registerClaim(
    authority.claimContinuation('pk-repair', 'ses_jg', 'InteractionRepair', root, 'fast-coder', digest1),
    state,
  )
  assert.equal(authority.gateNudgeAlreadyAdmitted('ses_jg', root.logicalRun, 'InteractionRepair', 'missing-final-report', 'run-1', state), true)
  assert.equal(authority.gateNudgeAlreadyAdmitted('ses_jg', root.logicalRun, 'InteractionRepair', 'missing-final-report', 'run-2', state), false)

  state = authority.abandonClaim('pk-repair', state)
  assert.equal(
    authority.gateNudgeAlreadyAdmitted('ses_jg', root.logicalRun, 'InteractionRepair', 'missing-final-report', 'run-1', state),
    false,
    'a definitely-not-sent abandoned claim must not spend the gate reminder occasion',
  )

  state = authority.registerClaim(
    authority.claimContinuation('pk-repair-retry', 'ses_jg', 'InteractionRepair', root, 'fast-coder', digest1),
    state,
  )
  state = authority.acceptClaim('pk-repair-retry', 'msg-repair-retry', state)
  assert.equal(
    authority.gateNudgeAlreadyAdmitted('ses_jg', root.logicalRun, 'InteractionRepair', 'missing-final-report', 'run-1', state),
    true,
    'physical acceptance permanently admits the exact terminal occasion',
  )
})

test('WHAT[INTERACTION-AUTHORITY-014] JNGD_nudge_contract_fails_closed_without_durable_authority', () => {
  const source = readFileSync(join(process.cwd(), 'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinGuard.fs'), 'utf8')
  const nudge = readFileSync(join(process.cwd(), 'src/Wanxiangshu/Interaction/Dispatch/OpenCode/SessionNudge.fs'), 'utf8')
  assert.match(source, /Join guard nudge requires an AgentJournal/)
  assert.match(nudge, /No active authority profile/)
  assert.match(source, /ContinuationKind\.JoinGuard/)
  assert.match(source, /AlreadyOutstanding/)
})

test('WHAT[INTERACTION-AUTHORITY-010] duplicate_idle_continuation_admission_is_not_terminal_failure', () => {
  const nudge = readFileSync(join(process.cwd(), 'src/Wanxiangshu/Interaction/Dispatch/OpenCode/SessionNudge.fs'), 'utf8')
  const manager = readFileSync(join(process.cwd(), 'src/Wanxiangshu/Mission/Manager/Idle.fs'), 'utf8')
  assert.match(nudge, /IdleContinuationOutcome\.AlreadyAdmitted/)
  assert.doesNotMatch(nudge, /Manager idle encouragement already claimed for this terminal/)
  assert.match(manager, /IdleContinuationOutcome\.AlreadyAdmitted/)
})

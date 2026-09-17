import assert from 'node:assert/strict'
import test from 'node:test'
import * as intent from '../../../dist/OpenCode/Host/ChatAdmission/IntentSurface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const hash = (value) => `H(${value})`

const personas = {
  engineer: 'Engineer',
  coder: 'Coder',
  manager: 'Lead',
  reviewer: 'Auditor',
  inspector: 'Investigator',
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

const inheritedSelection = (agent, physical) => {
  const owner = authority.createAuthorityRoot(
    hash,
    'rt_owner',
    'ses_owner',
    'HumanRoot',
    `owner_${physical}`,
    rootSelection('manager'),
  )
  assert.equal(owner.ok, true, owner.error)
  const inherited = authority.issueInheritedIdentitySeed(agent, owner.value)
  assert.equal(inherited.ok, true, inherited.error)
  return inherited.value
}

const rootFor = (agent = 'engineer', physical = 'msg_u1', kind = 'HumanRoot') => {
  const seed = kind === 'AgentOwnerRoot' ? inheritedSelection(agent, physical) : rootSelection(agent)
  const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', kind, physical, seed)
  assert.equal(result.ok, true, result.error)
  return result.value
}

const profile = (value) => ({
  session: value.session,
  logicalRun: value.logicalRun,
  authorityRoot: value.authorityRoot,
  authorityKind: value.authorityKind,
  participant: value.participantIdentity.participant,
  role: value.participantIdentity.role,
})

const register = (root) => authority.registerAuthority(root, authority.empty)

test('WHAT[INTERACTION-AUTHORITY-007] IA_007_unknown_origin_changes_no_projection_state', () => {
  const root = rootFor()
  const state = register(root)
  const before = JSON.stringify(state)
  assert.equal(authority.resolveKnownOrigin('msg_never_proven', 'pk_never_proven', false, state), 'UnknownOrigin')
  assert.deepEqual(intent.resolve({ sessionId: 'ses_a', physicalUserMessageId: 'msg_never_proven', explicitAgent: null, promptKey: null, hostCompaction: false, hostSynthetic: false }, { available: true, activeAgent: 'engineer', activeKind: 'HumanRoot', claims: [], acceptedContinuations: [] }), { case: 'Reject', reason: 'UnknownOriginWhileActive' })
  assert.equal(JSON.stringify(state), before)
})

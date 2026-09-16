import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

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
const rootFor = (agent = 'coder', physical = 'msg_u1') => {
  const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', 'HumanRoot', physical, rootSelection(agent))
  assert.equal(result.ok, true, result.error)
  return result.value
}
const register = (root) => authority.registerAuthority(root, authority.empty)

test('WHAT[INTERACTION-AUTHORITY-009] IA_009_pure_resolution_never_infers_human_root', () => {
  const root = rootFor('coder', 'msg_u1', 'HumanRoot')
  const state = register(root)
  assert.equal(state.activeLogicalRun.authorityKind, 'HumanRoot')
  assert.equal(authority.resolveKnownOrigin('msg_new', 'pk_any', false, state), 'UnknownOrigin')
})

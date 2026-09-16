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

test('WHAT[INTERACTION-AUTHORITY-010] IA_010_terminal_repair_identity_is_exactly_once', () => {
  const root = rootFor()
  let state = register(root)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-empty', 'run_term', 'empty', state), false)

  const repair = authority.claimContinuation(
    'pk_rep',
    'ses_a',
    'InteractionRepair',
    root,
    authority.repairPayloadDigest('req-empty', 'run_term', 'empty'),
  )
  state = authority.registerClaim(repair, state)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-empty', 'run_term', 'empty', state), true)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-empty', 'run-other', 'empty', state), false)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-empty', 'run_term', 'xml-only', state), false)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-next', 'run_term', 'empty', state), false)

  state = authority.abandonClaim('pk_rep', state)
  assert.equal(authority.repairAlreadyClaimed('ses_a', root.logicalRun, 'req-empty', 'run_term', 'empty', state), true)
})

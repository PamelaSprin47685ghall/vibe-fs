import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'

const H = (input) => `H(${input})`

const RUNTIME = 'rt_1'

const SESSION = 'ses_a'

const findClaim = (projection, key) => projection.pendingClaims.find((claim) => claim.promptKey === key)

const promptOrigin = (kind) => authority.originForContinuation(kind)

const personas = {
  engineer: 'Engineer',
  coder: 'Coder',
  manager: 'Lead',
}

const rootSelection = (participant) => {
  const role = participant === 'predictor' ? 'inspector' : participant
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      participant,
      role,
      selectedTier: 'deep',
      persona: personas[participant] ?? 'Unknown',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  }
}

const inheritedSeed = (agent, physical) => {
  const owner = authority.createAuthorityRoot(
    H,
    RUNTIME,
    SESSION,
    'HumanRoot',
    physical,
    rootSelection('manager'),
  )
  assert.equal(owner.ok, true, owner.error)
  const inherited = authority.issueInheritedIdentitySeed(agent, owner.value)
  assert.equal(inherited.ok, true, inherited.error)
  return inherited.value
}

const profileOf = () => {
  const built = authority.createAuthorityRoot(
    H,
    RUNTIME,
    SESSION,
    'HumanRoot',
    'msg_u1',
    rootSelection('engineer'),
  )
  assert.equal(built.ok, true, built.ok ? '' : built.error)
  return built.value
}

test('WHAT[dispatch-protocol-001] DP_001_every_send_member_lives_on_the_prompt_dispatcher_runtime', () => {
  const surface = dispatch.sendMemberObservation()
  assert.ok(surface.members.length >= 6, `send surface must exist, got ${surface.members.length}`)
  for (const name of surface.members) {
    assert.match(name, /^Send/, `${name} must be a PromptDispatcher.Runtime member`)
  }
  for (const member of ['SendAgentOwnerRoot', 'SendContinuation', 'SendInteractionRepair', 'SendManagerIdleEncouragement']) {
    assert.ok(surface.members.includes(member), `${member} must exist on the PromptDispatcher.Runtime send surface`)
  }
  assert.equal(surface.owner, 'PromptDispatcher.Runtime')
  assert.equal(surface.standaloneFireAndForget, false, 'no standalone postPromptFireAndForget bypass may exist')
})

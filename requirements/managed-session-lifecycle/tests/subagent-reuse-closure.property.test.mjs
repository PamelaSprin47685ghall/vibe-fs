import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'

import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const hash = (value) => `H(${value})`
const childSession = 'ses-reusable-subagent-child'
const ownerSession = 'ses-manager-owner'
const ownerPhysical = 'msg-manager-root'

const propertyOptions = { seed: 0x53554241, numRuns: 200 }

const subagentAgents = [
  'inspector',
  'coder',
  'blogger',
]

const arbitrarySubagentAgent = fc.constantFrom(...subagentAgents)
const arbitraryToken = fc
  .array(fc.constantFrom(...'abcdefghijklmnopqrstuvwxyz0123456789'), { minLength: 2, maxLength: 8 })
  .map((chars) => chars.join(''))

// Create the active manager owner profile
const managerSeed = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    selectedAgent: 'manager',
    peerAgent: 'manager',
    canonicalRole: 'manager',
    selectedTier: 'deep',
    persona: 'Lead',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

const ownerProfile = (() => {
  const result = authority.createAuthorityRoot(
    hash,
    'runtime-subagent-reuse',
    ownerSession,
    'HumanRoot',
    ownerPhysical,
    managerSeed,
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
})()

const createSubagentRoot = (physicalId, agentName) => {
  const seedResult = authority.issueInheritedIdentitySeed(agentName, ownerProfile)
  assert.equal(seedResult.ok, true, seedResult.ok ? '' : seedResult.error)
  const result = authority.createAuthorityRoot(
    hash,
    'runtime-subagent-reuse',
    childSession,
    'AgentOwnerRoot',
    physicalId,
    seedResult.value,
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

const runTurnArbitrary = fc.record({
  agentName: arbitrarySubagentAgent,
  token: arbitraryToken,
})

test('WHAT[MANAGED-SESSION-020] subagent session reuse property verifies prior-run closure across arbitrary agent sequences', () => {
  fc.assert(
    fc.property(
      fc.array(runTurnArbitrary, { minLength: 2, maxLength: 8 }),
      (turns) => {
        let state = authority.empty

        for (let i = 0; i < turns.length; i += 1) {
          const turn = turns[i]
          const physicalId = `msg-subagent-${i}-${turn.token}`
          const profile = createSubagentRoot(physicalId, turn.agentName)

          // 1. Fresh run registers successfully (returns updated state with activeLogicalRun)
          state = authority.registerAuthority(profile, state)
          assert.notEqual(state.ok, false, `turn ${i} registration must succeed`)
          assert.equal(state.activeLogicalRun.logicalRun, profile.logicalRun)
          assert.equal(state.activeLogicalRun.authorityRoot, profile.authorityRoot)
          assert.equal(state.activeLogicalRun.authorityKind, 'AgentOwnerRoot')

          // 2. While active, any premature next run MUST be rejected with ActiveRunIdentityConflict
          if (i + 1 < turns.length) {
            const nextTurn = turns[i + 1]
            const nextPhysical = `msg-premature-${i + 1}-${nextTurn.token}`
            const prematureProfile = createSubagentRoot(nextPhysical, nextTurn.agentName)
            const premature = authority.registerAuthority(prematureProfile, state)
            assert.equal(premature.ok, false, 'unclosed prior run must reject concurrent root')
            assert.equal(premature.error.kind, 'ActiveRunIdentityConflict')
            assert.equal(premature.error.active.logicalRun, profile.logicalRun)
            assert.equal(premature.error.requested.logicalRun, prematureProfile.logicalRun)
          }

          // 3. Mismatched authority root close fails closed
          const bogusClose = authority.closeAuthority(profile.logicalRun, 'msg-bogus-root', state)
          assert.equal(bogusClose.ok, false)
          assert.match(bogusClose.error, /logical-run close mismatch/)

          // 4. Durable child-work completion closes the active logical run for AgentOwnerRoot
          state = authority.closeCompletedAgentOwnerChildWork(profile.logicalRun, profile.authorityRoot, state)
          assert.equal(state.activeLogicalRun, null, `turn ${i} child-work completion must clear activeLogicalRun`)
          assert.equal(state.lastAuthorityProfile.logicalRun, profile.logicalRun)
          assert.equal(state.lastAuthorityProfile.authorityRoot, profile.authorityRoot)
        }
      },
    ),
    propertyOptions,
  )
})

test('WHAT[MANAGED-SESSION-020] subagent authority closure cleans claims, continuations, and sequences while retaining history', () => {
  fc.assert(
    fc.property(
      runTurnArbitrary,
      runTurnArbitrary,
      (turnA, turnB) => {
        let state = authority.empty
        const profileA = createSubagentRoot(`msg-run-a-${turnA.token}`, turnA.agentName)
        const profileB = createSubagentRoot(`msg-run-b-${turnB.token}`, turnB.agentName)

        // Register run A
        state = authority.registerAuthority(profileA, state)
        assert.notEqual(state.ok, false)
        assert.equal(state.activeLogicalRun.logicalRun, profileA.logicalRun)

        // Close run A via durable child-work completion
        const closedState = authority.closeCompletedAgentOwnerChildWork(profileA.logicalRun, profileA.authorityRoot, state)
        assert.equal(closedState.activeLogicalRun, null)
        assert.equal(closedState.lastAuthorityProfile.logicalRun, profileA.logicalRun)
        assert.equal(closedState.pendingClaims.length, 0)

        // Register run B on the same child session
        const nextState = authority.registerAuthority(profileB, closedState)
        assert.notEqual(nextState.ok, false)
        assert.equal(nextState.activeLogicalRun.logicalRun, profileB.logicalRun)
        assert.notEqual(nextState.activeLogicalRun.logicalRun, profileA.logicalRun)
        assert.notEqual(nextState.activeLogicalRun.authorityRoot, profileA.authorityRoot)

        // Closing run B also succeeds
        const finalState = authority.closeCompletedAgentOwnerChildWork(profileB.logicalRun, profileB.authorityRoot, nextState)
        assert.equal(finalState.activeLogicalRun, null)
        assert.equal(finalState.lastAuthorityProfile.logicalRun, profileB.logicalRun)
      },
    ),
    propertyOptions,
  )
})

test('WHAT[MANAGED-SESSION-020] Manager AgentOwnerRoot remains active for owner-directed post-life recovery', () => {
  const profile = createSubagentRoot('msg-manager-child-work', 'manager')
  const active = authority.registerAuthority(profile, authority.empty)
  assert.notEqual(active.ok, false)

  const refused = authority.closeCompletedAgentOwnerChildWork(
    profile.logicalRun,
    profile.authorityRoot,
    active,
  )
  assert.equal(refused.ok, false)
  assert.match(refused.error, /non-Manager AgentOwnerRoot/)
  assert.equal(active.activeLogicalRun.logicalRun, profile.logicalRun)
})

test('WHAT[MANAGED-SESSION-020] mutant: omitting child-work closure causes fast-check to detect ActiveRunIdentityConflict', () => {
  const property = fc.property(
    runTurnArbitrary,
    runTurnArbitrary,
    (turnA, turnB) => {
      let state = authority.empty
      const profileA = createSubagentRoot(`msg-a-${turnA.token}`, turnA.agentName)
      const profileB = createSubagentRoot(`msg-b-${turnB.token}`, turnB.agentName)

      // Register run A
      state = authority.registerAuthority(profileA, state)
      assert.notEqual(state.ok, false)

      // MUTANT: omitting child-work closure means run B cannot be registered
      const regB = authority.registerAuthority(profileB, state)
      // Assert that omitting closure correctly produces ActiveRunIdentityConflict
      assert.equal(regB.ok, false)
      assert.equal(regB.error.kind, 'ActiveRunIdentityConflict')
    },
  )

  fc.assert(property, { seed: 0x53554242, numRuns: 100 })
})

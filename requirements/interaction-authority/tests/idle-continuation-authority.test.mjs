// INTERACTION-AUTHORITY proof — Manager idle continuation occasion identity.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as finality from '../../../dist/Mission/Manager/FinalitySurface.js'

const hash = (value) => `H(${value})`
const personas = {
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
      persona: personas[agent] ?? 'Coordinator',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  }
}
const createdRoot = authority.createAuthorityRoot(hash, 'rt_idle', 'ses_idle', 'HumanRoot', 'root', rootSelection('fast-manager'))
assert.equal(createdRoot.ok, true, createdRoot.error)
const root = createdRoot.value
const register = () => authority.registerAuthority(root, authority.empty)

test('WHAT[INTERACTION-AUTHORITY-012] HOST_004_manager_idle_admission_is_exactly_once_per_terminal', () => {
  let state = register()
  assert.equal(authority.idleAlreadyAdmitted('ses_idle', root.logicalRun, 'life-idle', 'pre-t1', 'run-1', state), false)

  const idleDigest = (life, condition, run) => [life, condition, run].join('\x1f')
  const first = authority.claimContinuation('pk-idle-1', 'ses_idle', 'ManagerIdleEncouragement', root, 'fast-manager', idleDigest('life-idle', 'pre-t1', 'run-1'))
  state = authority.registerClaim(first, state)
  assert.equal(authority.idleAlreadyAdmitted('ses_idle', root.logicalRun, 'life-idle', 'pre-t1', 'run-1', state), true)
  assert.equal(authority.idleAlreadyAdmitted('ses_idle', root.logicalRun, 'life-idle', 'pre-t1', 'run-2', state), false)

  state = authority.abandonClaim('pk-idle-1', state)
  assert.equal(
    authority.idleAlreadyAdmitted('ses_idle', root.logicalRun, 'life-idle', 'pre-t1', 'run-1', state),
    false,
    'definite pre-send failure must re-open the exact Manager idle occasion',
  )

  const second = authority.claimContinuation('pk-idle-2', 'ses_idle', 'ManagerIdleEncouragement', root, 'fast-manager', idleDigest('life-idle', 'pre-t1', 'run-2'))
  state = authority.registerClaim(second, state)
  assert.equal(authority.idleAlreadyAdmitted('ses_idle', root.logicalRun, 'life-idle', 'pre-t1', 'run-2', state), true)
  assert.equal(authority.idleAlreadyAdmitted('ses_idle', root.logicalRun, 'life-idle', 'post-t1', 'run-3', state), false)
})

test('WHAT[INTERACTION-AUTHORITY-012] HOST_004_process_dedupe_key_is_per_terminal', () => {
  const first = finality.managerIdleOccasionKey('ses-manager-idle-process', 'life-manager-idle-process', 'pre-t1', 'run-1')
  const replay = finality.managerIdleOccasionKey('ses-manager-idle-process', 'life-manager-idle-process', 'pre-t1', 'run-1')
  const fresh = finality.managerIdleOccasionKey('ses-manager-idle-process', 'life-manager-idle-process', 'pre-t1', 'run-2')
  assert.equal(first, replay)
  assert.notEqual(first, fresh)
})

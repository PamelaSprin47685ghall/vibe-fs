import assert from 'node:assert/strict'
import test from 'node:test'
import {
  Authority,
  FsList,
  RolesModule,
  physicalUser,
  promoteToAuthorityRoot,
} from '../../verification-system/tests/support/domain.mjs'
import { lifeAdmission } from './support/finality-contract.mjs'

const profile = (kind, root = 'root-1') => ({
  CanonicalRole: RolesModule.Role.Manager,
  AuthorityKind: kind,
  AuthorityRootUserMessageId: promoteToAuthorityRoot(physicalUser(root)),
})

const lifecycle = ({ current, completed = [] } = {}) => ({
  CurrentLife: current,
  CompletedLives: FsList.ofArray(completed),
})

test('WHAT[FINALITY-022] AgentOwner migration is admitted only before any Life history', () => {
  const trace = { Opening: { AssignmentText: 'work' } }

  const first = lifeAdmission.ending(
    lifecycle(),
    profile(Authority.RootAuthorityKind.AgentOwnerRoot),
    trace,
  )
  assert.equal(first.name, 'InitialAgentOwnerMigration', 'first AgentOwner ending may materialize one migration Life')

  const afterCompletion = lifeAdmission.ending(
    lifecycle({ completed: [{}] }),
    profile(Authority.RootAuthorityKind.AgentOwnerRoot),
    trace,
  )
  assert.equal(
    afterCompletion.name,
    'NoLife',
    'CurrentLife=None after completion is terminal closure, never permission to rematerialize XTrace',
  )
})

test('WHAT[FINALITY-022] HumanRoot opening requires the exact authority root message id', () => {
  const active = profile(Authority.RootAuthorityKind.HumanRoot, 'root-1')

  const exact = lifeAdmission.tryHumanRootOpening(lifecycle(), active, physicalUser('root-1'))
  assert.ok(exact, 'the active authority-root message itself must open the Life')

  const laterUser = lifeAdmission.tryHumanRootOpening(
    lifecycle(),
    active,
    physicalUser('later-user-shaped-message'),
  )
  assert.equal(
    laterUser,
    undefined,
    'session-level HumanRoot authority must not turn another user-shaped message into a root',
  )
})

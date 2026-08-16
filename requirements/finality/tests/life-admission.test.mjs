import assert from 'node:assert/strict'
import test from 'node:test'
import { Role } from '../../../dist/Foundation/Roles.js'
import { RootAuthorityKind } from '../../../dist/Interaction/Authority/Model.js'
import {
  ManagerLifeAdmission_ending,
  ManagerLifeAdmission_tryHumanRootOpening,
} from '../../../dist/Mission/Manager/Life/Admission.js'
import {
  PhysicalUserMessageIdModule_create as physicalUser,
  PhysicalUserMessageIdModule_promoteToAuthorityRoot as promoteRoot,
} from '../../../dist/Foundation/Identity.js'
import { toList } from '../../verification-system/tests/support/domain/interop.mjs'

const profile = (kind, root = 'root-1') => ({
  CanonicalRole: Role.Manager,
  AuthorityKind: kind,
  AuthorityRootUserMessageId: promoteRoot(physicalUser(root)),
})

const lifecycle = ({ current, completed = [] } = {}) => ({
  CurrentLife: current,
  CompletedLives: toList(completed),
})

test('WHAT[FINALITY-022] AgentOwner migration is admitted only before any Life history', () => {
  const trace = { Opening: { AssignmentText: 'work' } }

  const first = ManagerLifeAdmission_ending(
    lifecycle(),
    profile(RootAuthorityKind.AgentOwnerRoot),
    trace,
  )
  assert.equal(first.tag, 1, 'first AgentOwner ending may materialize one migration Life')

  const afterCompletion = ManagerLifeAdmission_ending(
    lifecycle({ completed: [{}] }),
    profile(RootAuthorityKind.AgentOwnerRoot),
    trace,
  )
  assert.equal(
    afterCompletion.tag,
    2,
    'CurrentLife=None after completion is terminal closure, never permission to rematerialize XTrace',
  )
})

test('WHAT[FINALITY-022] HumanRoot opening requires the exact authority root message id', () => {
  const active = profile(RootAuthorityKind.HumanRoot, 'root-1')

  const exact = ManagerLifeAdmission_tryHumanRootOpening(lifecycle(), active, physicalUser('root-1'))
  assert.ok(exact, 'the active authority-root message itself must open the Life')

  const laterUser = ManagerLifeAdmission_tryHumanRootOpening(
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

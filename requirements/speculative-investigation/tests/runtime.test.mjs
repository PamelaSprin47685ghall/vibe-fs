// Split from tests/unit/strength/runtime.test.mjs (cutover Wave 2a); owner: speculative-investigation
//
// SPEC-INV-004 Replica authority: runtime is owner-single-flight and decision-local;
// K0 and ineligible replica authorities are rejected at registration.
// The replica tool-map narrowing (STRENGTH_004_replica_host_tool_map_*) went to
// capability-enforcement (ENF-005).

import assert from 'node:assert/strict'
import test from 'node:test'

import * as Runtime from '../../../dist/Session/StrengthRuntime.js'
import * as Authority from '../../../dist/Domain/PromptAuthority.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { Role } from '../../../dist/Kernel/Roles.js'
import { StrengthBudget } from '../../../dist/Domain/StrengthBudget.js'
import { ProviderRequestKind } from '../../../dist/Domain/PrefixCandidate.js'

const caseOf = (value) => value.cases()[value.tag]

const binding = (owner, replica, decision, role = Role.Coder, budget = StrengthBudget.K1) =>
  new Runtime.StrengthReplicaBinding(
    Id.SessionIdModule_create(owner),
    Id.SessionIdModule_create(replica),
    Id.StrengthDecisionIdModule_create(decision),
    Id.ProviderRunIdentityModule_create(`run-${decision}`),
    role,
    budget,
    65536,
    `sem-${decision}`,
    undefined,
    Authority.toolCapabilitiesFor(role, ProviderRequestKind.StrengthReplica),
  )

test('STRENGTH_014_runtime_is_owner_single_flight_and_decision_local', () => {
  const runtime = Runtime.StrengthRuntime_$ctor()
  const first = binding('owner', 'replica-1', 'd1')
  const second = binding('owner', 'replica-2', 'd2')

  assert.equal(Runtime.StrengthRuntime__Register_Z18AE9AF0(runtime, first).tag, 0)
  const duplicateOwner = Runtime.StrengthRuntime__Register_Z18AE9AF0(runtime, second)
  assert.equal(duplicateOwner.tag, 1)
  assert.equal(caseOf(duplicateOwner.fields[0]), 'OwnerAlreadyHasReplica')

  assert.equal(Runtime.StrengthRuntime__TryFindByReplica_Z31B28506(runtime, first.ReplicaSessionId)?.DecisionId, first.DecisionId)
  assert.equal(Runtime.StrengthRuntime__Retire_Z31B28506(runtime, first.ReplicaSessionId)?.DecisionId, first.DecisionId)
  assert.equal(Runtime.StrengthRuntime__TryFindByReplica_Z31B28506(runtime, first.ReplicaSessionId), undefined)
  assert.equal(Runtime.StrengthRuntime__Register_Z18AE9AF0(runtime, second).tag, 0)
})

test('STRENGTH_004_runtime_rejects_K0_and_ineligible_replica_authority', () => {
  const runtime = Runtime.StrengthRuntime_$ctor()
  const k0 = Runtime.StrengthRuntime__Register_Z18AE9AF0(runtime, binding('o1', 'r1', 'd1', Role.Coder, StrengthBudget.K0))
  assert.equal(caseOf(k0.fields[0]), 'EmptyBudget')

  const manager = Runtime.StrengthRuntime__Register_Z18AE9AF0(runtime, binding('o2', 'r2', 'd2', Role.Manager, StrengthBudget.K1))
  assert.equal(caseOf(manager.fields[0]), 'RoleIneligible')
})

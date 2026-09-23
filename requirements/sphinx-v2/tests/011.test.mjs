import assert from 'node:assert/strict'
import test from 'node:test'

import * as Core from '../../../dist/Sphinx/V2/Core/Surface.js'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'
import * as Persist from '../../../dist/Sphinx/V2/Persistence/Surface.js'

const ok = (result) => {
  assert.equal(Core.isOk(result), true)
  return Core.okValue(result)
}

// Shared fixtures. Every record is plain JS: the surface reads PascalCase fields.
const goalSpec = (text) =>
  Core.goalCreate({
    GoalId: Core.goalIdCreate('goal_loop'),
    Revision: ok(Core.revisionTryCreate(0n)),
    OriginalText: text,
    Constraints: Core.setOf([]),
    MaterialRefs: Core.setOf([]),
    AuthorizationRef: 'user-auth-1',
    CreatedBy: 'user',
    Amendments: Core.listOfItems([]),
  })

// A decision estimate. `Rank` is the only thing the selector reads, so a plan that was
// never compared has no rank at all rather than a rank of zero.
const modelEstimate = (planId, scopeId, location, rank) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: location,
  Rank: rank,
  Kind: 'model-estimate',
})

const ordinalOnly = (planId, scopeId, rank) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: null,
  Rank: rank,
  Kind: 'ordinal-only',
})

const provisional = (planId, scopeId, rank) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: null,
  Rank: rank,
  Kind: 'single-response-provisional',
})

const unestimated = (planId, scopeId) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: null,
  Rank: null,
  Kind: 'unestimated',
})

const resourceSpec = (name, unit, limit) => ({
  Name: name,
  Kind: Core.resourceKindCreate('consumed', unit),
  AuthorizedLimit: limit,
})

test('WHAT[sphinx-v2-011] each crash window has exactly one reconciliation action', () => {
  assert.equal(Loop.recoveryAction('DispatchPending'), 'dispatch')
  assert.equal(Loop.recoveryAction('ReceiptPending'), 'reconcile-by-intent')
  assert.equal(Loop.recoveryAction('RunningUnmarked'), 'reconcile-by-intent')
  assert.equal(Loop.recoveryAction('ResultPending'), 'accept-if-valid')
  assert.equal(Loop.recoveryAction('InterpretationPending'), 'interpret')
  assert.equal(Loop.recoveryAction('CancelPending'), 'await-terminal')
  assert.equal(Loop.recoveryAction('CommitPending'), 'commit-or-render')
})

test('WHAT[sphinx-v2-011] only the windows with real work left may spend', () => {
  assert.equal(Loop.recoveryMaySpend('DispatchPending'), true)
  assert.equal(Loop.recoveryMaySpend('CommitPending'), true)
  // Reconciliation and interpretation are pure; they must never bill.
  assert.equal(Loop.recoveryMaySpend('ReceiptPending'), false)
  assert.equal(Loop.recoveryMaySpend('InterpretationPending'), false)
})

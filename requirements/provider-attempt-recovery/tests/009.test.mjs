// failure-budget.test.mjs — PAR-001/002/003/004/005/007/009/020.
//
// One retry world: ProviderFailureBudget counts consecutive failures,
// ProviderFailureProjection holds the durable per-run budget view,
// ProviderFailureLedger (tested in provider-failure-ledger.test.mjs) is the
// only writer. Everything here drives the production
// Fallback/ProviderFailureSurface.js — no copied budget model.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

const {
  budget,
  providerFailureProjection,
  fold: foldFactsThroughOwner,
  authorityRootAccepted,
  providerFailureRecorded,
  providerRetryExhausted,
  providerSuccessRecorded,
  envelope: ownerEnvelope,
  providerFailureFactCaseNames,
} = failureOwner

const SESSION = 'ses_a'
const RUN = 'run_L'
const ROOT = 'msg_u1'

const ROOT_SELECTION_IDENTITY_SEED = {
  kind: 'RootSelection',
  participantIdentity: {
    selectedAgent: 'coder',
    canonicalRole: 'coder',
    persona: 'Coder',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

const identityFor = (run, { logical = RUN, root = ROOT } = {}) =>
  budget.attemptIdentity(SESSION, logical, root, run)

const rootFact = ({ kind = 'HumanRoot', logical = RUN, root = ROOT } = {}) =>
  authorityRootAccepted({
    session: SESSION,
    logicalRun: logical,
    authorityRoot: root,
    authorityKind: kind,
    identitySeed: ROOT_SELECTION_IDENTITY_SEED,
  })

const failureFact = ({ run, count, logical = RUN, root = ROOT, reason = 'provider_error' }) =>
  providerFailureRecorded({
    session: SESSION,
    logicalRun: logical,
    authorityRoot: root,
    providerRun: run,
    consecutiveFailureCount: count,
    reason,
  })

const exhaustedFact = ({ count }) =>
  providerRetryExhausted({
    session: SESSION,
    logicalRun: RUN,
    authorityRoot: ROOT,
    finalConsecutiveFailureCount: count,
  })

/** Fold a sequence of facts, numbering LocalSeq from 1. */
const foldFacts = (facts) =>
  foldFactsThroughOwner(facts.map((value, index) => ownerEnvelope({ seq: index + 1, session: SESSION, fact: value })))

const budgetOf = (projection) => providerFailureProjection.read(projection)

// ── PAR-002: the budget starts empty ─────────────────────────────────────────

test('WHAT[PAR-009] the_domain_count_is_reachable_only_through_a_confirmed_failure', () => {
  assert.equal(budget.recordFailure.length, 1)
  assert.equal(budget.recordSuccess.length, 1)

  let current = providerFailureProjection.forAuthority(RUN, ROOT)
  const first = providerFailureProjection.applyFailure(identityFor('run_same'), 1, current)
  assert.equal(first.ok, true)
  assert.deepEqual(providerFailureProjection.applyFailure(identityFor('run_same'), 2, first.value), {
    ok: false,
    error: 'AlreadyObserved',
  })
})

test('WHAT[PAR-009] the_dedupe_identity_names_the_run_the_root_and_the_attempt', () => {
  const identity = identityFor('run_1')

  assert.deepEqual(identity, { session: 'ses_a', run: 'run_L', root: 'msg_u1', attempt: 'run_1' })
  assert.equal(budget.dedupeKey(identity), ['ses_a', 'run_L', 'msg_u1', 'run_1'].join(''))

  const base = budget.dedupeKey(identity)
  assert.notEqual(budget.dedupeKey(identityFor('run_2')), base)
  assert.notEqual(budget.dedupeKey(identityFor('run_1', { logical: 'run_other' })), base)
  assert.notEqual(budget.dedupeKey(identityFor('run_1', { root: 'msg_u9' })), base)
})

// ── PAR-001: budget lifetime ─────────────────────────────────────────────────

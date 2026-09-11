import assert from 'node:assert/strict'
import test from 'node:test'

import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'

// ENF-016: freshness is re-verified at the consumption point. A stale admission cannot drive a new
// effect after the durable state moved on, and when the current facts satisfy the condition again the
// owner mints a fresh admission from a current read instead of reviving the older one. The production
// owner is the canonical chat-execution acceptance/lifecycle admission reached through the registered
// ChatExecution surface.

const evidence = (overrides = {}) => ({
  sessionId: 'ses-enf016-freshness',
  physicalUserMessageId: 'msg-enf016-freshness',
  logicalRunId: 'run-enf016-freshness',
  authorityRootUserMessageId: 'root-enf016-freshness',
  authorityKind: 'HumanRoot',
  identitySeed: {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: 'coder',
      canonicalRole: 'coder',
      selectedTier: 'deep',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  },
  providerRun: 'provider-enf016-freshness',
  origin: 'HumanRoot',
  requestKind: 'work-main',
  projectionChoice: { kind: 'UseCommittedEpoch' },
  ...overrides,
})

const accept = (attempt, appendOutcome = 'Committed') => ({
  kind: 'Accept',
  evidence: attempt,
  appendOutcome,
})

const start = (attempt, appendOutcome = 'Committed') => ({
  kind: 'ProviderStarted',
  evidence: attempt,
  appendOutcome,
})

const terminal = (disposition, attempt, appendOutcome = 'Committed') => ({
  kind: 'Terminal',
  disposition,
  evidence: attempt,
  appendOutcome,
})

test('WHAT[ENF-016] AUTHORITY_002_stale_admission_is_rejected_and_fresh_admission_reads_current_facts', async () => {
  // (1) Positive control: while the current facts admit the provider step, the very same admission
  // operation succeeds and derives its decision from a fresh read of the projection.
  const attempt = evidence()
  const admitted = await chatExecution.providerLifecycleScenario([accept(attempt), start(attempt)])

  assert.equal(admitted.ok, true, JSON.stringify(admitted.error))
  assert.equal(admitted.projection.phase, 'ProviderStarted')
  assert.deepEqual(admitted.appendCounts, { accepted: 1, providerStarted: 1, terminal: 0 })

  // (2) After the durable state advanced to a terminal, that stale admission can no longer drive a
  // new effect: typed rejection, no additional provider-start append, projection stays Terminal.
  const afterTerminal = await chatExecution.providerLifecycleScenario([
    accept(attempt),
    start(attempt),
    terminal('Completed', attempt),
    start(evidence({ providerRun: 'provider-enf016-second-run' })),
  ])

  assert.equal(afterTerminal.ok, false)
  assert.equal(afterTerminal.error.kind, 'ProviderStartedAfterTerminal')
  assert.equal(afterTerminal.error.detail, 'Completed')
  assert.deepEqual(afterTerminal.appendCounts, { accepted: 1, providerStarted: 1, terminal: 1 })
  assert.deepEqual(afterTerminal.projection, {
    sessionId: attempt.sessionId,
    physicalUserMessageId: attempt.physicalUserMessageId,
    phase: 'Terminal',
    disposition: 'Completed',
  })

  // (3) A competing provider-run version does not revive a start either: the established run wins and
  // the differing attempt is rejected without a second provider-start append.
  const competingRun = await chatExecution.providerLifecycleScenario([
    accept(attempt),
    start(attempt),
    start(evidence({ providerRun: 'provider-enf016-competing-run' })),
  ])

  assert.equal(competingRun.ok, false)
  assert.equal(competingRun.error.kind, 'ProviderRunConflict')
  assert.equal(competingRun.error.detail.established, attempt.providerRun)
  assert.equal(competingRun.error.detail.attempted, 'provider-enf016-competing-run')
  assert.deepEqual(competingRun.appendCounts, { accepted: 1, providerStarted: 1, terminal: 0 })
  assert.equal(competingRun.projection.phase, 'ProviderStarted')

  // (4) Freshness is subject-exact rather than session-wide: a new exact subject is admitted from its
  // own current observation instead of inheriting the settled subject's authority.
  const nextSubject = evidence({
    physicalUserMessageId: 'msg-enf016-next-subject',
    providerRun: 'provider-enf016-next-run',
  })
  const fresh = await chatExecution.acceptanceScenario(nextSubject, 'Committed')

  assert.equal(fresh.ok, true, JSON.stringify(fresh.error))
  assert.deepEqual(fresh.witness.key, {
    sessionId: nextSubject.sessionId,
    physicalUserMessageId: nextSubject.physicalUserMessageId,
  })
  assert.equal(fresh.witness.evidence.logicalRunId, nextSubject.logicalRunId)
  assert.equal(fresh.acceptanceAppendCount, 1)
})

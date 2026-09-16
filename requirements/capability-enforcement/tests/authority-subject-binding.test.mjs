import assert from 'node:assert/strict'
import test from 'node:test'

import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'

// ENF-015: Witness/Capability/Receipt contracts must declare the current subject plus the
// version/sequence that distinguishes the attempt, and a consumer must admit against that exact
// current subject/version instead of letting a witness drive effects directly. The production
// owner here is the canonical chat-execution acceptance/lifecycle admission
// (`ManagedChatAcceptance` / `ManagedChatProviderLifecycle`), reached through the registered
// ChatExecution surface.

const evidence = (overrides = {}) => ({
  sessionId: 'ses-enf015-authority',
  physicalUserMessageId: 'msg-enf015-authority',
  logicalRunId: 'run-enf015-authority',
  authorityRootUserMessageId: 'root-enf015-authority',
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
  providerRun: 'provider-enf015-authority',
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

test('WHAT[ENF-015] AUTHORITY_001_witness_declares_exact_subject_and_version_and_admits_fresh_at_consumption', async () => {
  // (1) The issued witness carries the exact subject (SessionId + PhysicalUserMessageId) and the
  // version attributes of the established attempt, rather than an anonymous boolean.
  const attempt = evidence()
  const accepted = await chatExecution.acceptanceScenario(attempt, 'Committed')

  assert.equal(accepted.ok, true, JSON.stringify(accepted.error))
  assert.deepEqual(accepted.witness.key, {
    sessionId: attempt.sessionId,
    physicalUserMessageId: attempt.physicalUserMessageId,
  })
  assert.equal(accepted.witness.evidence.logicalRunId, attempt.logicalRunId)
  assert.equal(accepted.witness.evidence.authorityRootUserMessageId, attempt.authorityRootUserMessageId)
  assert.equal(accepted.witness.evidence.authorityKind, attempt.authorityKind)
  assert.equal(accepted.witness.evidence.identitySeed.kind, 'RootSelection')
  assert.equal(accepted.witness.evidence.identitySeed.participantIdentity.personaCatalogVersion, 1)
  assert.equal(accepted.acceptanceAppendCount, 1)

  // (2) An attempt that claims a different subject is refused by the consumption point with a typed
  // key mismatch and produces no effect: no provider-start append and the projection stays Accepted.
  const foreign = evidence({ physicalUserMessageId: 'msg-enf015-foreign-subject' })
  const foreignAttempt = await chatExecution.providerLifecycleScenario([accept(attempt), start(foreign)])

  assert.equal(foreignAttempt.ok, false)
  assert.equal(foreignAttempt.error.kind, 'AttemptKeyMismatch')
  assert.equal(foreignAttempt.error.detail.evidenceKey.physicalUserMessageId, foreign.physicalUserMessageId)
  assert.equal(foreignAttempt.error.detail.requestedKey.physicalUserMessageId, attempt.physicalUserMessageId)
  assert.deepEqual(foreignAttempt.appendCounts, { accepted: 1, providerStarted: 0, terminal: 0 })
  assert.equal(foreignAttempt.projection.phase, 'Accepted')

  // (3) An attempt that reuses the same subject with a different version (another logical run and
  // authority root) is refused as an established-evidence conflict, again with zero effect.
  const staleVersion = evidence({
    logicalRunId: 'run-enf015-stale',
    authorityRootUserMessageId: 'root-enf015-stale',
  })
  const staleAttempt = await chatExecution.providerLifecycleScenario([accept(attempt), start(staleVersion)])

  assert.equal(staleAttempt.ok, false)
  assert.equal(staleAttempt.error.kind, 'EstablishedEvidenceConflict')
  assert.equal(staleAttempt.error.detail.established.logicalRunId, attempt.logicalRunId)
  assert.equal(staleAttempt.error.detail.attempted.logicalRunId, staleVersion.logicalRunId)
  assert.deepEqual(staleAttempt.appendCounts, { accepted: 1, providerStarted: 0, terminal: 0 })
  assert.equal(staleAttempt.projection.phase, 'Accepted')
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as settlement from '../../../dist/Execution/Session/ChatExecution/PreProviderSettlementSurface.js'

test('WHAT[CHATEXEC-007] every acquired pre-commit failure releases exactly once', () => {
  const r = settlement.testPreCommitFailureRelease()
  assert.equal(r.releases, 1)
})

test('WHAT[CHATEXEC-007] release boundary failure is typed without a second release', () => {
  const r = settlement.testReleaseBoundaryFailure()
  assert.equal(r.releases, 1)
})

test('WHAT[CHATEXEC-007] pre-provider failure cancellation and rejection settle without a provider run', () => {
  const key = { sessionId: 'ses-7-1', physicalUserMessageId: 'msg-7-1' }
  const accepted = chatExecution.acceptManagedChat('run-7-1', 'msg-root-7-1', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const term = chatExecution.terminalPreProvider(key, 'Cancelled')
  const folded = chatExecution.fold([accepted, term])
  assert.equal(folded.ok, true)
  assert.equal(folded.value[0].phase, 'Terminal')
})

test('WHAT[CHATEXEC-007] each typed pre-provider failure settles the exact accepted execution', () => {
  const r = settlement.testPreProviderSettlement()
  assert.equal(r.settled, true)
})

test('WHAT[CHATEXEC-007] rejects hostile legacy AGENT-028 membrane input before it can enter a legal managed flow', () => {
  const r = settlement.testRejectAgent028Membrane()
  assert.equal(r.rejected, true)
})

test('WHAT[CHATEXEC-007] detects missing exact pre-provider release', () => {
  const r = settlement.testMissingReleaseDetection()
  assert.equal(r.detected, true)
})

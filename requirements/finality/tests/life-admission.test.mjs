// requirements/finality/tests/life-admission.test.mjs
//
// FINALITY-022 / INTERACTION-AUTHORITY-009: Life admission decisions.
// PR 6 exemplar: the test speaks lifecycle vocabulary; FinalitySurface owns
// the profile construction and the F# boundary.

import assert from 'node:assert/strict'
import test from 'node:test'

const finality = await import('../../../dist/Mission/Manager/FinalitySurface.js')

const worldOf = (events) => {
  const out = finality.project(events)
  assert.equal(out.ok, true, JSON.stringify(out.error))
  return out.world
}

const emptyWorld = worldOf([])

const agentOwnerEnding = (opening) =>
  finality.endingAdmission(emptyWorld, 'agent-owner-root', 'root-1', 'fast-manager', 'deep-manager', 'fast', opening)

test('WHAT[FINALITY-022] AgentOwner migration is admitted only before any Life history', () => {
  const opening = { assignmentText: 'work' }

  const first = agentOwnerEnding(opening)
  assert.deepEqual(first, { kind: 'initial-agent-owner-migration' }, 'first AgentOwner ending may materialize one migration Life')

  // A Life that was completed and archived: CurrentLife=None after completion
  // is terminal closure, never permission to rematerialize XTrace.
  const closed = finality.project([
    {
      kind: 'life-opened',
      sessionId: 'ses-admission',
      lifeId: 'life-1',
      openingUserMessageId: 'msg-open',
      openingTextRef: 'blob-1',
      openingTextDigest: 'digest-1',
      openingCursorSequence: 1,
    },
    {
      kind: 'finality-requested',
      sessionId: 'ses-admission',
      lifeId: 'life-1',
      requestId: 'req-1',
      gitTreeHash: 'tree-1',
      lastWordsRef: 'blob-1',
      lastWordsDigest: 'digest-1',
      providerRun: 'run-1',
      toolCallId: 'call-1',
    },
    {
      kind: 'finality-reviewer-enlisted',
      sessionId: 'ses-admission',
      lifeId: 'life-1',
      requestId: 'req-1',
      reviewerSessionId: 'ses-reviewer',
      reviewerOrdinal: 1,
      barrierId: 'bar-1',
      gitTreeHash: 'tree-1',
      isNewReviewer: true,
    },
    {
      kind: 'finality-blessed',
      sessionId: 'ses-admission',
      lifeId: 'life-1',
      requestId: 'req-1',
      gitTreeHash: 'tree-1',
      workRecordBundleRef: 'blob-1',
      workRecordBundleDigest: 'digest-1',
    },
    {
      kind: 'life-completed',
      sessionId: 'ses-admission',
      lifeId: 'life-1',
      requestId: 'req-1',
      terminalRef: 'blob-1',
      terminalDigest: 'digest-1',
    },
  ])
  assert.equal(closed.ok, true, JSON.stringify(closed.error))
  assert.equal(finality.archivedLivesView(closed.world).length, 1)

  const afterCompletion = finality.endingAdmission(
    closed.world,
    'agent-owner-root',
    'root-1',
    'fast-manager',
    'deep-manager',
    'fast',
    opening,
  )
  assert.deepEqual(
    afterCompletion,
    { kind: 'no-life' },
    'CurrentLife=None after completion is terminal closure, never permission to rematerialize XTrace',
  )
})

test('WHAT[FINALITY-022] HumanRoot opening requires the exact authority root message id', () => {
  const exact = finality.tryHumanRootOpening(emptyWorld, 'human-root', 'root-1', 'root-1')
  assert.equal(exact, true, 'the active authority-root message itself must open the Life')

  const laterUser = finality.tryHumanRootOpening(emptyWorld, 'human-root', 'root-1', 'later-user-shaped-message')
  assert.equal(
    laterUser,
    false,
    'session-level HumanRoot authority must not turn another user-shaped message into a root',
  )
})

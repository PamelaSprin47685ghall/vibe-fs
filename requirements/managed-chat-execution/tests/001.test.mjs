import assert from 'node:assert/strict'
import test from 'node:test'
import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'

test('WHAT[CHATEXEC-001] exact key indexes two physical messages within one session', () => {
  const sessionId = 'ses-facts-distinct'
  const acceptedFirst = chatExecution.acceptManagedChat('run-distinct-1', 'msg-root-distinct-1', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      origin: 'ResolvedAtRoot',
      participant: 'coder',
      persona: 'Coder',
      personaCatalogVersion: 1,
      role: 'coder',
    },
  }, {
    sessionId,
    physicalUserMessageId: 'msg-user-distinct-1',
  }, 'work-main')
  const acceptedSecond = chatExecution.acceptManagedChat('run-distinct-2', 'msg-root-distinct-2', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      origin: 'ResolvedAtRoot',
      participant: 'coder',
      persona: 'Coder',
      personaCatalogVersion: 1,
      role: 'coder',
    },
  }, {
    sessionId,
    physicalUserMessageId: 'msg-user-distinct-2',
  }, 'work-main')

  const folded = chatExecution.fold([acceptedFirst, acceptedSecond])
  assert.equal(folded.ok, true, folded.ok ? '' : folded.error)
  assert.equal(folded.value.length, 2)
  assert.deepEqual(folded.value.map((entry) => entry.physicalUserMessageId), [
    'msg-user-distinct-1',
    'msg-user-distinct-2',
  ])
})

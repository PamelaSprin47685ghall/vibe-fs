import assert from 'node:assert/strict'
import test from 'node:test'
import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'

test('WHAT[CHATEXEC-005] ProviderStarted enforces acceptance provider run and terminal fences', () => {
  const key = { sessionId: 'ses-5-1', physicalUserMessageId: 'msg-5-1' }
  const accepted = chatExecution.acceptManagedChat('run-5-1', 'msg-root-5-1', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const start = chatExecution.providerStarted(key, 'run-5-1-p', 'use-committed-epoch', 'work-main')
  const folded = chatExecution.fold([accepted, start])
  assert.equal(folded.ok, true)
  assert.equal(folded.value[0].phase, 'ProviderStarted')
})

test('WHAT[CHATEXEC-005] equal start and terminal duplicates are semantic no-ops', () => {
  const key = { sessionId: 'ses-5-2', physicalUserMessageId: 'msg-5-2' }
  const accepted = chatExecution.acceptManagedChat('run-5-2', 'msg-root-5-2', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const start = chatExecution.providerStarted(key, 'run-5-2-p', 'use-committed-epoch', 'work-main')
  const folded = chatExecution.fold([accepted, start, start])
  assert.equal(folded.ok, true)
})

test('WHAT[CHATEXEC-005] ProviderStarted before Accepted rejects', () => {
  const key = { sessionId: 'ses-5-3', physicalUserMessageId: 'msg-5-3' }
  const start = chatExecution.providerStarted(key, 'run-5-3-p', 'use-committed-epoch', 'work-main')
  const folded = chatExecution.fold([start])
  assert.equal(folded.ok, false)
})

test('WHAT[CHATEXEC-005] exact public assistant observation alone establishes provider start', () => {
  const key = { sessionId: 'ses-5-4', physicalUserMessageId: 'msg-5-4' }
  const accepted = chatExecution.acceptManagedChat('run-5-4', 'msg-root-5-4', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const start = chatExecution.providerStarted(key, 'run-5-4-p', 'use-committed-epoch', 'work-main')
  assert.equal(chatExecution.fold([accepted, start]).value[0].phase, 'ProviderStarted')
})

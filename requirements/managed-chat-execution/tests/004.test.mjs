import assert from 'node:assert/strict'
import test from 'node:test'
import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as admission from '../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'
import * as decision from '../../../dist/OpenCode/Host/ChatAdmission/DecisionTableSurface.js'

test('WHAT[CHATEXEC-004] durable acceptance is projected before its witness exists', () => {
  const key = { sessionId: 'ses-4-1', physicalUserMessageId: 'msg-4-1' }
  const accepted = chatExecution.acceptManagedChat('run-4-1', 'msg-root-4-1', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const folded = chatExecution.fold([accepted])
  assert.equal(folded.value[0].phase, 'Accepted')
})

test('WHAT[CHATEXEC-004] exact duplicate reconstructs an equivalent witness without another append', () => {
  const key = { sessionId: 'ses-4-2', physicalUserMessageId: 'msg-4-2' }
  const accepted = chatExecution.acceptManagedChat('run-4-2', 'msg-root-4-2', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const folded = chatExecution.fold([accepted, accepted])
  assert.equal(folded.value.length, 1)
})

test('WHAT[CHATEXEC-004] established evidence conflict is typed and appends nothing', () => {
  const key = { sessionId: 'ses-4-3', physicalUserMessageId: 'msg-4-3' }
  const accepted1 = chatExecution.acceptManagedChat('run-4-3', 'msg-root-4-3', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const accepted2 = chatExecution.acceptManagedChat('run-4-3-conflict', 'msg-root-4-3', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'manager', persona: 'Lead', personaCatalogVersion: 1, role: 'manager' },
  }, key, 'work-main')
  const folded = chatExecution.fold([accepted1, accepted2])
  assert.equal(folded.ok, false)
})

test('WHAT[CHATEXEC-004] each uncertain persistence outcome acquires no capacity', async () => {
  const res = await admission.runManagedAdmissionScenario('uncertain-persistence')
  assert.equal(res.acquiredCapacity, false)
})

test('WHAT[CHATEXEC-004] fixed admission counterworlds distinguish every intent and rejection', () => {
  const table = decision.evaluateCounterworlds()
  assert.equal(table.allDistinguished, true)
})

test('WHAT[CHATEXEC-004] accepted replay reuses acceptance without another append', async () => {
  const res = await admission.runManagedAdmissionScenario('accepted-replay')
  assert.equal(res.appends, 0)
})

test('WHAT[CHATEXEC-004] identical Accepted replay is idempotent and conflicting evidence fails closed', () => {
  const key = { sessionId: 'ses-4-4', physicalUserMessageId: 'msg-4-4' }
  const accepted = chatExecution.acceptManagedChat('run-4-4', 'msg-root-4-4', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  assert.equal(chatExecution.fold([accepted, accepted]).ok, true)
})

test('WHAT[CHATEXEC-004] same admitted plan binds the same admission twice', () => {
  const key = { sessionId: 'ses-4-5', physicalUserMessageId: 'msg-4-5' }
  const accepted = chatExecution.acceptManagedChat('run-4-5', 'msg-root-4-5', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  assert.equal(chatExecution.fold([accepted, accepted]).value.length, 1)
})

test('WHAT[CHATEXEC-004] conflicting plan against the same key fails closed', () => {
  const key = { sessionId: 'ses-4-6', physicalUserMessageId: 'msg-4-6' }
  const a1 = chatExecution.acceptManagedChat('run-4-6a', 'msg-root-4-6', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const a2 = chatExecution.acceptManagedChat('run-4-6b', 'msg-root-4-6', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  assert.equal(chatExecution.fold([a1, a2]).ok, false)
})

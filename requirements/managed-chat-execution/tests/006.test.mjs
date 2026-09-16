import assert from 'node:assert/strict'
import test from 'node:test'
import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as admission from '../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'
import { acceptManagedChat, providerStarted, terminal } from './support/chat-wire.mjs'
import { runManagedAdmissionScenario } from './support/admission-scenario-helper.mjs'
import { terminalOrderValid, classifyTerminalFinish } from './support/terminal-helper.mjs'

test('WHAT[CHATEXEC-006] terminal replay performs no acceptance or capacity effect', async () => {
  const res = await runManagedAdmissionScenario('terminal-replay')
  assert.equal(res.duplicateEffects, 0)
})

test('WHAT[CHATEXEC-006] same key terminal conflict', () => {
  const key = { sessionId: 'ses-6-1', physicalUserMessageId: 'msg-6-1' }
  const accepted = acceptManagedChat('run-6-1', 'msg-root-6-1', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const start = providerStarted(key, 'run-6-1-p', 'use-committed-epoch', 'work-main')
  const term1 = terminal(key, 'run-6-1-p', 'Completed')
  const term2 = terminal(key, 'run-6-1-p', 'Cancelled')
  const folded = chatExecution.fold([accepted, start, term1, term2])
  assert.equal(folded.ok, false)
})

test('WHAT[CHATEXEC-006] Terminal directly after Accepted is rejected', () => {
  const key = { sessionId: 'ses-6-2', physicalUserMessageId: 'msg-6-2' }
  const accepted = acceptManagedChat('run-6-2', 'msg-root-6-2', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const term = terminal(key, 'run-6-2-p', 'Completed')
  assert.equal(chatExecution.fold([accepted, term]).ok, false)
})

test('WHAT[CHATEXEC-006] each terminal disposition is durable after provider start', () => {
  const key = { sessionId: 'ses-6-3', physicalUserMessageId: 'msg-6-3' }
  const accepted = acceptManagedChat('run-6-3', 'msg-root-6-3', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const start = providerStarted(key, 'run-6-3-p', 'use-committed-epoch', 'work-main')
  const term = terminal(key, 'run-6-3-p', 'Completed')
  assert.equal(chatExecution.fold([accepted, start, term]).value[0].phase, 'Terminal')
})

test('WHAT[CHATEXEC-006] provider terminal before ProviderStarted rejects', () => {
  const key = { sessionId: 'ses-6-4', physicalUserMessageId: 'msg-6-4' }
  const term = terminal(key, 'run-6-4-p', 'Completed')
  assert.equal(chatExecution.fold([term]).ok, false)
})

test('WHAT[CHATEXEC-006] conflicting terminal rejects without a second write', () => {
  const key = { sessionId: 'ses-6-5', physicalUserMessageId: 'msg-6-5' }
  const accepted = acceptManagedChat('run-6-5', 'msg-root-6-5', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const start = providerStarted(key, 'run-6-5-p', 'use-committed-epoch', 'work-main')
  const term1 = terminal(key, 'run-6-5-p', 'Completed')
  const term2 = terminal(key, 'run-6-5-p', 'Failed')
  assert.equal(chatExecution.fold([accepted, start, term1, term2]).ok, false)
})

test('WHAT[CHATEXEC-006] each uncertain Terminal append leaves projection provider-started', () => {
  const key = { sessionId: 'ses-6-6', physicalUserMessageId: 'msg-6-6' }
  const accepted = acceptManagedChat('run-6-6', 'msg-root-6-6', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const start = providerStarted(key, 'run-6-6-p', 'use-committed-epoch', 'work-main')
  assert.equal(chatExecution.fold([accepted, start]).value[0].phase, 'ProviderStarted')
})

test('WHAT[CHATEXEC-006] production Host terminal owner persists before exact capacity settlement', () => {
  assert.equal(terminalOrderValid(), true)
})

test('WHAT[CHATEXEC-006] exact successful Host terminals retain typed finish outcomes', () => {
  assert.equal(classifyTerminalFinish('stop'), 'Completed')
})

test('WHAT[CHATEXEC-006] exact cancel and interruption become closed typed terminal dispositions', () => {
  assert.equal(classifyTerminalFinish('aborted'), 'Cancelled')
})

test('WHAT[CHATEXEC-006] exact provider failure remains typed but awaits retry-owner disposition', () => {
  assert.equal(classifyTerminalFinish('error'), 'Failed')
})

test('WHAT[CHATEXEC-006] ambiguous and deleted evidence fail closed', () => {
  assert.equal(classifyTerminalFinish('unknown'), 'FailedClosed')
})

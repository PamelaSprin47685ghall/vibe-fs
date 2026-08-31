import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'

import { canonicalize, fold } from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import { queryFacts } from '../../../dist/Execution/Session/ChatExecution/StatusSurface.js'
import { projectRecord } from '../../../dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js'
import { reconcileCapacityEvidence } from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import { recoverScenarios } from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import { captureEvidence, replayEvidence, serializeEvidence } from './support/incident-evidence.mjs'

const fact = fs.readFileSync(new URL('./fixtures/chat-execution-v1.json', import.meta.url), 'utf8')
const hostContract = JSON.parse(fs.readFileSync(
  new URL('../../host-boundary/fixtures/opencode-chat-admission-1.18.18.json', import.meta.url),
  'utf8',
))
const agent028 = JSON.parse(fs.readFileSync(
  new URL('../fixtures/incidents/agent-028.json', import.meta.url),
  'utf8',
))
const surfaces = { canonicalize, fold, queryFacts, projectRecord, reconcileCapacityEvidence, recoverScenarios }
const input = {
  facts: [fact],
  key: { sessionId: 'ses-chat-fixture', physicalUserMessageId: 'msg-chat-fixture' },
  capacitySnapshot: {
    ledgerEntries: [], tokens: [], custodies: [], executions: [], waiters: [], owners: [], lineage: [],
    tokenStateCounts: { idle: 0, inFlight: 0, retiring: 0 }, activeCount: 0,
    counters: { duplicate: 0, stale: 0, conflict: 0 },
  },
  diagnostics: [{
    operation: 'AcceptedPersisted', logicalRunId: 'run-chat-fixture', sessionId: 'ses-chat-fixture',
    authorityRootUserMessageId: 'msg-chat-root', physicalUserMessageId: 'msg-chat-fixture', promptKey: null,
    providerRunIdentity: null, effectiveAgent: 'fast-coder', role: 'coder', providerRequestKind: 'work-main',
    transition: { from: null, to: 'Accepted' }, failureClass: null, retryDecision: null,
    fallbackDecision: null, capacityState: 'Released', capacityFence: null, hook: 'chat.message',
    policyClass: 'Workflow', recoveryDecision: 'ResumeAdmission', persistenceCommitment: 'Committed',
  }],
  hostContract,
  recovery: {
    scenario: 'CrashAfterAcceptance', providerObservation: 'ProviderAbsent',
    resourceObservation: 'ResourceAbsent', persistenceCommitment: 'NotCommitted',
    failurePolicy: 'NoFailureDecision',
  },
}

test('WHAT[CHATEXEC-014] replay reconstructs the canonical projection and emits only owner effect requests', async () => {
  const captured = await captureEvidence(input, surfaces)
  const replayed = await replayEvidence(serializeEvidence(captured), surfaces)

  assert.equal(replayed.ok, true)
  assert.deepEqual(replayed.execution, captured.execution)
  assert.deepEqual(replayed.capacity, captured.capacity)
  assert.deepEqual(replayed.recovery, captured.recovery)
  assert.deepEqual(replayed.operatorActions, [{
    owner: 'managed-chat-execution',
    action: 'ResumePreProvider',
    authority: 'EffectRequestOnly',
  }])
  assert.deepEqual(replayed.mutations, [])
})

test('WHAT[CHATEXEC-014] redacted agent-028 reproduces legacy session binding conflict and replays through current owners', async () => {
  assert.equal(agent028.historicalModel.first.bindingKey, agent028.historicalModel.second.bindingKey)
  assert.notEqual(
    agent028.historicalModel.first.physicalUserMessageId,
    agent028.historicalModel.second.physicalUserMessageId,
  )
  assert.equal(agent028.historicalModel.observedOutcome, 'IdentityConflict')

  const projected = fold(agent028.currentModel.facts)
  assert.equal(projected.ok, true)
  assert.deepEqual(
    projected.value.map(({ sessionId, physicalUserMessageId, phase }) => ({ sessionId, physicalUserMessageId, phase })),
    [
      { sessionId: 'session-agent-028', physicalUserMessageId: 'message-agent-028-a', phase: 'Accepted' },
      { sessionId: 'session-agent-028', physicalUserMessageId: 'message-agent-028-b', phase: 'Accepted' },
    ],
  )
  const recovery = await recoverScenarios([agent028.currentModel.recoveryScenario])
  assert.deepEqual(recovery.decisions, [agent028.currentModel.expectedDecision])
})

test('WHAT[CHATEXEC-014] duplicate replay is idempotent and does not accumulate authority', async () => {
  const captured = await captureEvidence(input, surfaces)
  const serialized = serializeEvidence(captured)
  assert.deepEqual(await replayEvidence(serialized, surfaces), await replayEvidence(serialized, surfaces))
})

test('WHAT[CHATEXEC-014] replay fails closed on tamper, version, unknown, or missing evidence', async () => {
  const captured = await captureEvidence(input, surfaces)
  const tampered = structuredClone(captured)
  tampered.execution.status.terminal = true
  await assert.rejects(replayEvidence(JSON.stringify(tampered), surfaces), /incident evidence integrity mismatch/)

  const version = structuredClone(captured)
  version.schemaVersion = 2
  await assert.rejects(replayEvidence(JSON.stringify(version), surfaces), /unsupported incident evidence schema version '2'/)

  const unknown = structuredClone(captured)
  unknown.untrusted = true
  await assert.rejects(replayEvidence(JSON.stringify(unknown), surfaces), /unknown incident evidence field 'untrusted'/)

  const missing = structuredClone(captured)
  delete missing.capacity
  await assert.rejects(replayEvidence(JSON.stringify(missing), surfaces), /missing incident evidence field 'capacity'/)
})

test('WHAT[CHATEXEC-014] replay rejects unsupported Host evidence and unknown recovery observations', async () => {
  const unsupported = { ...input, hostContract: { ...hostContract, supportedVersionRange: null, observedResult: 'unsupported' } }
  await assert.rejects(captureEvidence(unsupported, surfaces), /Host contract is not in an exact supported version/)

  const unknown = { ...input, recovery: { ...input.recovery, providerObservation: 'MaybeAlive' } }
  await assert.rejects(captureEvidence(unknown, surfaces), /recovery observation does not match scenario 'CrashAfterAcceptance'/)
})

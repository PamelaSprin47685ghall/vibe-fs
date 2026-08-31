import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'

import { canonicalize, fold } from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import { queryFacts } from '../../../dist/Execution/Session/ChatExecution/StatusSurface.js'
import { projectRecord } from '../../../dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js'
import { reconcileCapacityEvidence } from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import { recoverScenarios } from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import { captureEvidence, serializeEvidence } from './support/incident-evidence.mjs'

const fact = fs.readFileSync(new URL('./fixtures/chat-execution-v1.json', import.meta.url), 'utf8')
const hostContract = JSON.parse(fs.readFileSync(
  new URL('../../host-boundary/fixtures/opencode-chat-admission-1.18.18.json', import.meta.url),
  'utf8',
))
const capacitySnapshot = {
  ledgerEntries: [], tokens: [], custodies: [], executions: [], waiters: [], owners: [], lineage: [],
  tokenStateCounts: { idle: 0, inFlight: 0, retiring: 0 },
  activeCount: 0,
  counters: { duplicate: 0, stale: 0, conflict: 0 },
}
const causalRecord = {
  operation: 'AcceptedPersisted',
  logicalRunId: 'run-chat-fixture',
  sessionId: 'ses-chat-fixture',
  authorityRootUserMessageId: 'msg-chat-root',
  physicalUserMessageId: 'msg-chat-fixture',
  promptKey: null,
  providerRunIdentity: null,
  effectiveAgent: 'Bearer operator-secret at /home/operator/private/key',
  role: 'coder',
  providerRequestKind: 'work-main',
  transition: { from: null, to: 'Accepted' },
  failureClass: 'PersistenceFailure',
  retryDecision: 'NoRetry',
  fallbackDecision: 'NoFallback',
  capacityState: 'Released',
  capacityFence: null,
  hook: 'chat.message',
  policyClass: 'Workflow',
  recoveryDecision: 'ResumeAdmission',
  persistenceCommitment: 'Committed',
}
const surfaces = { canonicalize, fold, queryFacts, projectRecord, reconcileCapacityEvidence, recoverScenarios }

const captureInput = () => ({
  facts: [fact],
  key: { sessionId: 'ses-chat-fixture', physicalUserMessageId: 'msg-chat-fixture' },
  capacitySnapshot,
  diagnostics: [causalRecord],
  hostContract,
  recovery: {
    scenario: 'CrashAfterAcceptance',
    providerObservation: 'ProviderAbsent',
    resourceObservation: 'ResourceAbsent',
    persistenceCommitment: 'NotCommitted',
    failurePolicy: 'NoFailureDecision',
  },
})

test('WHAT[CHATEXEC-014] capture canonicalizes facts and preserves only immutable owner evidence', async () => {
  const evidence = await captureEvidence(captureInput(), surfaces)
  const projection = fold(evidence.execution.facts)
  const status = queryFacts(evidence.execution.facts, evidence.execution.key.sessionId, evidence.execution.key.physicalUserMessageId)

  assert.equal(projection.ok, true)
  assert.deepEqual(evidence.execution.projection, projection.value)
  assert.deepEqual(evidence.execution.status, status.status)
  assert.deepEqual(evidence.capacity.reconciliation, { kind: 'NoOp' })
  assert.equal(Object.isFrozen(evidence), true)
  assert.deepEqual(JSON.parse(serializeEvidence(evidence)), evidence)
})

test('WHAT[CHATEXEC-014] capture redacts known failures and rejects payload or stack fields', async () => {
  const evidence = await captureEvidence(captureInput(), surfaces)
  const serialized = serializeEvidence(evidence)
  assert.doesNotMatch(serialized, /operator-secret|\/home\/operator|stack trace/i)
  assert.match(evidence.diagnostics[0].effectiveAgent, /\[REDACTED\]/)

  await assert.rejects(
    captureEvidence({ ...captureInput(), prompt: 'raw user message' }, surfaces),
    /unknown incident capture field 'prompt'/,
  )
  await assert.rejects(
    captureEvidence({ ...captureInput(), diagnostics: [{ ...causalRecord, stack: 'stack trace' }] }, surfaces),
    /unknown causal diagnostic field 'stack'/,
  )
})

import assert from 'node:assert/strict'
import test from 'node:test'

import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'

const evidence = (overrides = {}) => ({
  sessionId: 'ses-provider-lifecycle',
  physicalUserMessageId: 'msg-provider-lifecycle',
  logicalRunId: 'run-provider-lifecycle',
  authorityRootUserMessageId: 'root-provider-lifecycle',
  authorityKind: 'HumanRoot',
  identitySeed: {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: 'fast-coder',
      peerAgent: 'deep-coder',
      canonicalRole: 'coder',
      selectedTier: 'fast',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  },
  providerRun: 'provider-provider-lifecycle',
  origin: 'HumanRoot',
  effectiveAgent: 'fast-coder',
  requestKind: 'work-main',
  projectionChoice: { kind: 'UseCommittedEpoch' },
  ...overrides,
})

const accept = (attempt = evidence(), appendOutcome = 'Committed') => ({
  kind: 'Accept',
  evidence: attempt,
  appendOutcome,
})
const start = (attempt = evidence(), appendOutcome = 'Committed') => ({
  kind: 'ProviderStarted',
  evidence: attempt,
  appendOutcome,
})
const terminal = (disposition, attempt = evidence(), appendOutcome = 'Committed') => ({
  kind: 'Terminal',
  disposition,
  evidence: attempt,
  appendOutcome,
})
const run = (...actions) => chatExecution.providerLifecycleScenario(actions)

test('WHAT[CHATEXEC-006] each terminal disposition is durable after provider start', async () => {
  for (const disposition of ['Completed', 'Cancelled', 'Rejected', 'Failed']) {
    const result = await run(accept(), start(), terminal(disposition))

    assert.equal(result.ok, true, JSON.stringify(result.error))
    assert.deepEqual(result.projection, {
      sessionId: evidence().sessionId,
      physicalUserMessageId: evidence().physicalUserMessageId,
      phase: 'Terminal',
      disposition,
    })
    assert.deepEqual(result.appendCounts, { accepted: 1, providerStarted: 1, terminal: 1 })
  }
})

test('WHAT[CHATEXEC-002] writes ProviderStarted before provider work', async () => {
  const result = await run(accept(), start(), { kind: 'ProviderWork' })

  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.deepEqual(result.trace.slice(-5), [
    'Read',
    'AppendProviderStarted',
    'Committed',
    'ReRead',
    'ProviderStartedWitness',
  ])
  assert.equal(result.providerWorkCount, 1)
  assert.equal(result.projection.phase, 'ProviderStarted')
})

test('WHAT[CHATEXEC-005] equal start and terminal duplicates are semantic no-ops', async () => {
  const result = await run(
    accept(),
    start(),
    start(),
    terminal('Completed'),
    terminal('Completed'),
  )

  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.deepEqual(result.appendCounts, { accepted: 1, providerStarted: 1, terminal: 1 })
  assert.equal(result.semanticTransitionCount, 3)
})

test('WHAT[CHATEXEC-005] ProviderStarted before Accepted rejects', async () => {
  const result = await run(start())
  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'MissingAccepted')
})

test('WHAT[CHATEXEC-006] provider terminal before ProviderStarted rejects', async () => {
  const result = await run(accept(), terminal('Completed'))
  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'ProviderNotStarted')
})

test('WHAT[CHATEXEC-006] conflicting terminal rejects without a second write', async () => {
  const result = await run(
    accept(),
    start(),
    terminal('Completed'),
    terminal('Failed'),
  )

  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'TerminalConflict')
  assert.deepEqual(result.appendCounts, { accepted: 1, providerStarted: 1, terminal: 1 })
  assert.equal(result.projection.disposition, 'Completed')
})

test('WHAT[CHATEXEC-011] exact physical provider run and evidence are frozen', async () => {
  for (const [attempt, expected] of [
    [evidence({ physicalUserMessageId: 'msg-wrong' }), 'AttemptKeyMismatch'],
    [evidence({ providerRun: 'provider-wrong' }), 'ProviderRunConflict'],
    [evidence({ effectiveAgent: 'deep-coder' }), 'EstablishedEvidenceConflict'],
  ]) {
    const startResult = await run(accept(), start(), start(attempt))
    assert.equal(startResult.ok, false)
    assert.equal(startResult.error.kind, expected)

    const terminalResult = await run(accept(), start(), terminal('Completed', attempt))
    assert.equal(terminalResult.ok, false)
    assert.equal(terminalResult.error.kind, expected)
  }
})

test('WHAT[CHATEXEC-002] each uncertain ProviderStarted append leaves projection accepted', async () => {
  for (const outcome of ['NotAttempted', 'CommitUnknown']) {
    const result = await run(accept(), start(evidence(), outcome))
    assert.equal(result.ok, false)
    assert.equal(result.error.kind, outcome)
    assert.equal(result.projection.phase, 'Accepted')
  }
})

test('WHAT[CHATEXEC-006] each uncertain Terminal append leaves projection provider-started', async () => {
  for (const outcome of ['NotAttempted', 'CommitUnknown']) {
    const result = await run(accept(), start(), terminal('Completed', evidence(), outcome))
    assert.equal(result.ok, false)
    assert.equal(result.error.kind, outcome)
    assert.equal(result.projection.phase, 'ProviderStarted')
  }
})

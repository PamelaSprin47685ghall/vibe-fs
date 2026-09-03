import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'

const fixture = readFileSync(
  new URL('./fixtures/chat-execution-v1.json', import.meta.url),
  'utf8',
).trim()

const keyWire = {
  SessionId: ['SessionId', 'ses-chat-fixture'],
  PhysicalUserMessageId: ['PhysicalUserMessageId', 'msg-chat-fixture'],
}

const factWire = (factCase, payload) => JSON.stringify(['Agent', ['ChatExecution', [factCase, payload]]])
const acceptedEvidence = JSON.parse(fixture)[1][1][1].Evidence
const startedEvidence = {
  Accepted: acceptedEvidence,
  ProviderRun: ['ProviderRunIdentity', 'provider-chat-fixture'],
  RequestKind: 'WorkMain',
  ProjectionChoice: 'UseCommittedEpoch',
}
const started = factWire('ProviderStarted', {
  Evidence: startedEvidence,
  Key: keyWire,
  SchemaVersion: 1,
})
const terminal = factWire('Terminal', {
  Disposition: 'Completed',
  Evidence: ['AfterProviderStart', startedEvidence],
  Key: keyWire,
  SchemaVersion: 1,
})

const canonicalize = (wire) => {
  const result = chatExecution.canonicalize(wire)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

const acceptedPayload = (wire) => wire[1][1][1]

test('WHAT[CHATEXEC-002] schema v1 Accepted ProviderStarted and Terminal round-trip canonically', () => {
  const acceptedCanonical = canonicalize(fixture)
  assert.equal(acceptedCanonical, fixture, 'fixture must already be canonical production FactCodec bytes')

  const history = [acceptedCanonical, canonicalize(started), canonicalize(terminal)]
  for (const line of history) assert.equal(canonicalize(line), line)

  const replayed = chatExecution.fold(history)
  assert.equal(replayed.ok, true, replayed.ok ? '' : replayed.error)
  assert.deepEqual(replayed.value, [
    {
      sessionId: 'ses-chat-fixture',
      physicalUserMessageId: 'msg-chat-fixture',
      phase: 'Terminal',
      disposition: 'Completed',
      identity: {
        logicalRunId: 'run-chat-fixture',
        authorityRootUserMessageId: 'msg-chat-root',
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
        providerRun: 'provider-chat-fixture',
        origin: 'HumanRoot',
        effectiveAgent: 'fast-coder',
        requestKind: 'work-main',
        projectionChoice: { kind: 'UseCommittedEpoch' },
      },
    },
  ])
})

test('WHAT[CHATEXEC-009] durable execution fact round-trip excludes process-local artifacts', () => {
  const history = [canonicalize(fixture), canonicalize(started), canonicalize(terminal)]

  for (const line of history) {
    assert.doesNotMatch(
      line,
      /"[^"]*(?:lease|handle|binding|waiter|callback|queue|cancellationToken|subscription)[^"]*"\s*:/i,
    )
  }
})

test('WHAT[CHATEXEC-002] unknown schema version fails closed during production fold', () => {
  const unknown = JSON.parse(fixture)
  acceptedPayload(unknown).SchemaVersion = 2
  const result = chatExecution.fold([JSON.stringify(unknown)])

  assert.equal(result.ok, false)
  assert.notEqual(result.error, '')
})

test('WHAT[CHATEXEC-011] malformed exact identity seed is rejected by the production codec', () => {
  const malformed = JSON.parse(fixture)
  acceptedPayload(malformed).Evidence.IdentitySeed[0] = 'ForgedSeed'
  const result = chatExecution.canonicalize(JSON.stringify(malformed))

  assert.equal(result.ok, false)
  assert.notEqual(result.error, '')
})

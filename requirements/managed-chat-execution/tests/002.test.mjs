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
  assert.equal(result.ok, true, result.error)
  return result.value
}

const acceptedPayload = (wire) => wire[1][1][1]

test('WHAT[CHATEXEC-002] schema v1 Accepted ProviderStarted and Terminal round-trip canonically', () => {
  const acceptedCanonical = canonicalize(fixture)
  assert.doesNotMatch(acceptedCanonical, /PeerAgent|EffectiveAgent/, 'canonical encoding drops raw v1 legacy agent fields')
  assert.equal(canonicalize(acceptedCanonical), acceptedCanonical, 'canonical bytes are a fixed point')

  const history = [acceptedCanonical, canonicalize(started), canonicalize(terminal)]
  for (const line of history) assert.equal(canonicalize(line), line)

  const replayed = chatExecution.fold(history)
  assert.equal(replayed.ok, true, replayed.error)
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
            origin: 'ResolvedAtRoot',
            participant: 'coder',
            persona: 'Coder',
            personaCatalogVersion: 1,
            role: 'coder',
          },
        },
        providerRun: 'provider-chat-fixture',
        origin: 'HumanRoot',
        participant: 'coder',
        role: 'coder',
        requestKind: 'work-main',
        projectionChoice: { kind: 'UseCommittedEpoch' },
      },
    },
  ])
})

test('WHAT[CHATEXEC-002] unknown schema version fails closed during production fold', () => {
  const unknown = JSON.parse(fixture)
  acceptedPayload(unknown).SchemaVersion = 2
  const result = chatExecution.fold([JSON.stringify(unknown)])

  assert.equal(result.ok, false)
  assert.notEqual(result.error, '')
})

test('WHAT[CHATEXEC-002] online prefix integration equals replay from the same canonical facts', () => {
  const key = { sessionId: 'ses-facts-replay', physicalUserMessageId: 'msg-user-replay' }
  const accepted = chatExecution.acceptManagedChat('run-replay', 'msg-root-replay', 'HumanRoot', {
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
  }, key, 'work-main')
  const start = chatExecution.providerStarted(key, 'run-replay-provider', 'use-committed-epoch', 'work-main')
  const terminal = chatExecution.terminal(key, 'run-replay-provider', 'Completed')

  const folded = chatExecution.fold([accepted, start, terminal])
  assert.equal(folded.ok, true, folded.error)
  assert.equal(folded.value[0].phase, 'Terminal')
  assert.equal(folded.value[0].disposition, 'Completed')
})

test('WHAT[CHATEXEC-002] writes ProviderStarted before provider work', () => {
  const key = { sessionId: 'ses-start-1', physicalUserMessageId: 'msg-start-1' }
  const accepted = chatExecution.acceptManagedChat('run-1', 'msg-root-1', 'HumanRoot', {
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
  }, key, 'work-main')
  const start = chatExecution.providerStarted(key, 'provider-run-1', 'use-committed-epoch', 'work-main')
  const folded = chatExecution.fold([accepted, start])
  assert.equal(folded.ok, true)
  assert.equal(folded.value[0].phase, 'ProviderStarted')
})

test('WHAT[CHATEXEC-002] each uncertain ProviderStarted append leaves projection accepted', () => {
  const key = { sessionId: 'ses-start-2', physicalUserMessageId: 'msg-start-2' }
  const accepted = chatExecution.acceptManagedChat('run-2', 'msg-root-2', 'HumanRoot', {
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
  }, key, 'work-main')
  const folded = chatExecution.fold([accepted])
  assert.equal(folded.ok, true)
  assert.equal(folded.value[0].phase, 'Accepted')
})

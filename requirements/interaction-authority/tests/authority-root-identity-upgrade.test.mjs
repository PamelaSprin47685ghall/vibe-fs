import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

import * as journalCodec from '../../../dist/Persistence/Journal/CodecSurface.js'
import * as factCodec from '../../../dist/Persistence/Journal/FactCodecSurface.js'

const legacyHumanRoot = readFileSync(
  new URL('./fixtures/authority-root-v1.json', import.meta.url),
  'utf8',
).trim()

const currentPayload = {
  SchemaVersion: 2,
  SessionId: 'ses-authority-v2',
  LogicalRunId: 'run-authority-v2',
  AuthorityRootUserMessageId: 'root-authority-v2',
  AuthorityKind: 'HumanRoot',
  IdentitySeed: {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: 'coder',
      peerAgent: 'coder',
      canonicalRole: 'coder',
      selectedTier: 'deep',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  },
}

const inheritedPayload = {
  ...currentPayload,
  SessionId: 'ses-authority-child',
  LogicalRunId: 'run-authority-child',
  AuthorityRootUserMessageId: 'root-authority-child',
  AuthorityKind: 'AgentOwnerRoot',
  IdentitySeed: {
    kind: 'InheritedFromOwner',
    ownerSession: 'ses-authority-owner',
    ownerLogicalRun: 'run-authority-owner',
    ownerAuthorityRoot: 'root-authority-owner',
    participantIdentity: {
      selectedAgent: 'coder',
      peerAgent: 'coder',
      canonicalRole: 'coder',
      selectedTier: 'deep',
      persona: 'Lead',
      personaCatalogVersion: 1,
      origin: 'InheritedFromOwner',
    },
  },
}

const currentFact = (payload = currentPayload) => ({
  family: 'Prompt',
  case: 'AuthorityRootAccepted',
  payload,
})

const envelope = (fact = currentFact()) => ({
  runtime: 'rt-authority-codec',
  seq: 1,
  observedAt: '2026-01-02T03:04:05Z',
  id: 'a'.repeat(32),
  stream: { kind: 'Session', id: currentPayload.SessionId },
  providerRun: null,
  fact,
})

const authorityCase = (value) => {
  if (Array.isArray(value)) {
    if (value[0] === 'AuthorityRootAccepted' && value[1] && typeof value[1] === 'object') return value
    for (const item of value) {
      const found = authorityCase(item)
      if (found) return found
    }
  } else if (value && typeof value === 'object') {
    for (const item of Object.values(value)) {
      const found = authorityCase(item)
      if (found) return found
    }
  }
  return null
}

const replacePayload = (line, change) => {
  const fact = JSON.parse(line)
  const taggedCase = authorityCase(fact)
  assert.notEqual(taggedCase, null, 'AuthorityRootAccepted case must be present')
  change(taggedCase[1])
  return JSON.stringify(fact)
}

const identityInSeed = (seed) => seed[0] === 'RootSelection' ? seed[1] : seed[1].ParticipantIdentity

const envelopeLineForRawFact = (line) => {
  const value = JSON.parse(journalCodec.serialize(envelope()))
  value.Fact = JSON.parse(line)
  return JSON.stringify(value)
}

const eventForRawFact = (line) => {
  const event = journalCodec.encode([], [], envelope())
  event.payload.Fact = JSON.parse(line)
  return event
}

const assertDecodeErrorAcrossReplayRoutes = (line, expected) => {
  const factResult = factCodec.decode(line)
  assert.equal(factResult.ok, false)
  assert.match(factResult.error, expected)

  const envelopeResult = journalCodec.deserialize(envelopeLineForRawFact(line))
  assert.equal(envelopeResult.ok, false)
  assert.match(envelopeResult.error, expected)

  const eventResult = journalCodec.decode(eventForRawFact(line))
  assert.equal(eventResult.ok, false)
  assert.match(eventResult.error, expected)
}

test('WHAT[INTERACTION-AUTHORITY-003] current schema-v2 authority bytes round-trip canonically', () => {
  const factLine = factCodec.encode(currentFact())
  const decodedFact = factCodec.decode(factLine)
  assert.equal(decodedFact.ok, true, decodedFact.ok ? '' : decodedFact.error)
  assert.equal(decodedFact.case, 'AuthorityRootAccepted')
  assert.deepEqual(decodedFact.payload, currentPayload)
  assert.equal(Object.hasOwn(decodedFact.payload, 'ParticipantIdentity'), false)
  assert.equal(decodedFact.line, factLine)

  const envelopeLine = journalCodec.serialize(envelope())
  const decodedEnvelope = journalCodec.deserialize(envelopeLine)
  assert.equal(decodedEnvelope.ok, true, decodedEnvelope.ok ? '' : decodedEnvelope.error)
  assert.deepEqual(decodedEnvelope.value.fact, currentFact())
  assert.equal(journalCodec.serialize(decodedEnvelope.value), envelopeLine)

  const event = journalCodec.encode([], [], envelope())
  const decodedEvent = journalCodec.decode(event)
  assert.equal(decodedEvent.ok, true, decodedEvent.ok ? '' : decodedEvent.error)
  assert.deepEqual(decodedEvent.value.fact, currentFact())
  assert.equal(decodedEvent.value.line, envelopeLine)
})

test('WHAT[INTERACTION-AUTHORITY-003] current AgentOwnerRoot retains exact inherited owner provenance', () => {
  const line = factCodec.encode(currentFact(inheritedPayload))
  const decoded = factCodec.decode(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.deepEqual(decoded.payload.IdentitySeed, inheritedPayload.IdentitySeed)
})

test('WHAT[INTERACTION-AUTHORITY-003] repeated legacy HumanRoot replay upgrades to identical schema-v2 identity', () => {
  const factFirst = factCodec.decode(legacyHumanRoot)
  const factSecond = factCodec.decode(legacyHumanRoot)
  assert.equal(factFirst.ok, true, factFirst.ok ? '' : factFirst.error)
  assert.equal(factSecond.ok, true, factSecond.ok ? '' : factSecond.error)
  assert.equal(factFirst.payload.SchemaVersion, 2)
  assert.equal(factFirst.payload.IdentitySeed.kind, 'RootSelection')
  assert.deepEqual(factSecond.payload.IdentitySeed, factFirst.payload.IdentitySeed)

  const envelopeLine = envelopeLineForRawFact(legacyHumanRoot)
  const envelopeFirst = journalCodec.deserialize(envelopeLine)
  const envelopeSecond = journalCodec.deserialize(envelopeLine)
  assert.equal(envelopeFirst.ok, true, envelopeFirst.ok ? '' : envelopeFirst.error)
  assert.equal(envelopeSecond.ok, true, envelopeSecond.ok ? '' : envelopeSecond.error)
  assert.deepEqual(envelopeFirst.value.fact.payload.IdentitySeed, factFirst.payload.IdentitySeed)
  assert.deepEqual(envelopeSecond.value.fact.payload.IdentitySeed, factFirst.payload.IdentitySeed)

  const event = eventForRawFact(legacyHumanRoot)
  const eventFirst = journalCodec.decode(event)
  const eventSecond = journalCodec.decode(event)
  assert.equal(eventFirst.ok, true, eventFirst.ok ? '' : eventFirst.error)
  assert.equal(eventSecond.ok, true, eventSecond.ok ? '' : eventSecond.error)
  assert.deepEqual(eventFirst.value.fact.payload.IdentitySeed, factFirst.payload.IdentitySeed)
  assert.deepEqual(eventSecond.value.fact.payload.IdentitySeed, factFirst.payload.IdentitySeed)
})

test('WHAT[INTERACTION-AUTHORITY-003] unknown authority schema fails closed precisely', () => {
  assert.throws(
    () => factCodec.encode(currentFact({ ...currentPayload, SchemaVersion: 1 })),
    /AuthorityRootAccepted encoder requires SchemaVersion 2, got 1/,
  )

  const unknown = replacePayload(factCodec.encode(currentFact()), (payload) => {
    payload.SchemaVersion = 99
  })
  assertDecodeErrorAcrossReplayRoutes(unknown, /AuthorityRootAccepted schema version is unsupported: 99/)
})

test('WHAT[INTERACTION-AUTHORITY-003] malformed legacy identity fails closed at the missing field', () => {
  const malformed = replacePayload(legacyHumanRoot, (payload) => {
    delete payload.SelectedAgent
  })
  assertDecodeErrorAcrossReplayRoutes(malformed, /SelectedAgent/)
})

test('WHAT[INTERACTION-AUTHORITY-003] stale legacy peer fields normalize to canonical identity', () => {
  const stale = replacePayload(legacyHumanRoot, (payload) => {
    payload.PeerAgent = 'reviewer'
    payload.SelectedTier = 'fast'
  })
  const decoded = factCodec.decode(stale)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.payload.IdentitySeed.participantIdentity.selectedAgent, 'coder')
  assert.equal(decoded.payload.IdentitySeed.participantIdentity.peerAgent, 'coder')
  assert.equal(decoded.payload.IdentitySeed.participantIdentity.selectedTier, 'deep')
})

test('WHAT[INTERACTION-AUTHORITY-003] malformed schema-v2 identity fails closed at the missing field', () => {
  const malformed = replacePayload(factCodec.encode(currentFact()), (payload) => {
    delete identityInSeed(payload.IdentitySeed).Persona
  })
  assertDecodeErrorAcrossReplayRoutes(malformed, /Persona/)
})

test('WHAT[INTERACTION-AUTHORITY-003] mismatched schema-v2 identity fails closed precisely', () => {
  const mismatch = replacePayload(factCodec.encode(currentFact()), (payload) => {
    identityInSeed(payload.IdentitySeed).Persona = 'Engineer'
  })
  assertDecodeErrorAcrossReplayRoutes(
    mismatch,
    /participant identity Persona mismatch: expected Coder, got Engineer/,
  )
})

test('WHAT[INTERACTION-AUTHORITY-003] rejects unprovable historical identity', () => {
  const agentOwner = replacePayload(legacyHumanRoot, (payload) => {
    payload.AuthorityKind = 'AgentOwnerRoot'
  })
  assertDecodeErrorAcrossReplayRoutes(
    agentOwner,
    /legacy AuthorityRootAccepted v1 AgentOwnerRoot cannot prove participant identity/,
  )
})

test('WHAT[INTERACTION-AUTHORITY-003] participant identity is not a second live fact case', () => {
  const invented = JSON.parse(legacyHumanRoot)
  const taggedCase = authorityCase(invented)
  assert.notEqual(taggedCase, null)
  taggedCase[0] = 'ParticipantIdentityInstalled'
  assertDecodeErrorAcrossReplayRoutes(JSON.stringify(invented), /ParticipantIdentityInstalled/)
})

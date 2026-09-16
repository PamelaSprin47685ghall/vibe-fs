import assert from 'node:assert/strict'
import test from 'node:test'
import * as envelope from '../../../dist/Persistence/EventStore/EnvelopeSurface.js'
import * as journalCodec from '../../../dist/Persistence/Journal/EventStoreJournalCodecSurface.js'
import * as factCodec from '../../../dist/Persistence/FactCodecSurface.js'
import * as hostTurn from '../../../dist/Persistence/HostTurnObservedSurface.js'
import * as gate from '../../../dist/Persistence/UnifiedStoreGateSurface.js'
import * as hostTurnObservedSurface from '../../../dist/Execution/Delegation/HostTurnObservedSurface.js'

test('WHAT[DURABLE-EVENTS-002] PERSIST_001_an_envelope_serializes_to_exactly_one_line', () => {
  const env = envelope.createEnvelope({
    streamId: 's1',
    eventType: 'TypeA',
    payload: { key: 'value' },
  })
  const line = envelope.serializeEnvelope(env)
  assert.equal(line.includes('\n'), true)
  assert.equal(line.indexOf('\n'), line.length - 1)
})

test('WHAT[DURABLE-EVENTS-002] journal_codec_encodes_without_version_field', () => {
  const enc = journalCodec.encodeFact({ type: 'TestFact', data: 123 })
  assert.equal(enc.version, undefined)
  assert.equal(enc.formatVersion, undefined)
})

test('WHAT[DURABLE-EVENTS-002] fact_codec_encodes_additive_vocabulary', () => {
  const fact = factCodec.createFact('NewAdditiveType', { foo: 'bar' })
  assert.equal(fact.eventType, 'NewAdditiveType')
})

test('WHAT[DURABLE-EVENTS-002] host_turn_observed_envelope_has_no_version', () => {
  const env = hostTurn.createTurnEnvelope('turn-1', { status: 'ok' })
  assert.equal(env.version, undefined)
})

test('WHAT[DURABLE-EVENTS-002] gate_rejects_versioned_schemas', () => {
  assert.equal(gate.isAdditiveEnvelope({ eventType: 'V1Type', schemaVersion: 1 }), false)
})

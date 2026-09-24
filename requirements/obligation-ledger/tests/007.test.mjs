import assert from 'node:assert/strict'
import test from 'node:test'
import * as envelope from '../../../dist/Persistence/Journal/ObligationEnvelopeSurface.js'

// obligation-ledger-007: the retired obligation-ledger envelope family stays
// recognisable for audit and migration, and nothing more. These tests pin the
// honest refusal — a decoder that returned a half-decoded shape would let a caller
// believe it still had a ledger.
test('WHAT[obligation-ledger-007] legacy envelope family is named without a decoder', () => {
  assert.equal(envelope.legacyFamilyName, 'MagicTodo')
})

test('WHAT[obligation-ledger-007] a legacy envelope decodes to a typed refusal, never a value', () => {
  const result = envelope.deserializeLegacyEnvelope('{"case":"TodoWriteAccepted"}')

  assert.equal(result.ok, false)
  assert.equal(result.family, 'MagicTodo')
  assert.match(result.error, /retired/i)

  // The refusal must not smuggle a payload back: a decoded value would be a second,
  // weaker source of truth for a protocol the system retired.
  assert.deepEqual(Object.keys(result).sort(), ['error', 'family', 'ok'])
})

test('WHAT[obligation-ledger-007] an empty or malformed legacy envelope is refused the same way', () => {
  for (const encoded of ['', 'not json', '{}']) {
    const result = envelope.deserializeLegacyEnvelope(encoded)
    assert.equal(result.ok, false, `'${encoded}' must be refused`)
    assert.equal(result.family, 'MagicTodo')
  }
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as envelope from '../../../dist/Persistence/Journal/ObligationEnvelopeSurface.js'

test('WHAT[obligation-ledger-007] the retired decoder currently returns explicit refusal instead of a usable legacy fact', () => {
  for (const encoded of ['', 'not json', '{"case":"TodoWriteAccepted"}']) {
    assert.deepEqual(envelope.deserializeLegacyEnvelope(encoded), {
      ok: false,
      family: 'MagicTodo',
      error: 'legacy MagicTodo fact envelope decoder is retired (obligation-ledger-007)',
    })
  }
})

test.todo('WHAT[obligation-ledger-007] controlled legacy audit reading retains old accepted facts without activating gates prefix switching or Host writes')

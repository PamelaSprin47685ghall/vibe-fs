import assert from 'node:assert/strict'
import test from 'node:test'

import * as journal from '../../../dist/Persistence/Journal/Surface.js'

test('WHAT[EXECFAIL-007] journal writer outcomes preserve exact persistence commitment', () => {
  assert.deepEqual(journal.JournalSurface_mapAppendFailure({ kind: 'WriterUnavailable', diagnostic: 'writer closing' }), {
    failure: 'PersistenceFailure', commitment: 'NotCommitted', diagnostic: 'writer closing',
  })
  assert.deepEqual(journal.JournalSurface_mapAppendFailure({ kind: 'FactRejected', diagnostic: 'durable semantic cut' }), {
    failure: 'PersistenceFailure', commitment: 'Committed', diagnostic: 'durable semantic cut',
  })
  assert.deepEqual(journal.JournalSurface_mapAppendFailure({ kind: 'WriteUnknown', diagnostic: 'flush receipt absent' }), {
    failure: 'PersistenceFailure', commitment: 'Unknown', diagnostic: 'flush receipt absent',
  })
})

test('WHAT[EXECFAIL-008] persistence diagnostics cannot change commitment', () => {
  for (const diagnostic of ['definitely succeeded', 'definitely failed', 'retry me']) {
    assert.equal(journal.JournalSurface_mapAppendFailure({ kind: 'WriteUnknown', diagnostic }).commitment, 'Unknown')
  }
})

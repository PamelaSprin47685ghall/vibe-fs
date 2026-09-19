// requirements/repository-programming/tests/026.test.mjs
//
// Law: repository-programming-026
// Scenario T18, T19: Separation of transaction ReadSnapshots (including grep scans)
// from case Substantive Access, and handling uncommitted/failed mutations.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as jsSurface from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

test('WHAT[repository-programming-026] T18_transaction_read_snapshots_and_substantive_access_are_strictly_separated', async () => {
  // Proves that files read during grep scanning enter ReadSnapshots for CAS conflict checking,
  // but are EXCLUDED from the substantive access collection.
  assert.equal(typeof jsSurface.createTransactionContext, 'function', 'must export createTransactionContext')
  const ctx = jsSurface.createTransactionContext()

  // Simulate grep operation scanning multiple files
  ctx.recordGrepScan(['src/a.fs', 'src/b.fs', 'src/c.fs'])
  // Explicit read
  ctx.recordExplicitRead('src/a.fs')

  const readSnapshots = ctx.getReadSnapshots()
  const substantiveAccess = ctx.getSubstantiveAccess()

  assert.equal(readSnapshots.length, 3, 'ReadSnapshots must include all scanned files for CAS')
  assert.deepEqual(substantiveAccess, ['src/a.fs'], 'SubstantiveAccess must only include explicit reads')
})

test('WHAT[repository-programming-026] T19_uncommitted_or_aborted_transaction_effects_do_not_record_substantive_modifications', async () => {
  // Staged mutations (effectPaths) are intents, not committed effects.
  // If transaction aborts or fails preflight, substantive access must NOT contain modifications.
  assert.equal(typeof jsSurface.createTransactionContext, 'function')
  const ctx = jsSurface.createTransactionContext()

  ctx.stageWrite('src/new-file.fs', 'content')
  ctx.abort()

  const substantiveAccess = ctx.getSubstantiveAccess()
  assert.equal(substantiveAccess.includes('src/new-file.fs'), false, 'uncommitted mutation must not enter substantive access')
})

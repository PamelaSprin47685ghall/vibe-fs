import assert from 'node:assert/strict'
import test from 'node:test'
import * as cutoff from '../../../dist/OpenCode/Host/PrefixEpochCutoffSurface.js'

test('WHAT[OBLIGATION-LEDGER-021] committed cutoff is supplied by one previous locator, never by scanning Accepted history', () => {
  assert.equal(cutoff.testCommittedCutoffSuppliedByOneLocator(), true)
})

test('WHAT[OBLIGATION-LEDGER-021] TodoCheckpoint evidence binds trigger plus O(1) previous committed locator', () => {
  assert.equal(cutoff.testTodoCheckpointEvidenceBindsO1Locator(), true)
})

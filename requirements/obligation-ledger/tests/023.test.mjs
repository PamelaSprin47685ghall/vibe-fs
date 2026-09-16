import assert from 'node:assert/strict'
import test from 'node:test'
import * as boundary from '../../../dist/OpenCode/Host/MagicTodoProviderBoundarySurface.js'

test('WHAT[OBLIGATION-LEDGER-023] manager guideline freezes ledger discipline as Manager-only content', () => {
  assert.equal(boundary.testManagerGuidelineFreezesDiscipline(), true)
})

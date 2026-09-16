import assert from 'node:assert/strict'
import test from 'node:test'
import * as shared from '../../../dist/OpenCode/Host/SharedStateSurface.js'

test('WHAT[HOST-BOUNDARY-010] SHARED_dictionaries_are_live_singletons_shared_across_importers', async () => {
  const d1 = shared.getRegistry()
  const d2 = shared.getRegistry()
  assert.equal(d1, d2)
})

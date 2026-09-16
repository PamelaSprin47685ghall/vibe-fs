import assert from 'node:assert/strict'
import test from 'node:test'
import * as shared from '../../../dist/OpenCode/Host/SharedStateSurface.js'

test('WHAT[HOST-BOUNDARY-010] SHARED_dictionaries_are_live_singletons_shared_across_importers', async () => {
  shared.clearSessionParents()
  shared.putSessionParent('session-parent', 'ses-root')
  assert.equal(shared.getSessionParent('session-parent'), 'ses-root')
  assert.equal(shared.getSessionParent('nonexistent'), null)

  const again = await import('../../../dist/OpenCode/Host/SharedStateSurface.js')
  assert.equal(again.getSessionParent('session-parent'), 'ses-root')
  shared.clearSessionParents()
})

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  putSessionParent,
  getSessionParent,
  clearSessionParents,
  tryBindRootWorkspace,
  tryGetRootWorkspace,
  firstBoundRootWorkspace,
  selectContinuationDirectory,
} from '../../../dist/OpenCode/Host/SharedStateSurface.js'
import * as sharedStateSurface from '../../../dist/OpenCode/Host/SharedStateSurface.js'



test('WHAT[HOST-BOUNDARY-010] SHARED_dictionaries_are_live_singletons_shared_across_importers', async () => {
  // SessionParents: mutations made through one import must be visible through
  // a fresh dynamic import of the same surface. A per-instance Map (the
  // HOST-012 failure mode) would not retain the entry across imports.
  clearSessionParents()
  putSessionParent('session-parent', 'ses-root')
  assert.equal(getSessionParent('session-parent'), 'ses-root')
  assert.equal(getSessionParent('nonexistent'), null)

  const again = await import('../../../dist/OpenCode/Host/SharedStateSurface.js')
  assert.equal(again.getSessionParent('session-parent'), 'ses-root',
    'mutation made through one import must be visible through a fresh import')

  // Isolation: leave the shared singletons clean for sibling tests.
  clearSessionParents()
})

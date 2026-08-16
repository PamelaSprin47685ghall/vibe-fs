import assert from 'node:assert/strict'
import test from 'node:test'
import { sharedState } from './support/host-surface.mjs'

test('WHAT[HOST-BOUNDARY-010] SHARED_dictionaries_are_live_singletons_shared_across_importers', async () => {
  sharedState.clear()
  sharedState.put('session-parent', 'ses-root')
  const again = await import('./support/host-surface.mjs')
  assert.equal(again.sharedState.get('session-parent'), 'ses-root')
})

test('WHAT[HOST-BOUNDARY-010] SHARED_root_workspace_atom_round_trips_and_restores', () => {
  sharedState.put('root-workspace', '/tmp/shared-root-workspace')
  assert.equal(sharedState.get('root-workspace'), '/tmp/shared-root-workspace')
  sharedState.put('root-workspace', undefined)
  assert.equal(sharedState.get('root-workspace'), undefined)
})

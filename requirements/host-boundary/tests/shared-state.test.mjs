import assert from 'node:assert/strict'
import test from 'node:test'
import {
  putSessionParent,
  getSessionParent,
  clearSessionParents,
  addReviewGuardNudge,
  hasReviewGuardNudge,
  clearReviewGuardNudges,
  tryBindRootWorkspace,
  tryGetRootWorkspace,
  firstBoundRootWorkspace,
  selectContinuationDirectory,
} from '../../../dist/OpenCode/Host/SharedStateSurface.js'
import * as sharedStateSurface from '../../../dist/OpenCode/Host/SharedStateSurface.js'

// HOST-BOUNDARY-010 / HOST-012: SessionParents / VerdictSessions /
// SessionDirectories / ReviewGuardNudges are module-level shared singletons
// (OpenCode/Host/SharedState.fs). All plugin instances — root and worktree —
// read/write the same state through the SharedStateSurface boundary; the
// physical Map/Set singletons stay opaque behind narrow put/get/clear
// operations. The behavioral proof: a mutation made through one import is
// visible through a fresh dynamic import — a per-instance copy (the HOST-012
// failure mode) would not retain the entry.

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

  // ReviewGuardNudges: the cross-instance reservation Set whose key must NOT
  // contain RuntimeId (root + worktree would each send a twin nudge). Same
  // singleton proof — a fresh import sees the reservation.
  clearReviewGuardNudges()
  addReviewGuardNudge('ses-root|barrier-42|missing-verdict')
  assert.equal(hasReviewGuardNudge('ses-root|barrier-42|missing-verdict'), true)
  assert.equal(again.hasReviewGuardNudge('ses-root|barrier-42|missing-verdict'), true,
    'reservation made through one import must be visible through a fresh import')

  // Isolation: leave the shared singletons clean for sibling tests.
  clearSessionParents()
  clearReviewGuardNudges()
})

test('WHAT[HOST-BOUNDARY-031] SHARED_root_workspace_is_first_bound_behind_typed_capabilities', async () => {
  assert.equal(tryGetRootWorkspace(), null)
  assert.equal(tryBindRootWorkspace(null), false, 'None must not occupy the first-bind slot')
  assert.equal(tryBindRootWorkspace(''), false, 'blank must not occupy the first-bind slot')
  assert.equal(tryBindRootWorkspace('  '), false, 'whitespace must not occupy the first-bind slot')
  assert.equal(tryGetRootWorkspace(), null)

  const attempts = await Promise.all(
    ['/tmp/first-root-workspace', '/tmp/concurrent-root-workspace']
      .map(candidate => Promise.resolve().then(() => ({ candidate, bound: tryBindRootWorkspace(candidate) }))),
  )
  const winners = attempts.filter(attempt => attempt.bound)
  assert.equal(winners.length, 1, 'concurrent contenders must produce one winner')
  assert.equal(tryGetRootWorkspace(), winners[0].candidate)

  assert.equal(tryBindRootWorkspace('/tmp/second-root-workspace'), false)
  assert.equal(tryGetRootWorkspace(), winners[0].candidate, 'a later plugin cannot overwrite the root')

  assert.equal(firstBoundRootWorkspace([null, '', '  ', '/tmp/later']), '/tmp/later')
  assert.equal(selectContinuationDirectory('/tmp/live', true, '/tmp/root'), '/tmp/live')
  assert.equal(selectContinuationDirectory('/tmp/deleted', false, '/tmp/root'), '/tmp/root')
  assert.equal(selectContinuationDirectory('/tmp/deleted', false, null), null)

  assert.equal('setRootWorkspace' in sharedStateSurface, false)
  assert.equal('clearRootWorkspace' in sharedStateSurface, false)
})

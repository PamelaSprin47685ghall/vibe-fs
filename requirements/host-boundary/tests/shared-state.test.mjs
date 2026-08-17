import assert from 'node:assert/strict'
import test from 'node:test'
import {
  putSessionParent,
  getSessionParent,
  clearSessionParents,
  addReviewGuardNudge,
  hasReviewGuardNudge,
  clearReviewGuardNudges,
  setRootWorkspace,
  clearRootWorkspace,
  tryGetRootWorkspace,
} from '../../../dist/OpenCode/Host/SharedStateSurface.js'

// HOST-BOUNDARY-010 / HOST-012: SessionParents / VerdictSessions /
// SessionDirectories / ReviewGuardNudges are module-level shared singletons
// (OpenCode/Host/SharedState.fs). All plugin instances — root and worktree —
// read/write the same state through the SharedStateSurface boundary; the
// physical Map/Set/atom singletons stay opaque behind narrow put/get/clear
// operations. RootWorkspace is a mutable atom set by whichever plugin instance
// boots first. The behavioral proof: a mutation made through one import is
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

test('WHAT[HOST-BOUNDARY-010] SHARED_root_workspace_atom_round_trips_and_restores', () => {
  // RootWorkspace is a mutable atom: the worktree plugin pins its blogger
  // companion here so the system prompt survives the manager worktree release
  // at publish. Round-trip set → read → clear → read must hold.
  setRootWorkspace('/tmp/shared-root-workspace')
  assert.equal(tryGetRootWorkspace(), '/tmp/shared-root-workspace')
  clearRootWorkspace()
  assert.equal(tryGetRootWorkspace(), null)
})

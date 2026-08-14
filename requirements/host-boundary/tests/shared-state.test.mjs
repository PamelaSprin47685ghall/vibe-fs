// Split from tests/unit/host/shared-state.test.mjs (cutover Wave 2a);
// owner: host-boundary. HOST-BOUNDARY-010（HOST-012 共享面）：跨实例 module-level
// 单例字典跨 importer 共享；root workspace atom 可 round-trip/restore。
// REVIEW-010 PendingSeal shape 断言归 review-assurance。

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  RootWorkspace,
  SessionDirectories,
  SessionParents,
  VerdictSessions,
} = await import('../../../dist/Infrastructure/OpenCode/Host/SharedState.js')

test('SHARED_dictionaries_are_live_singletons_shared_across_importers', async () => {
  // A second import of the same module must observe the first import's writes:
  // fork→verdict causality depends on the cross-instance single reference.
  const again = await import('../../../dist/Infrastructure/OpenCode/Host/SharedState.js')
  const key = 'shared-test-ses-parents'
  try {
    SessionParents.set(key, 'parent-of-' + key)
    assert.equal(again.SessionParents.get(key), 'parent-of-' + key)

    VerdictSessions.add('shared-test-verdict')
    assert.equal(again.VerdictSessions.has('shared-test-verdict'), true)

    SessionDirectories.set('shared-test-dir', '/tmp/x')
    assert.equal(again.SessionDirectories.get('shared-test-dir'), '/tmp/x')
  } finally {
    SessionParents.delete(key)
    VerdictSessions.delete('shared-test-verdict')
    SessionDirectories.delete('shared-test-dir')
  }
})

test('SHARED_root_workspace_atom_round_trips_and_restores', () => {
  const before = RootWorkspace()
  try {
    RootWorkspace('/tmp/shared-root-workspace')
    assert.equal(RootWorkspace(), '/tmp/shared-root-workspace')
    RootWorkspace(undefined)
    assert.equal(RootWorkspace(), undefined)
  } finally {
    RootWorkspace(before)
  }
})

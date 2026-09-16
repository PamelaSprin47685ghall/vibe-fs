// requirements/knowledge-reuse/tests/014.test.mjs
//
// Laws: KNOWLEDGE-REUSE-003, KNOWLEDGE-REUSE-004, KNOWLEDGE-REUSE-005, KNOWLEDGE-REUSE-006, KNOWLEDGE-REUSE-010, KNOWLEDGE-REUSE-014
// Scenarios T17-T26: Substantive access collection, dual baselines, diff-driven fetch,
// Bookkeeper diff refresh, Fission convergence, and budget truncation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as index from '../../../dist/Repository/Knowledge/Casebook/IndexSurface.js'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'

test('WHAT[KNOWLEDGE-REUSE-003] T17_substantive_access_collects_read_create_edit_delete_move_and_ignores_grep_glob', async () => {
  // Substantive access collects:
  // - successful read: entire file associated (path recorded)
  // - successful create: new path
  // - successful edit: target path
  // - successful delete: original path, ending as Missing
  // - successful move: both old and new paths
  // - grep/glob/ls/mentions: NOT collected
  assert.equal(typeof casebook.recordSubstantiveAccess, 'function', 'casebook must export recordSubstantiveAccess')
  const access = casebook.createAccessTracker()
  access.recordRead('src/lib.fs', 'hash1')
  access.recordCreate('src/new.fs')
  access.recordEdit('src/edit.fs')
  access.recordDelete('src/deleted.fs')
  access.recordMove('src/old.fs', 'src/renamed.fs')
  // grep & glob should be ignored
  access.recordGrep('TODO', 'src/lib.fs')
  access.recordGlob('src/**/*.fs')

  const paths = access.getRelatedPaths()
  assert.deepEqual(paths.sort(), [
    'src/deleted.fs',
    'src/edit.fs',
    'src/lib.fs',
    'src/new.fs',
    'src/old.fs',
    'src/renamed.fs',
  ].sort())
})

test('WHAT[KNOWLEDGE-REUSE-003] T18_grep_glob_ls_and_prose_mentions_do_not_enter_case_related_paths', () => {
  assert.equal(typeof casebook.isSubstantiveTool, 'function', 'casebook must export isSubstantiveTool predicate')
  assert.equal(casebook.isSubstantiveTool('read'), true)
  assert.equal(casebook.isSubstantiveTool('write'), true)
  assert.equal(casebook.isSubstantiveTool('edit'), true)
  assert.equal(casebook.isSubstantiveTool('mv'), true)
  assert.equal(casebook.isSubstantiveTool('rm'), true)
  assert.equal(casebook.isSubstantiveTool('grep'), false)
  assert.equal(casebook.isSubstantiveTool('glob'), false)
  assert.equal(casebook.isSubstantiveTool('ls'), false)
})

test('WHAT[KNOWLEDGE-REUSE-003] T19_failed_or_uncommitted_mutations_do_not_record_modification_access', () => {
  assert.equal(typeof casebook.createAccessTracker, 'function')
  const tracker = casebook.createAccessTracker()
  tracker.recordRead('src/a.fs', 'h1')
  tracker.recordAttemptedMutation('src/b.fs', false) // uncommitted/failed
  const paths = tracker.getRelatedPaths()
  assert.deepEqual(paths, ['src/a.fs'])
})

test('WHAT[KNOWLEDGE-REUSE-004] T20_completion_boundary_freezes_baseline_B_and_tracks_diff_B_to_C', async () => {
  // Read A -> change to B -> end trajectory -> external changes to C
  // Initial completionFileState = B; maintenanceFileState = B; diff = B -> C
  assert.equal(typeof casebook.freezeCompletionState, 'function', 'casebook must export freezeCompletionState')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-kr-t20-'))
  try {
    writeFileSync(join(dir, 'a.txt'), 'version-B', 'utf8')
    const baseline = await casebook.freezeCompletionState(dir, ['a.txt'])
    assert.equal(baseline.get('a.txt')?.kind, 'Present')
    assert.equal(baseline.get('a.txt')?.contentHash, casebook.contentHash('version-B'))

    // External change to C
    writeFileSync(join(dir, 'a.txt'), 'version-C', 'utf8')
    const diff = await casebook.computeMaintenanceDiff(dir, baseline)
    assert.equal(diff.hasDiff, true)
    assert.match(diff.diffSummary, /version-B.*version-C/s)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-005] T23_T24_fetch_uses_diff_and_advances_maintenance_baseline_without_replay_loops', async () => {
  assert.equal(typeof casebook.refreshWithDiff, 'function', 'casebook must export refreshWithDiff')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-kr-t23-'))
  const store = eventStore.create(dir, 'kr-t23-writer')
  try {
    writeFileSync(join(dir, 'a.txt'), 'version-B', 'utf8')
    const initialCase = {
      identity: 'eng-invocation-1',
      sourceTrace: 'trace-1',
      q: 'Task Q',
      a: 'Answer A',
      relatedPaths: ['a.txt'],
      completionFileState: 'state-ref-B',
      maintenanceFileState: 'state-ref-B',
      accessOrder: 0,
    }
    await casebook.archiveCase(store, initialCase)

    // External change B -> C
    writeFileSync(join(dir, 'a.txt'), 'version-C', 'utf8')
    const diff = 'diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n-version-B\n+version-C\n'
    const refreshResult = await casebook.refreshWithDiff(store, 'eng-invocation-1', diff, 'state-ref-C', {
      q: 'Task Q',
      a: 'Updated Answer A for C',
    })

    assert.equal(refreshResult.ok, true)
    const fetched = await casebook.fetchCaseByIdentity(store, 'eng-invocation-1')
    assert.equal(fetched.a, 'Updated Answer A for C')
    assert.equal(fetched.completionFileState, 'state-ref-B', 'completionFileState must NEVER be rewritten')
    assert.equal(fetched.maintenanceFileState, 'state-ref-C', 'maintenanceFileState advances to C')
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-006] T23_bookkeeper_refresh_receives_diff_only_and_has_no_repo_investigation_rights', async () => {
  // Bookkeeper CaseRefresh receives: old case + diff. No repo read/glob/grep.
  assert.equal(typeof bookkeeper.createRefreshPrompt, 'function', 'bookkeeper must format refresh prompt with diff only')
  const prompt = bookkeeper.createRefreshPrompt({
    q: 'Old Q',
    a: 'Old A',
    relatedPaths: ['a.txt'],
    diff: '-B\n+C',
  })
  assert.match(prompt, /CaseRefresh/)
  assert.match(prompt, /Old Q/)
  assert.match(prompt, /Old A/)
  assert.match(prompt, /diff/)
  assert.doesNotMatch(prompt, /read|glob|grep|query-shell/i)
})

test('WHAT[KNOWLEDGE-REUSE-010] T21_multiple_resumes_in_same_session_create_independent_case_records', async () => {
  assert.equal(typeof casebook.caseIdentityForInvocation, 'function', 'case identity must be scoped to invocation, not physical session')
  const session = 'session-123'
  const id1 = casebook.caseIdentityForInvocation(session, 'inv-1')
  const id2 = casebook.caseIdentityForInvocation(session, 'inv-2')
  assert.notEqual(id1, id2, 'multiple resumes must yield distinct CaseId/identities')
})

test('WHAT[KNOWLEDGE-REUSE-010] T22_engineer_fission_merges_substantive_access_and_keyed_traces_for_single_finalize', async () => {
  assert.equal(typeof casebook.mergeFissionSubstantiveAccess, 'function', 'casebook must support merging fission lane access')
  const preFission = ['a.txt']
  const lane1 = ['b.txt', 'a.txt']
  const lane2 = ['c.txt']
  const merged = casebook.mergeFissionSubstantiveAccess(preFission, [lane1, lane2])
  assert.deepEqual(merged.sort(), ['a.txt', 'b.txt', 'c.txt'])
})

test('WHAT[KNOWLEDGE-REUSE-014] T26_large_traces_and_diffs_truncate_with_explicit_truncation_notice', () => {
  assert.equal(typeof casebook.truncateDiffForBudget, 'function', 'casebook must export truncateDiffForBudget')
  const largeDiff = 'x'.repeat(20000)
  const truncated = casebook.truncateDiffForBudget(largeDiff, 1000)
  assert.ok(truncated.text.length <= 1000)
  assert.equal(truncated.isTruncated, true)
  assert.match(truncated.notice, /truncated|截断/i)
})

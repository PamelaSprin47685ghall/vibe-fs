// SQAS: StudentQaStore — PERSIST-011 private QA authority file over real fs.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, readFileSync, existsSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { execFileSync } from 'node:child_process'

import { sessionId, logicalRunId, resultOf } from '../support/domain.mjs'

const { StudentQaStore_Create_Z721C83C5, StudentQaStore__Path_Z145977CC, StudentQaStore__Read_Z145977CC, StudentQaStore__Append_Z126B0D71, StudentQaStore__Delete_Z145977CC, StudentQaStore__get_GitDirectory } =
  await import('../../../dist/Infrastructure/OpenCode/Host/StudentQaStore.js')

const SESSION = sessionId('ses_stu_1')
const RUN = logicalRunId('run_qa_1')

const withGitDir = (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-sqas-'))
  const gitDir = join(dir, 'repo')
  execFileSync('git', ['init', '-q', gitDir])
  try {
    fn(dir, gitDir)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}

const create = (gitDir) => {
  const r = resultOf(StudentQaStore_Create_Z721C83C5(gitDir))
  assert.equal(r.ok, true, r.ok ? '' : r.error)
  return r.value
}

test('SQAS_create_rejects_non_git_workspace', () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-sqas-nogit-'))
  try {
    const r = resultOf(StudentQaStore_Create_Z721C83C5(dir))
    assert.equal(r.ok, false)
    assert.match(r.error, /Git private directory/)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('SQAS_path_lives_under_git_private_wanxiangshu_student', () => {
  withGitDir((_dir, gitDir) => {
    const store = create(gitDir)
    const p = resultOf(StudentQaStore__Path_Z145977CC(store, SESSION, RUN))
    assert.equal(p.ok, true)
    assert.equal(p.value, join(gitDir, '.git', 'wanxiangshu', 'student', 'ses_stu_1', 'run_qa_1', 'QA.md'))
    assert.ok(p.value.startsWith(join(gitDir, '.git')), 'QA must never appear in the worktree')
  })
})

test('SQAS_path_rejects_unsafe_session_and_run_segments', () => {
  withGitDir((_dir, gitDir) => {
    const store = create(gitDir)
    for (const bad of ['../x', 'a/b', '', '  ', 'a b', 'a;rm']) {
      const r = resultOf(StudentQaStore__Path_Z145977CC(store, sessionId(bad), RUN))
      assert.equal(r.ok, false, `session ${JSON.stringify(bad)} must be refused`)
      assert.match(r.error, /Unsafe SessionId/)
      const r2 = resultOf(StudentQaStore__Path_Z145977CC(store, SESSION, logicalRunId(bad)))
      assert.equal(r2.ok, false, `run ${JSON.stringify(bad)} must be refused`)
      assert.match(r2.error, /Unsafe LogicalRunId/)
    }
  })
})

test('SQAS_read_missing_returns_empty_string', () => {
  withGitDir((_dir, gitDir) => {
    const store = create(gitDir)
    const r = resultOf(StudentQaStore__Read_Z145977CC(store, SESSION, RUN))
    assert.deepEqual(r, { ok: true, value: '' })
  })
})

test('SQAS_append_writes_idempotent_private_file_and_read_round_trips_utf8', () => {
  withGitDir((_dir, gitDir) => {
    const store = create(gitDir)
    const entry = 'Q: 什么是结对编程？\nA: 结对编程是一种协作开发方式。\n'
    const appended = resultOf(StudentQaStore__Append_Z126B0D71(store, SESSION, RUN, entry))
    assert.equal(appended.ok, true, appended.ok ? '' : appended.error)
    assert.equal(appended.value, join(gitDir, '.git', 'wanxiangshu', 'student', 'ses_stu_1', 'run_qa_1', 'QA.md'))

    const file = readFileSync(appended.value, 'utf8')
    assert.equal(file, entry, 'durable bytes must be exactly the entry')

    const r = resultOf(StudentQaStore__Read_Z145977CC(store, SESSION, RUN))
    assert.equal(r.ok, true)
    assert.equal(r.value, entry)

    // Idempotent tail: appending the same entry again changes nothing.
    const again = resultOf(StudentQaStore__Append_Z126B0D71(store, SESSION, RUN, entry))
    assert.equal(again.ok, true)
    assert.equal(readFileSync(again.value, 'utf8'), entry, 'duplicate append must be a no-op')
  })
})

test('SQAS_append_accumulates_distinct_entries', () => {
  withGitDir((_dir, gitDir) => {
    const store = create(gitDir)
    const a = resultOf(StudentQaStore__Append_Z126B0D71(store, SESSION, RUN, 'first\n'))
    const b = resultOf(StudentQaStore__Append_Z126B0D71(store, SESSION, RUN, 'second\n'))
    assert.equal(a.ok, true)
    assert.equal(b.ok, true)
    // appendIdempotentTail separates entries with a blank line.
    assert.equal(readFileSync(b.value, 'utf8'), 'first\n\n\nsecond\n')
  })
})

test('SQAS_append_rejects_unsafe_ids_without_touching_fs', () => {
  withGitDir((_dir, gitDir) => {
    const store = create(gitDir)
    const r = resultOf(StudentQaStore__Append_Z126B0D71(store, sessionId('../evil'), RUN, 'x'))
    assert.equal(r.ok, false)
    assert.match(r.error, /Unsafe SessionId/)
    assert.equal(existsSync(join(gitDir, '.git', 'wanxiangshu')), false, 'no directories may be created for unsafe ids')
  })
})

test('SQAS_delete_removes_file_and_run_directory', () => {
  withGitDir((_dir, gitDir) => {
    const store = create(gitDir)
    const appended = resultOf(StudentQaStore__Append_Z126B0D71(store, SESSION, RUN, 'x\n'))
    assert.equal(appended.ok, true)
    assert.ok(existsSync(appended.value))

    const del = resultOf(StudentQaStore__Delete_Z145977CC(store, SESSION, RUN))
    assert.equal(del.ok, true, del.ok ? '' : del.error)
    assert.equal(existsSync(appended.value), false)
    assert.equal(existsSync(join(gitDir, '.git', 'wanxiangshu', 'student', 'ses_stu_1', 'run_qa_1')), false)
  })
})

test('SQAS_delete_on_missing_file_is_a_noop_success', () => {
  withGitDir((_dir, gitDir) => {
    const store = create(gitDir)
    const r = resultOf(StudentQaStore__Delete_Z145977CC(store, SESSION, RUN))
    assert.deepEqual(r, { ok: true, value: undefined })
  })
})

test('SQAS_GitDirectory_exposes_the_resolved_git_dir', () => {
  withGitDir((_dir, gitDir) => {
    const store = create(gitDir)
    assert.equal(StudentQaStore__get_GitDirectory(store), join(gitDir, '.git'))
  })
})

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { chmodSync, existsSync, mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  StudentQaStore_Create_Z721C83C5 as createStore,
  StudentQaStore__Append_Z126B0D71 as append,
  StudentQaStore__Delete_Z145977CC as remove,
  StudentQaStore__Path_Z145977CC as pathFor,
  StudentQaStore__Read_Z145977CC as read,
} from '../../../dist/Infrastructure/OpenCode/Host/StudentQaStore.js'
import {
  LogicalRunIdModule_create as logicalRun,
  SessionIdModule_create as session,
} from '../../../dist/Kernel/Identity.js'

const ok = (result) => {
  assert.equal(result.tag, 0, result.fields?.[0])
  return result.fields[0]
}

test('PERSIST_011_QA_is_private_atomic_verbatim_tail_deduped_and_deleted', () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-qa-'))
  try {
    execFileSync('git', ['init', '--quiet', repo])
    const store = ok(createStore(repo))
    const sid = session('ses_student_1')
    const run = logicalRun('run_1')

    const first = '用户原始请求\n保留这一行\n'
    const question = '为什么会这样？'
    const answer = '因为 α 与 β。'

    const path = ok(append(store, sid, run, first))
    const gitDirectory = execFileSync('git', ['-C', repo, 'rev-parse', '--absolute-git-dir'], { encoding: 'utf8' }).trim()
    assert.equal(path.startsWith(gitDirectory), true, 'QA must live under Git private storage')
    assert.equal(path.includes(join(gitDirectory, 'wanxiangshu', 'student')), true)
    assert.equal(readFileSync(path, 'utf8'), first)
    assert.equal(statSync(path).mode & 0o777, 0o600)
    assert.equal(statSync(join(path, '..')).mode & 0o777, 0o700)

    ok(append(store, sid, run, question))
    ok(append(store, sid, run, answer))
    ok(append(store, sid, run, answer))
    assert.equal(ok(read(store, sid, run)), `${first}\n\n${question}\n\n${answer}`)

    ok(remove(store, sid, run))
    assert.equal(existsSync(path), false)
    ok(remove(store, sid, run)) // absent is idempotent success
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

test('PERSIST_011_invalid_UTF8_fails_closed_and_preserves_the_bad_file', () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-qa-bad-'))
  try {
    execFileSync('git', ['init', '--quiet', repo])
    const store = ok(createStore(repo))
    const sid = session('ses_student_bad')
    const run = logicalRun('run_bad')
    const path = ok(append(store, sid, run, 'valid'))

    writeFileSync(path, Buffer.from([0xff, 0xfe]))
    chmodSync(path, 0o600)
    const before = readFileSync(path)

    assert.equal(read(store, sid, run).tag, 1)
    assert.equal(append(store, sid, run, 'must-not-append').tag, 1)
    assert.deepEqual(readFileSync(path), before)
    assert.equal(ok(pathFor(store, sid, run)), path)
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

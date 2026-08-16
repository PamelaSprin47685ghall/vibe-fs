// JS-012/015: transaction modules never enumerate EventStore history.
// A crash leaves Prepared as audit evidence; the next process never mutates files to hide the broken tool.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { randomUUID } from 'node:crypto'

import { create as createEventStore, dispose as disposeEventStore } from '../../../dist/Persistence/EventStore/Surface.js'
import {
  appendPrepared,
  appendCommitted,
  pending,
} from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-txstore-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const localStore = (commonDir) => {
  const owned = commonDir ?? mkdtempSync(join(tmpdir(), 'wxs-txstore-events-'))
  const gitCommonDir = join(owned, '.git')
  mkdirSync(gitCommonDir, { recursive: true })
  const handle = createEventStore(gitCommonDir, randomUUID().replaceAll('-', ''))
  return { handle, owned, close: () => { disposeEventStore(handle); if (!commonDir) rmSync(owned, { recursive: true, force: true }) } }
}

const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result
}

const mutation = (path, originalText, newText) => ({
  path,
  originalText,
  newText,
})

const prepared = (id, root, mutations) => ({
  transactionId: id,
  workspaceRoot: root,
  mutations,
})

test('WHAT[REPOSITORY-PROGRAMMING-012] JS012_prepare_then_commit_updates_only_integrator_Current', async () => {
  const local = localStore()
  try {
    const p = prepared('tx-1', '/ws', [mutation('a.txt', 'old', 'new')])
    unwrap(await appendPrepared(local.handle, p))
    assert.equal(pending(local.handle).length, 1)

    unwrap(await appendCommitted(local.handle, 'tx-1'))
    assert.deepEqual(pending(local.handle), [])
  } finally {
    local.close()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_prepared_without_committed_is_interrupted_tool_evidence', async () => {
  const local = localStore()
  try {
    const p = prepared('tx-2', '/ws', [mutation('a.txt', 'old', 'new'), mutation('b.txt', null, 'fresh')])
    unwrap(await appendPrepared(local.handle, p))
    const waiting = pending(local.handle)
    assert.equal(waiting.length, 1)
    assert.equal(waiting[0].workspaceRoot, '/ws')
    assert.equal(waiting[0].mutations.length, 2)
  } finally {
    local.close()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_reopening_store_never_undoes_an_interrupted_tool', async () => {
  const { dir, cleanup } = sandbox()
  const common = mkdtempSync(join(tmpdir(), 'wxs-txstore-events-'))
  try {
    writeFileSync(join(dir, 'a.txt'), 'new', 'utf8')
    const first = localStore(common)
    try {
      unwrap(await appendPrepared(first.handle, prepared('tx-3', dir, [mutation('a.txt', 'old', 'new')])))
    } finally {
      first.close()
    }

    const reopened = localStore(common)
    try {
      assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'new')
      assert.equal(pending(reopened.handle).length, 1)
    } finally {
      reopened.close()
    }
  } finally {
    rmSync(common, { recursive: true, force: true })
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_store_source_has_no_manual_history_reader', async () => {
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Repository/Programming/Js/TransactionStore.fs', import.meta.url), 'utf8')
  assert.doesNotMatch(source, /loadEvents|scanUncommitted|OpenSnapshot|readStreams/)
  assert.doesNotMatch(source, /recoverCurrent|undoIfMatches/)
})

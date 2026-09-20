// requirements/knowledge-reuse/tests/016.test.mjs
//
// Laws: knowledge-reuse-016

import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'

test('WHAT[knowledge-reuse-016] present_entry_maintains_immutable_content_addressing_and_storage_reuse', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-kr-016-present-'))
  const store = eventStore.create(dir, 'kr-016-writer-present')
  try {
    const filePath = join(dir, 'example.txt')
    writeFileSync(filePath, 'hello immutable baseline', 'utf8')

    // First freeze
    const baseline1Json = await casebook.freezeCompletionState(store, dir, ['example.txt'])
    assert.equal(typeof baseline1Json, 'string')
    const parsed1 = JSON.parse(baseline1Json)
    const entry1 = parsed1['example.txt']

    assert.equal(entry1.kind, 'Present')
    assert.ok(entry1.payloadRef)
    assert.equal(entry1.sha256, casebook.contentHash('hello immutable baseline'))
    assert.equal(entry1.contentHash, entry1.sha256)

    // Verify stored payload in EventStore
    const payloadBytes1 = await eventStore.readPayload(store, entry1.payloadRef)
    assert.ok(payloadBytes1)
    assert.equal(new TextDecoder().decode(payloadBytes1), 'hello immutable baseline')

    // Second freeze on unchanged file: must reuse identical payloadRef and sha256
    const baseline2Json = await casebook.freezeCompletionState(store, dir, ['example.txt'])
    assert.equal(typeof baseline2Json, 'string')
    const parsed2 = JSON.parse(baseline2Json)
    const entry2 = parsed2['example.txt']

    assert.equal(entry2.kind, 'Present')
    assert.equal(entry2.payloadRef, entry1.payloadRef, 'must reuse identical payload reference for unchanged content')
    assert.equal(entry2.sha256, entry1.sha256, 'hash fingerprint must be identical')

    // Modify file: freeze must yield a different payloadRef and updated sha256
    writeFileSync(filePath, 'hello changed content', 'utf8')
    const baseline3Json = await casebook.freezeCompletionState(store, dir, ['example.txt'])
    const parsed3 = JSON.parse(baseline3Json)
    const entry3 = parsed3['example.txt']

    assert.equal(entry3.kind, 'Present')
    assert.notEqual(entry3.payloadRef, entry1.payloadRef, 'modified content must generate a new payloadRef')
    assert.equal(entry3.sha256, casebook.contentHash('hello changed content'))
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[knowledge-reuse-016] missing_entry_records_nonexistent_file_as_missing_and_drives_diff', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-kr-016-missing-'))
  const store = eventStore.create(dir, 'kr-016-writer-missing')
  try {
    const nonExistentPath = 'never-created.fs'

    const baselineJson = await casebook.freezeCompletionState(store, dir, [nonExistentPath])
    assert.equal(typeof baselineJson, 'string')
    const parsed = JSON.parse(baselineJson)

    assert.equal(parsed[nonExistentPath]?.kind, 'Missing', 'nonexistent file must be classified as Missing')
    assert.equal(parsed[nonExistentPath]?.payloadRef, undefined, 'Missing entry must not have a payloadRef')

    // Computing diff against Missing baseline when file remains missing -> no diff
    const diffBefore = await casebook.computeMaintenanceDiff(dir, baselineJson)
    assert.equal(diffBefore.hasDiff, false)

    // When the file is subsequently created on disk, diff must identify it as an added file (new file)
    writeFileSync(join(dir, nonExistentPath), 'let created = true', 'utf8')
    const diffAfter = await casebook.computeMaintenanceDiff(dir, baselineJson)
    assert.equal(diffAfter.hasDiff, true)
    assert.match(diffAfter.diffSummary, /new file/i)
    assert.match(diffAfter.diffSummary, /let created = true/)
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[knowledge-reuse-016] io_failure_on_directory_or_unreadable_path_fails_closed_without_recording_missing_or_present', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-kr-016-iofail-'))
  const store = eventStore.create(dir, 'kr-016-writer-iofail')
  try {
    // Create a directory where a file path is expected
    const subDirName = 'folder-as-file'
    mkdirSync(join(dir, subDirName), { recursive: true })

    // freezeCompletionState must fail closed on EISDIR / directory read error
    const result = await casebook.freezeCompletionState(store, dir, [subDirName])

    // Result must NOT be a baseline JSON string mapping folder-as-file to Missing or Present
    assert.notEqual(typeof result, 'string', 'I/O failure must not return a successful baseline JSON')
    assert.equal(result.ok, false, 'freeze completion state must fail with ok: false')
    assert.ok(result.error, 'must report explicit error message')
    assert.match(String(result.error), /failed to read file/i)
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

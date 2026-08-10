// tests/integration/persist/object-identity.test.mjs
// The ODB write path must produce byte-identical Git objects.
//
// `GitObjectDatabase` writes loose objects itself (sha1 + zlib + objects/xx/yyyy) instead of
// spawning `git hash-object` / `git mktree` per object — measured 24 spawns / ~60ms per
// single-event append against 2 spawns / ~7.5ms, with the spawns blocking the Host event loop.
// The whole safety of that swap is one claim: OUR oid IS GIT'S OID, and git can read what we
// wrote. Both halves are pinned here against the real binary, because a comment asserting
// "this is the documented format" is not evidence.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { isSome, listItems, toList } from '../../unit/support/domain.mjs'

const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const Odb = await import('../../../dist/Infrastructure/Persist/GitObjectDatabase.js')

const oidText = (oid) => Persist.GitObjectIdModule_value(oid)
const gitObjectId = (text) => Persist.GitObjectIdModule_create(text)

const withRepo = (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'odb-identity-'))
  try {
    execFileSync('git', ['init', '--quiet', dir], { encoding: 'utf8' })
    const git = (args, options = {}) =>
      execFileSync('git', ['-C', dir, ...args], { encoding: 'utf8', ...options }).trim()
    const objects = git(['rev-parse', '--git-path', 'objects'])
    fn({ dir, git, objects: objects.startsWith('/') ? objects : join(dir, objects) })
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}

const BODIES = [
  '',
  'a',
  'plain ascii line\n',
  '{"event_id":"' + 'a'.repeat(32) + '","payload":{"k":1}}\n',
  '多字节 UTF-8 内容\n',
  Array.from({ length: 5000 }, (_, i) => `line ${i}`).join('\n'),
]

test('ODB blob oid equals git hash-object, and git can read the object', () => {
  withRepo(({ git, objects, dir }) => {
    for (const body of BODIES) {
      const ours = Odb.writeBlob(objects, Buffer.from(body, 'utf8'))

      const input = join(dir, 'input.bin')
      writeFileSync(input, body)
      const theirs = git(['hash-object', '--', input])

      assert.equal(ours, theirs, `blob oid must match git for ${JSON.stringify(body.slice(0, 24))}`)
      assert.equal(git(['cat-file', '-t', ours]), 'blob', 'git must recognise our object as a blob')
      assert.equal(
        execFileSync('git', ['-C', dir, 'cat-file', 'blob', ours], { encoding: 'utf8' }),
        body,
        'git must read back exactly the bytes we wrote',
      )
    }
  })
})

test('ODB tree oid equals git mktree, including nested trees and name ordering', () => {
  withRepo(({ git, objects }) => {
    const blobA = Odb.writeBlob(objects, Buffer.from('A\n', 'utf8'))
    const blobB = Odb.writeBlob(objects, Buffer.from('B\n', 'utf8'))

    const entry = (mode, name, oid) => new Persist.TreeEntry(mode, name, gitObjectId(oid))
    const leaf = [entry('100644', 'b.jsonl', blobB), entry('100644', 'a.jsonl', blobA)]
    const leafOid = Odb.writeTree(objects, toList(leaf))

    // `mktree` reads the same records from stdin; sorting is ours to get right.
    const mktree = (rows) =>
      execFileSync('git', ['-C', git(['rev-parse', '--show-toplevel']) || '.', 'mktree'], {
        encoding: 'utf8',
        input: rows.join('\n') + '\n',
      }).trim()

    assert.equal(
      leafOid,
      mktree([`100644 blob ${blobA}\ta.jsonl`, `100644 blob ${blobB}\tb.jsonl`]),
      'flat tree oid must match git mktree',
    )

    // A directory entry sorts as `name/`, which is why `events` must precede `events.meta`.
    const nested = [
      entry('040000', 'events', leafOid),
      entry('100644', 'events.meta', blobA),
    ]
    const nestedOid = Odb.writeTree(objects, toList(nested))
    assert.equal(
      nestedOid,
      mktree([`040000 tree ${leafOid}\tevents`, `100644 blob ${blobA}\tevents.meta`]),
      'nested tree oid must match git mktree',
    )
    assert.equal(git(['cat-file', '-t', nestedOid]), 'tree', 'git must recognise our tree')
    assert.match(
      execFileSync('git', ['-C', git(['rev-parse', '--show-toplevel']), 'ls-tree', nestedOid], {
        encoding: 'utf8',
      }),
      /events\.meta/,
      'git must list the entries we wrote',
    )
  })
})

test('ODB reads back its own trees; a packed object is reported absent, not guessed', () => {
  withRepo(({ git, objects }) => {
    const blob = Odb.writeBlob(objects, Buffer.from('payload\n', 'utf8'))
    const entry = new Persist.TreeEntry('100644', 'x.jsonl', gitObjectId(blob))
    const treeOid = Odb.writeTree(objects, toList([entry]))

    const read = Odb.tryReadTree(objects, treeOid)
    assert.equal(isSome(read), true, 'loose tree must be readable in-process')
    const entries = listItems(read)
    assert.equal(entries.length, 1)
    assert.equal(entries[0].Name, 'x.jsonl')
    assert.equal(oidText(entries[0].Oid), blob)

    const body = Odb.tryReadObject(objects, blob)
    assert.equal(isSome(body), true, 'loose blob must be readable in-process')
    assert.equal(Buffer.from(body).toString('utf8'), 'payload\n')

    // Absent is None — never a fabricated empty object; the store falls back to the CLI.
    assert.equal(Odb.tryReadObject(objects, '0'.repeat(40)), undefined)
    assert.equal(git(['cat-file', '-t', treeOid]), 'tree')
  })
})

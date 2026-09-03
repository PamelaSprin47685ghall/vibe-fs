import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, unlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as retention from '../../../dist/Persistence/EventStore/RetentionSurface.js'

const git = (repo, args, options = {}) => execFileSync('git', ['-C', repo, ...args], options)

const invalidUtf8Line = (line) => {
  const bytes = Buffer.from(line)
  const continuation = bytes.indexOf(0xa9)
  assert.notEqual(continuation, -1)
  bytes[continuation] = 0x20
  return bytes
}

test('WHAT[DURABLE-CONVERGENCE-003] remote writer bytes reject invalid UTF-8 before retained-union merge', async () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-remote-invalid-utf8-'))
  git(repo, ['init', '-q'])
  const commonDir = join(repo, '.git')
  const eventsDir = join(commonDir, 'wanxiang', 'events')
  const writerPath = join(eventsDir, 'writer-remote.ndjson')
  const nowMs = Date.now()
  const line = JSON.stringify({
    event_id: '757466382d72656d6f74652d7772697465722d6964',
    event_type: 'JobRequested',
    parents: [],
    payload: { text: 'é' },
    payload_refs: [],
    stream_id: 'proof/remote-utf8',
  }) + '\n'

  try {
    mkdirSync(eventsDir, { recursive: true })
    writeFileSync(writerPath, line)
    const initial = await retention.syncAt(repo, commonDir, null, nowMs)
    assert.equal(initial.ok, true, JSON.stringify(initial))

    const originalWriterEntry = git(repo, ['ls-tree', `${initial.root}:writers`], { encoding: 'utf8' }).trim()
    const originalWriterOid = originalWriterEntry.split(/\s+/)[2]
    const invalidWriterOid = git(repo, ['hash-object', '-w', '--stdin'], {
      encoding: 'utf8',
      input: invalidUtf8Line(line),
    }).trim()
    const invalidWriterTree = git(repo, ['mktree'], {
      encoding: 'utf8',
      input: originalWriterEntry.replace(originalWriterOid, invalidWriterOid) + '\n',
    }).trim()

    const manifest = git(repo, ['show', `${initial.root}:writer-manifest`], { encoding: 'utf8' })
    const invalidManifestOid = git(repo, ['hash-object', '-w', '--stdin'], {
      encoding: 'utf8',
      input: manifest.replace(originalWriterOid, invalidWriterOid),
    }).trim()
    const invalidRootEntries = git(repo, ['ls-tree', initial.root], { encoding: 'utf8' })
      .trim()
      .split('\n')
      .map((entry) => {
        if (entry.endsWith('\twriters')) return entry.replace(/tree [0-9a-f]{40}/, `tree ${invalidWriterTree}`)
        if (entry.endsWith('\twriter-manifest')) return entry.replace(/blob [0-9a-f]{40}/, `blob ${invalidManifestOid}`)
        return entry
      })
      .join('\n') + '\n'
    const invalidRoot = git(repo, ['mktree'], { encoding: 'utf8', input: invalidRootEntries }).trim()

    unlinkSync(writerPath)
    const result = await retention.syncAt(repo, commonDir, invalidRoot, nowMs + 1)

    assert.equal(result.ok, false)
    assert.match(String(result.error), /not valid UTF-8/)
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

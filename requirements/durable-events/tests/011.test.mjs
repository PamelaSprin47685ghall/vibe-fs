import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdtempSync, readdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
const hexId = (n) => n.toString(16).padStart(40, '0')
const event = (id, n, parents = []) => ({
  id,
  stream: 'proof/local',
  type: 'JobRequested',
  parents,
  payload: { n },
  payloadRefs: [],
})

test('WHAT[durable-events-011] runtime append creates zero Git objects and writes only under .Herdchestra', async (t) => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-de011-'))
  t.after(() => {
    rmSync(repo, { recursive: true, force: true })
  })

  execFileSync('git', ['init', '--quiet', repo])
  const gitDir = join(repo, '.git')
  const stateRoot = join(repo, '.Herdchestra')

  await t.test('WHAT[durable-events-011] initial git ODB is empty', () => {
    const objectsEntries = readdirSync(join(gitDir, 'objects')).filter(e => e !== 'info' && e !== 'pack')
    assert.equal(objectsEntries.length, 0, 'initial git ODB must be empty')
  })

  const store = eventStore.create(stateRoot, 'writer-de011-test')
  const e1 = event(hexId(1), 101)
  const e2 = event(hexId(2), 102, [hexId(1)])
  await eventStore.append(store, [e1, e2])

  await t.test('WHAT[durable-events-011] runtime append creates zero loose Git objects', () => {
    const objectsEntries = readdirSync(join(gitDir, 'objects')).filter(e => e !== 'info' && e !== 'pack')
    assert.equal(objectsEntries.length, 0, 'runtime append must never create loose Git objects in ODB')
    assert.equal(existsSync(join(gitDir, 'wanxiangshu')), false, 'retired Git runtime data must stay unread and unwritten')
  })

  await t.test('WHAT[durable-events-011] the writer file exists only under the local state root', () => {
    const writerFiles = readdirSync(join(stateRoot, 'wanxiangshu/events')).filter(f => f.endsWith('.ndjson'))
    assert.equal(writerFiles.length, 1, 'exactly one local writer NDJSON file must exist')
  })
})

import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as eventStore from '../../../../dist/Persistence/EventStore/Surface.js'

const runner = fileURLToPath(new URL('../../../../resources/git/wanxiang-hook.mjs', import.meta.url))

export function runHook(repo, kind = 'pre-push', arg = 'origin', input = '', environment = {}) {
  const result = spawnSync(process.execPath, [runner, kind, arg], {
    cwd: repo,
    input,
    encoding: 'utf8',
    env: { ...process.env, ...environment, WANXIANG_GIT_SYNC_ACTIVE: '' },
  })
  assert.equal(result.status, 0, result.stderr || result.stdout)
}

export async function appendFact(repo, writer, value) {
  const store = eventStore.create(join(repo, '.git'), writer)
  try {
    const result = await eventStore.append(store, [value])
    assert.equal(result.ok, true, JSON.stringify(result.error))
  } finally {
    eventStore.dispose(store)
  }
}

export function assertFacts(repo, expected) {
  const store = eventStore.create(join(repo, '.git'), 'observer')
  try {
    for (const value of expected) assert.deepEqual(eventStore.read(store, value.id), value)
    assert.deepEqual(eventStore.heads(store, expected[0].stream).sort(), expected.map(value => value.id).sort())
  } finally {
    eventStore.dispose(store)
  }
}

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../../..', import.meta.url))
const lifecycleUrl = new URL('../../../dist/Mission/Lifecycle/DurableWorkHost.js', import.meta.url).href

const consumer = `
  import assert from 'node:assert/strict'
  import { mkdtempSync, rmSync } from 'node:fs'
  import { tmpdir } from 'node:os'
  import path from 'node:path'
  import * as host from ${JSON.stringify(lifecycleUrl)}
  const directory = mkdtempSync(path.join(tmpdir(), 'wanxiangshu-lifecycle-runtime-'))
  try {
    const workId = 'runtime-work'
    const opened = host.execute(directory, workId, 'open', {
      type: 'Open',
      root: '/repo',
      sequence: ['S1', 'S2', 'S3'],
      ownerId: 'owner',
      openingCursor: 0,
    })
    assert.equal(opened.Status, 'Committed')
    assert.equal(opened.Kind, 'Opened')
    const first = host.inspected(directory, workId)
    assert.equal(first.Opened, true)
    assert.equal(first.ActiveId, opened.Result.incumbencyId)
    const second = host.inspected(directory, workId)
    assert.deepEqual(second, first)
    const ephemeral = host.useEphemeralWork('probe', [], work => work.execute('open', {
      type: 'Open',
      root: '/probe',
      sequence: ['S4'],
      ownerId: 'owner',
      openingCursor: 0,
    }))
    assert.equal(ephemeral.status, 'Committed')
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
`

for (const [runtime, executable] of [['node', process.execPath], ['bun', 'bun']]) {
  test(`WHAT[work-047] Lifecycle host loads and persists through ${runtime} SQLite without a foreign builtin import`, t => {
    if (runtime === 'bun') {
      const version = spawnSync(executable, ['--version'], { encoding: 'utf8' })
      if (version.status !== 0) {
        t.skip('Bun is unavailable in this environment.')
        return
      }
    }

    const result = spawnSync(executable, ['--input-type=module', '--eval', consumer], {
      cwd: root,
      encoding: 'utf8',
      timeout: 15000,
    })
    assert.ifError(result.error)
    assert.equal(result.status, 0, result.stderr || result.stdout)
  })
}

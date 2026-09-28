import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as causal from '../../../dist/Execution/Session/Wait/Surface.js'

const descriptor = causal.createWait({
  waitKind: 'business-result',
  owner: causal.owner('workflow', { id: 'owner' }),
  subject: { target: 'result' },
  producer: causal.externalProducer('capability', { id: 'producer' }),
  escapes: [causal.escape('processLifetime')],
  source: 'diagnostic-failure-isolation',
})

test('WHAT[causal-wait-001] a failing diagnostic destination does not change business completion or failure', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-diagnostic-failure-'))
  const blocked = join(dir, 'not-a-directory')
  writeFileSync(blocked, 'ordinary file prevents diagnostic directory creation')
  const registry = causal.createRegistry()
  try {
    assert.equal(causal.bindDiagnosticWorkspace(registry, blocked), true)
    assert.equal(await causal.awaitTask(registry, descriptor, Promise.resolve('business value')), 'business value')
    await assert.rejects(causal.awaitTask(registry, descriptor, Promise.reject(new Error('business failure'))), /business failure/)
    const snapshot = causal.snapshot(registry)
    assert.equal(snapshot.active.length, 0)
    assert.deepEqual(snapshot.history.filter(event => event.kind === 'Left').map(event => event.exit), ['WaitResolved', 'WaitFailed'])
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

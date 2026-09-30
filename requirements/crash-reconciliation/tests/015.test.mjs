import assert from 'node:assert/strict'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as delegate from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const scenario = async mode => {
  const directory = await mkdtemp(join(tmpdir(), `wanxiangshu-managed-child-${mode}-`))
  try { return await delegate.managedChildReconciliationScenario(directory, mode) }
  finally { await rm(directory, { recursive: true, force: true }) }
}

test('WHAT[crash-reconciliation-015] actual delegate adapter reuses unique exact children creates missing ones and refuses ambiguous or failed queries', async () => {
  const adopted = await scenario('matching')
  assert.deepEqual(adopted.listedFamilies, ['host-family-root'])
  assert.equal(adopted.createCount, 0)
  assert.equal(adopted.child, 'host-child-existing')
  assert.equal(adopted.error, '')
  for (const mode of ['missing', 'other-scope']) {
    const created = await scenario(mode)
    assert.deepEqual(created.listedFamilies, ['host-family-root'])
    assert.equal(created.createCount, 1)
    assert.equal(created.createTitle, `wanxiangshu:sync-delegate:v1:scope=${encodeURIComponent(created.ownerScope)}:role=engineer:agent=engineer`)
    assert.equal(created.createAgent, 'engineer')
    assert.equal(created.child, mode === 'missing' ? 'host-child-created' : 'host-child-created-exact-scope')
    assert.equal(created.error, '')
  }
  for (const mode of ['conflicting', 'query-error']) {
    const refused = await scenario(mode)
    assert.deepEqual(refused.listedFamilies, ['host-family-root'])
    assert.equal(refused.createCount, 0)
    assert.equal(refused.child, '')
    assert.match(refused.error, mode === 'conflicting' ? /conflicted.*host-child-existing-a.*host-child-existing-b/ : /controlled ListChildren rejection/)
  }
})

test('WHAT[crash-reconciliation-015] simultaneous callers share one actual attachment observation and creation', async () => {
  assert.deepEqual(await delegate.concurrentAttachedGetOrCreateScenario(), {
    observeCount: 1, createCount: 1, children: ['concurrent-child', 'concurrent-child'],
  })
})

test.todo('WHAT[crash-reconciliation-015] actual restart replacement requires proven old-session loss and Close before Link after explicit authorization (GAP-149)')

import assert from 'node:assert/strict'
import test from 'node:test'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import { realpathSync } from 'node:fs'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { execFileSync } from 'node:child_process'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

test('WHAT[interaction-authority-022] actual road decision preserves an established DevOps model on a conflicting rebind', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'devops-rebind-'))
  execFileSync('git', ['init'], { cwd: directory })
  const store = workspace.acquire(realpathSync(join(directory, '.git')))
  const opened = await lifecycle.seedOpen(store, 'plan', 'road-fixed', directory, 1n)
  assert.equal(opened.ok, true)
  const bound = await lifecycle.bindDevOps(store, 'road-fixed', 'devops:road-fixed', 'provider/fixed-model:high')
  assert.equal(bound.ok, true)
  const original = lifecycle.roadDevOps(store, 'road-fixed')
  assert.equal(original.devopsId, 'devops:road-fixed')
  assert.equal(original.modelTarget, 'provider/fixed-model:high')
  const repeated = await lifecycle.bindDevOps(store, 'road-fixed', 'devops:road-fixed', 'provider/other-model:low')
  assert.equal(repeated.ok, true)
  assert.deepEqual(lifecycle.roadDevOps(store, 'road-fixed'), original)
})

test('WHAT[interaction-authority-022] pure handle transition rejects a second child on the occupied DevOps handle', () => {
  const handle = handles.handleIdAgent('devops')
  const first = handles.apply(handles.emptyState(), { op: 'link', handle, child: 'devops-child-1', agent: 'devops', role: 'DevOps' })
  assert.equal(first.ok, true)
  const second = handles.apply(first.state, { op: 'link', handle, child: 'devops-child-2', agent: 'devops', role: 'DevOps' })
  assert.equal(second.ok, false)
})

test.todo('WHAT[interaction-authority-022] GAP-122 actual DevOps resume and crash recovery preserve model Persona and one logical authority through concurrent physical attempts')

import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const life = lifecycle.LifecycleSurface ?? lifecycle

const withWorkspace = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-008-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })
  try {
    await body(workspace.acquire(commonDir), root, commonDir)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const openWork = async (store, commonDir, workId) => {
  const opened = await surface.PlanningSurface.ensurePlanWorkOpened(store, commonDir, workId, 0n)
  assert.equal(opened.ok, true, opened.error)
  const folded = life.readProbe(life.encodeOpening('plan', workId, commonDir, 0n))
  assert.equal(folded.ok, true, folded.reason)
  assert.notEqual(folded.activeId, '')
  return folded.activeId
}

test('WHAT[work-026] authorized deliver without retirement evidence fails closed with no spawn', async () => {
  await withWorkspace(async (store, root, commonDir) => {
    const workId = 'work-d3'
    const written = surface.PlanningSurface.rewritePlanAtomic(root, workId, '# Plan content')
    assert.equal(written.ok, true)
    const activeId = await openWork(store, commonDir, workId)
    let spawnCalls = 0
    const res = await surface.PlanningSurface.executeDeliver(
      store, root, workId, activeId, 'run-d3', 'call-d3', false,
      async () => { spawnCalls += 1; return 'exec-never' },
      undefined,
    )
    assert.equal(res.ok, false)
    assert.match(res.error, /retirement cursor evidence is required/i)
    assert.equal(spawnCalls, 0)
  })
})

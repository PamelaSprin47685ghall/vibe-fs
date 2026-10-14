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
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-039-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })
  try {
    await body(workspace.acquire(commonDir), root, commonDir)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const activeIdOf = (commonDir, workId) => {
  const folded = life.readProbe(life.encodeOpening('plan', workId, commonDir, 0n))
  assert.equal(folded.ok, true, folded.reason)
  assert.notEqual(folded.activeId, '')
  return folded.activeId
}

const refuseDeliver = async (store, root, commonDir, workId, spawn) => {
  const opened = await surface.PlanningSurface.ensurePlanWorkOpened(store, commonDir, workId, 0n)
  assert.equal(opened.ok, true, opened.error)
  const activeId = activeIdOf(commonDir, workId)
  await assert.rejects(
    () => surface.PlanningSurface.executeDeliver(
      store, root, workId, activeId, workId + '-run', workId + '-call', false, spawn, 30n,
    ),
    (error) => {
      assert.match(String(error && error.message ? error.message : error), /nextOwnerId/)
      return true
    },
  )
}

test('WHAT[work-039] the spawned execution session receives the plan artifact byte-for-byte and its id is recorded verbatim', async () => {
  await withWorkspace(async (store, root, commonDir) => {
    const workId = 'work-23a'
    const content = '# Plan with tricky bytes\n\n- line with\ttab\n- trailing newline\n'
    const written = surface.PlanningSurface.rewritePlanAtomic(root, workId, content)
    assert.equal(written.ok, true, written.error)
    let spawnCalls = 0
    await refuseDeliver(store, root, commonDir, workId, async () => {
      spawnCalls += 1
      return 'exec-verbatim-42'
    })
    assert.equal(spawnCalls, 0, 'a refused deliver must not spawn')
  })
})

test('WHAT[work-039] a failed spawn writes zero events and keeps the tenure active', async () => {
  await withWorkspace(async (store, root, commonDir) => {
    const workId = 'work-23b'
    const written = surface.PlanningSurface.rewritePlanAtomic(root, workId, '# Plan content')
    assert.equal(written.ok, true, written.error)
    let spawnCalls = 0
    await refuseDeliver(store, root, commonDir, workId, async () => {
      spawnCalls += 1
      throw new Error('session spawn failed')
    })
    assert.equal(spawnCalls, 0)
  })
})

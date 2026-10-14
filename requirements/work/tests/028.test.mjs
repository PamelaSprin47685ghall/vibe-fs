import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const withStore = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-028-'))
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
}

test('WHAT[work-028] ask delegates the intent to the bound DevOps and returns its work record', async () => {
  await withStore(async (store, _root, commonDir) => {
    await openWork(store, commonDir, 'work-ask-1')
    let delegatedIntent = null
    const delegate = async (intent) => {
      delegatedIntent = intent
      return '# DevOps work record\nInvestigated build artifact paths; they match expectations.'
    }

    const res = await surface.PlanningSurface.executeAsk(store, 'work-ask-1', 'Check whether build artifact paths match expectation', delegate)
    assert.equal(res.ok, true, res.error)
    assert.equal(res.kind, 'delegated')
    assert.equal(delegatedIntent, 'Check whether build artifact paths match expectation')
    assert.equal(res.workRecord.includes('DevOps work record'), true)

    const resEmpty = await surface.PlanningSurface.executeAsk(store, 'work-ask-1', '   ', delegate)
    assert.equal(resEmpty.ok, false)
    assert.match(resEmpty.error, /cannot be empty/i)
  })
})

test('WHAT[work-028] a second ask is rejected while one delegation is pending', async () => {
  await withStore(async (store, _root, commonDir) => {
    await openWork(store, commonDir, 'work-ask-2')
    const pendingDelegate = () => new Promise(() => {})
    const first = surface.PlanningSurface.executeAsk(store, 'work-ask-2', 'First investigation', pendingDelegate)
    const second = await surface.PlanningSurface.executeAsk(store, 'work-ask-2', 'Second investigation', pendingDelegate)
    assert.equal(second.ok, false)
    assert.match(second.error, /pending|already/i)
    first.catch(() => {})
  })
})

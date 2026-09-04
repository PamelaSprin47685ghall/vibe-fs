// CAUSAL-008 — diagnostic bridge is process-local, overwritable and git-excluded.

import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')

const mkWait = (waitKind, ownerKind, subject, producer) =>
  causal.createWait({
    waitKind,
    owner: causal.owner(ownerKind, { id: ownerKind }),
    subject,
    producer,
    escapes: [causal.escape('processLifetime')],
    source: 'causal-wait-bridge.test',
  })

const externalProducer = (kind, identity) => causal.externalProducer(kind, identity)

const readDiagnostic = (workspace) =>
  JSON.parse(fs.readFileSync(path.join(workspace, '.wanxiangshu', 'diagnostics', 'causal-waits.json'), 'utf8'))

test('WHAT[CAUSAL-008] CAUSAL_BRIDGE_writeSnapshot_overwrites_workspace_json', () => {
  const workspace = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-bridge-'))
  fs.mkdirSync(path.join(workspace, '.git', 'info'), { recursive: true })
  const registry = causal.createRegistry()
  const wait = mkWait(
    'provider-assessment',
    'RelayWorkflow',
    { incumbency: 'I2', road: 'R17' },
    externalProducer('provider', { run: 'P81' }),
  )
  const lease = causal.enter(registry, wait)
  try {
    causal.writeSnapshot(workspace, registry)
    const filePath = path.join(workspace, '.wanxiangshu', 'diagnostics', 'causal-waits.json')
    assert.equal(fs.existsSync(filePath), true)
    const exclude = fs.readFileSync(path.join(workspace, '.git', 'info', 'exclude'), 'utf8')
    assert.ok(exclude.includes('.wanxiangshu/'), 'diagnostic dir must be git-excluded')
    const snap = readDiagnostic(workspace)
    assert.equal(typeof snap.pid, 'number')
    assert.ok(String(snap.sequence).length > 0)
    assert.equal(snap.active.length, 1)
    assert.equal(snap.active[0].waitKind, 'provider-assessment')
    assert.ok(Array.isArray(snap.history))
    assert.ok(Array.isArray(snap.frontiers))
    assert.ok(snap.frontiers.length >= 1)
  } finally {
    causal.dispose(lease)
    fs.rmSync(workspace, { recursive: true, force: true })
  }
})

test('WHAT[CAUSAL-008] CAUSAL_BRIDGE_hub_refreshes_file_on_enter', () => {
  const workspace = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-hub-'))
  causal.hubSetWorkspace(workspace)
  const wait = mkWait(
    'manager-job',
    'OrchestratorWorkflow',
    { job: 'OJ7', manager: 'M4' },
    causal.workflowProducer(causal.owner('ManagerWorkflow', { session: 'M4' })),
  )
  const lease = causal.hubEnter(wait)
  try {
    const filePath = path.join(workspace, '.wanxiangshu', 'diagnostics', 'causal-waits.json')
    assert.equal(fs.existsSync(filePath), true)
    assert.ok(readDiagnostic(workspace).active.some((value) => value.waitKind === 'manager-job'))
    causal.hubWriteToWorkspace()
    assert.equal(fs.existsSync(filePath), true)
  } finally {
    causal.dispose(lease)
    causal.hubSetWorkspace(null)
    fs.rmSync(workspace, { recursive: true, force: true })
  }
})

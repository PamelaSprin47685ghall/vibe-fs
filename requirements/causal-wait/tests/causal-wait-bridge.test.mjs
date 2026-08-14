// Split from tests/unit/session/causal-wait-bridge.test.mjs (cutover Wave 2a); owner: causal-wait.
//
// CAUSAL-008 bridge 面：writeSnapshot / Hub 写盘断言（诊断文件 git-excluded、非
// Journal）。E2E diagnostics 格式化（formatDiagnostics/formatCausalSection/watchdog
// onTimeout）已随 SPLIT@cutover 迁 requirements/verification-system/tests/
// causal-diagnostics.test.mjs。

import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'

import { toList } from '../../verification-system/tests/support/domain.mjs'
import {
  CausalOwner_create,
  CausalProducerRef,
  DiagnosticWaitModule_create,
  WaitEscape,
} from '../../../dist/Kernel/CausalWait.js'
import {
  CausalWaitRegistry,
  CausalWaitHub_setWorkspace,
  CausalWaitHub_observer,
  CausalWaitHub_writeToWorkspace,
} from '../../../dist/Session/CausalWaitRegistry.js'
import { writeSnapshot as CausalWaitBridge_writeSnapshot } from '../../../dist/Session/CausalWaitBridge.js'

const mkWait = (waitKind, ownerKind, subjectPairs, producer) =>
  DiagnosticWaitModule_create(
    waitKind,
    CausalOwner_create(ownerKind, toList(subjectPairs.slice(0, 1))),
    toList(subjectPairs),
    producer,
    toList([WaitEscape.ProcessLifetime]),
    'causal-wait-bridge.test',
  )

const externalProducer = (kind, identity) => new CausalProducerRef(1, [kind, toList(identity)])

test('CAUSAL_BRIDGE_writeSnapshot_overwrites_workspace_json', () => {
  const workspace = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-bridge-'))
  fs.mkdirSync(path.join(workspace, '.git', 'info'), { recursive: true })
  const registry = new CausalWaitRegistry()
  const wait = mkWait(
    'provider-verdict',
    'ReviewerWorkflow',
    [['reviewer', 'R2'], ['barrier', 'B17']],
    externalProducer('provider', [['run', 'P81']]),
  )
  const lease = registry.Enter(wait)
  try {
    CausalWaitBridge_writeSnapshot(workspace, registry)
    const filePath = path.join(workspace, '.wanxiangshu', 'diagnostics', 'causal-waits.json')
    assert.equal(fs.existsSync(filePath), true)
    const exclude = fs.readFileSync(path.join(workspace, '.git', 'info', 'exclude'), 'utf8')
    assert.ok(exclude.includes('.wanxiangshu/'), 'diagnostic dir must be git-excluded')
    const snap = JSON.parse(fs.readFileSync(filePath, 'utf8'))
    assert.equal(typeof snap.pid, 'number')
    assert.ok(String(snap.sequence).length > 0)
    assert.equal(snap.active.length, 1)
    assert.equal(snap.active[0].waitKind, 'provider-verdict')
    assert.ok(Array.isArray(snap.history))
    assert.ok(Array.isArray(snap.frontiers))
    assert.ok(snap.frontiers.length >= 1)
  } finally {
    lease.Dispose()
    fs.rmSync(workspace, { recursive: true, force: true })
  }
})

test('CAUSAL_BRIDGE_hub_refreshes_file_on_enter', () => {
  const workspace = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-hub-'))
  CausalWaitHub_setWorkspace(workspace)
  const wait = mkWait(
    'manager-job',
    'OrchestratorWorkflow',
    [['job', 'OJ7'], ['manager', 'M4']],
    new CausalProducerRef(0, [CausalOwner_create('ManagerWorkflow', toList([['session', 'M4']]))]),
  )
  const lease = CausalWaitHub_observer.Enter(wait)
  try {
    const filePath = path.join(workspace, '.wanxiangshu', 'diagnostics', 'causal-waits.json')
    assert.equal(fs.existsSync(filePath), true)
    const snap = JSON.parse(fs.readFileSync(filePath, 'utf8'))
    assert.ok(snap.active.some((w) => w.waitKind === 'manager-job'))
    CausalWaitHub_writeToWorkspace()
    assert.equal(fs.existsSync(filePath), true)
  } finally {
    lease.Dispose()
    CausalWaitHub_setWorkspace(undefined)
    fs.rmSync(workspace, { recursive: true, force: true })
  }
})

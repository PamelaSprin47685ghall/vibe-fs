// Split from tests/unit/session/causal-wait-bridge.test.mjs (cutover Wave 2a); owner: verification-system.
//
// E2E diagnostics 格式化 MECHANISM：gatherDiagnostics 读取 causal-waits.json、
// formatDiagnostics/formatCausalSection 首屏顺序、scenario-parallel.js watchdog
// onTimeout 的 frontier-before-tail 渲染。bridge 文件/registry 断言已随 SPLIT@cutover
// 迁 requirements/causal-wait/tests/causal-wait-bridge.test.mjs。

import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { gatherDiagnostics } from './e2e/support/diagnostics-collect.js'
import { formatDiagnostics } from './e2e/support/diagnostics-format.js'
import { formatCausalSection } from './e2e/support/diagnostics-causal.js'

test('WHAT[VERIFICATION-SYSTEM-006] gather reads causal waits file', async () => {
  const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-gather-'))
  const dir = path.join(workDir, '.wanxiangshu', 'diagnostics')
  fs.mkdirSync(dir, { recursive: true })
  const payload = {
    pid: 1,
    sequence: '3',
    active: [{
      waitKind: 'provider-assessment',
      owner: { kind: 'RelayWorkflow', identity: [{ k: 'incumbency', v: 'I2' }] },
      subject: [{ k: 'road', v: 'road-manager.0' }],
      producer: { tag: 'external', kind: 'provider', identity: [{ k: 'run', v: 'P81' }] },
      escapes: [{ tag: 'processLifetime' }],
      source: 'test',
    }],
    history: [],
    frontiers: [{
      kind: 'ExternalProducerFrontier',
      detail: 'FRONTIER: waiting for external producer external:provider',
      chain: [{ owner: { kind: 'RelayWorkflow', identity: [{ k: 'incumbency', v: 'I2' }] }, waitKind: 'provider-assessment' }],
      frontierProducer: { tag: 'external', kind: 'provider', identity: [{ k: 'run', v: 'P81' }] },
      cycle: [],
    }],
  }
  fs.writeFileSync(path.join(dir, 'causal-waits.json'), JSON.stringify(payload), 'utf8')
  try {
    const diag = await gatherDiagnostics({
      host: { workDir },
      provider: {
        requests: [],
        unexpectedRequests: [],
        remainingExpectations: 1,
        blockedExpectations: [{ id: 'road-manager.0', lane: 'x', blocking: true }],
      },
    })
    assert.ok(diag.causalWaitSnapshot)
    assert.equal(diag.causalWaitSnapshot.active[0].waitKind, 'provider-assessment')
    assert.ok(Array.isArray(diag.causalFrontier))
    assert.equal(diag.causalExpectationCorrelation.matched.includes('road-manager.0'), true)
  } finally {
    fs.rmSync(workDir, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-006] format puts frontier before e2e events', () => {
  const text = formatDiagnostics({
    events: [{ seq: 1, time: '00:00:00.000', type: 'session.idle' }],
    causalFrontier: [{
      kind: 'ExternalProducerFrontier',
      detail: 'FRONTIER: waiting for external producer external:provider',
      chain: [{ owner: { kind: 'RelayWorkflow', identity: [{ k: 'incumbency', v: 'I2' }] }, waitKind: 'provider-assessment' }],
      frontierProducer: { tag: 'external', kind: 'provider', identity: [{ k: 'run', v: 'P81' }] },
      cycle: [],
    }],
    causalWaitSnapshot: {
      active: [],
      history: [{ sequence: '1', kind: 'entered', wait: { waitKind: 'provider-assessment' }, exit: null }],
    },
  })
  const frontierAt = text.indexOf('════════════ CAUSAL FRONTIER ════════════')
  const e2eAt = text.indexOf('══════════════════════ E2E DIAGNOSTICS ══════════════════════')
  assert.ok(frontierAt >= 0, 'missing CAUSAL FRONTIER banner')
  assert.ok(e2eAt > frontierAt, 'CAUSAL FRONTIER must precede E2E DIAGNOSTICS')
})

test('WHAT[VERIFICATION-SYSTEM-006] formatCausalSection banner is first line', () => {
  const lines = formatCausalSection({
    causalFrontier: [{
      kind: 'ExternalProducerFrontier',
      detail: 'FRONTIER: waiting for external producer external:provider',
      chain: [],
      frontierProducer: { tag: 'external', kind: 'provider', identity: [] },
      cycle: [],
    }],
    causalWaitSnapshot: { active: [], history: [] },
  })
  assert.ok(lines.length > 0, 'formatCausalSection must emit lines')
  assert.equal(lines[0], '════════════ CAUSAL FRONTIER ════════════')
})

test('WHAT[VERIFICATION-SYSTEM-006] watchdog onTimeout prints frontier before event tail', () => {
  const source = fs.readFileSync(
    fileURLToPath(new URL('./e2e/support/scenario-parallel.js', import.meta.url)),
    'utf8',
  )
  const onTimeoutAt = source.indexOf('onTimeout: async () => {')
  assert.ok(onTimeoutAt >= 0, 'missing watchdog onTimeout')
  const body = source.slice(onTimeoutAt)
  const frontierAt = body.indexOf('CAUSAL FRONTIER')
  const eventTailAt = body.indexOf('watchdog event tail')
  assert.ok(frontierAt >= 0, 'onTimeout must print CAUSAL FRONTIER')
  assert.ok(eventTailAt >= 0, 'onTimeout must still print event tail')
  assert.ok(frontierAt < eventTailAt, 'CAUSAL FRONTIER must precede watchdog event tail')
  assert.ok(body.includes('collectCausalWaits(diag, scenario)'), 'onTimeout must collect via scenario')
})

import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'
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
import { gatherDiagnostics } from '../../e2e/support/diagnostics-collect.js'
import { formatDiagnostics } from '../../e2e/support/diagnostics-format.js'
import { formatCausalSection } from '../../e2e/support/diagnostics-causal.js'

const mkWait = (waitKind, ownerKind, subjectPairs, producer) =>
  DiagnosticWaitModule_create(
    waitKind,
    CausalOwner_create(ownerKind, ofArray(subjectPairs.slice(0, 1))),
    ofArray(subjectPairs),
    producer,
    ofArray([WaitEscape.ProcessLifetime]),
    'causal-wait-bridge.test',
  )

const externalProducer = (kind, identity) => new CausalProducerRef(1, [kind, ofArray(identity)])

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
    new CausalProducerRef(0, [CausalOwner_create('ManagerWorkflow', ofArray([['session', 'M4']]))]),
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

test('CAUSAL_DIAG_gather_reads_causal_waits_file', async () => {
  const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-gather-'))
  const dir = path.join(workDir, '.wanxiangshu', 'diagnostics')
  fs.mkdirSync(dir, { recursive: true })
  const payload = {
    pid: 1,
    sequence: '3',
    active: [{
      waitKind: 'provider-verdict',
      owner: { kind: 'ReviewerWorkflow', identity: [{ k: 'reviewer', v: 'R2' }] },
      subject: [{ k: 'barrier', v: 'barrier-reviewer.0' }],
      producer: { tag: 'external', kind: 'provider', identity: [{ k: 'run', v: 'P81' }] },
      escapes: [{ tag: 'processLifetime' }],
      source: 'test',
    }],
    history: [],
    frontiers: [{
      kind: 'ExternalProducerFrontier',
      detail: 'FRONTIER: waiting for external producer external:provider',
      chain: [{ owner: { kind: 'ReviewerWorkflow', identity: [{ k: 'reviewer', v: 'R2' }] }, waitKind: 'provider-verdict' }],
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
        blockedExpectations: [{ id: 'barrier-reviewer.0', lane: 'x', blocking: true }],
      },
    })
    assert.ok(diag.causalWaitSnapshot)
    assert.equal(diag.causalWaitSnapshot.active[0].waitKind, 'provider-verdict')
    assert.ok(Array.isArray(diag.causalFrontier))
    assert.equal(diag.causalExpectationCorrelation.matched.includes('barrier-reviewer.0'), true)
  } finally {
    fs.rmSync(workDir, { recursive: true, force: true })
  }
})

test('CAUSAL_DIAG_format_puts_frontier_before_e2e_events', () => {
  const text = formatDiagnostics({
    events: [{ seq: 1, time: '00:00:00.000', type: 'session.idle' }],
    causalFrontier: [{
      kind: 'ExternalProducerFrontier',
      detail: 'FRONTIER: waiting for external producer external:provider',
      chain: [{ owner: { kind: 'ReviewerWorkflow', identity: [{ k: 'reviewer', v: 'R2' }] }, waitKind: 'provider-verdict' }],
      frontierProducer: { tag: 'external', kind: 'provider', identity: [{ k: 'run', v: 'P81' }] },
      cycle: [],
    }],
    causalWaitSnapshot: {
      active: [],
      history: [{ sequence: '1', kind: 'entered', wait: { waitKind: 'provider-verdict' }, exit: null }],
    },
  })
  const frontierAt = text.indexOf('════════════ CAUSAL FRONTIER ════════════')
  const e2eAt = text.indexOf('══════════════════════ E2E DIAGNOSTICS ══════════════════════')
  assert.ok(frontierAt >= 0, 'missing CAUSAL FRONTIER banner')
  assert.ok(e2eAt > frontierAt, 'CAUSAL FRONTIER must precede E2E DIAGNOSTICS')
})

test('CAUSAL_DIAG_formatCausalSection_banner_is_first_line', () => {
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

test('CAUSAL_WATCHDOG_onTimeout_prints_frontier_before_event_tail', () => {
  const source = fs.readFileSync(
    fileURLToPath(new URL('../../e2e/support/scenario-parallel.js', import.meta.url)),
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

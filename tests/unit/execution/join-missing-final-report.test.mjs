// Join completion — MISSING_FINAL_REPORT / empty-terminal must be observation-only.
//
// A manager's fork subagent that stops with an empty or missing final report is
// NOT concluded as failed. Per FALLBACK-008 an empty/XML-only terminal earns an
// interaction repair (RepairOnce / AbandonRoundProduct), never a slot failure; the
// subagent auto-retries and continues. `HostForkRunLifecycle.complete` must keep
// the pending run Active for a later proven terminal (P0-RECOVERY-JOIN-001) instead
// of delivering a proven MISSING_FINAL_REPORT failure. This mirrors the `Aborted`
// branch ("Observation only. Keep pending run Active for a later proven terminal.").

import assert from 'node:assert/strict'
import { readdirSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { agentCompletion, caseOf, pendingRunLifecycle, roles, sessionId } from '../support/domain.mjs'

const BUILD_ROOT = new URL('../../../dist/', import.meta.url).pathname

const importDist = async (rel) => import(new URL(`../../../dist/${rel}`, import.meta.url).pathname)

// ── pending-runs dictionary + run helpers (mirror join-completion.test.mjs) ───

async function loadDictionary() {
  const fableLibDir = (() => {
    const root = join(BUILD_ROOT, 'fable_modules')
    const name = readdirSync(root).find((e) => e.startsWith('fable-library-js.'))
    if (!name) throw new Error(`no fable-library-js.* under dist; run npm run build`)
    return join(root, name)
  })()
  const candidates = ['MutableMap.js', 'System.Collections.Generic.js', 'MapUtil.js']
  for (const file of candidates) {
    try {
      const mod = await import(join(fableLibDir, file))
      const Ctor = mod.Dictionary ?? mod.default?.Dictionary
      if (typeof Ctor === 'function') {
        const util = await import(join(fableLibDir, 'Util.js'))
        return {
          Dictionary: Ctor,
          comparer: { Equals: util.equals, GetHashCode: (x) => (util.safeHash(x) | 0) },
        }
      }
    } catch {
      // try next
    }
  }
  const DictShim = class {
    constructor() {
      this._m = new Map()
    }
    set_Item(k, v) { this._m.set(k, v) }
    get_Item(k) { return this._m.get(k) }
    set(k, v) { this._m.set(k, v) }
    get(k) { return this._m.get(k) }
    has(k) { return this._m.has(k) }
    TryGetValue(k) {
      if (this._m.has(k)) return [true, this._m.get(k)]
      return [false, null]
    }
    Remove(k) { return this._m.delete(k) }
  }
  return { Dictionary: DictShim }
}

function makePendingRuns(pendingRuns, run) {
  if (typeof pendingRuns.set_Item === 'function') pendingRuns.set_Item(run.AgentId, run)
  else if (typeof pendingRuns.set === 'function') pendingRuns.set(run.AgentId, run)
  else pendingRuns[run.AgentId] = run
  if (typeof pendingRuns.TryGetValue !== 'function') {
    pendingRuns.TryGetValue = (key) => {
      const has = typeof pendingRuns.has === 'function'
        ? pendingRuns.has(key)
        : pendingRuns.get?.(key) !== undefined
      if (!has) return [false, null]
      const value = typeof pendingRuns.get_Item === 'function'
        ? pendingRuns.get_Item(key)
        : pendingRuns.get(key)
      return [true, value]
    }
  }
  if (typeof pendingRuns.Remove !== 'function') {
    pendingRuns.Remove = (key) => (typeof pendingRuns.delete === 'function' ? pendingRuns.delete(key) : false)
  }
}

function makeRun(agentId, childId) {
  const source = pendingRunLifecycle.completionSource()
  return {
    Token: {},
    AgentId: agentId,
    ChildId: childId,
    Role: roles.of('Coder'),
    Source: source,
    Subscription: undefined,
    Finished: false,
  }
}

// Fable union constructor for TerminalOutcome.Failed (tag 2).
async function failedOutcome(error) {
  const Events = await importDist('Infrastructure/OpenCode/Host/Events.js')
  return new Events.TerminalOutcome(/* Failed */ 2, [error])
}

// Fable Record constructor for AgentRunResult (dist/Kernel/Outcome.js).
// Order: SessionId, AuthorityRootUserMessageId, ProviderRun, Role, Directory,
// TerminalText, TurnFormalText. TerminalText empty => IsValid=false.
async function completedOutcome(terminalText) {
  const [Outcome, Events, roots] = await Promise.all([
    importDist('Kernel/Outcome.js'),
    importDist('Infrastructure/OpenCode/Host/Events.js'),
    (async () => {
      const Identity = await importDist('Kernel/Identity.js')
      return {
        session: sessionId,
        authorityRoot: (v) => {
          const id = Identity.AuthorityRootUserMessageId
          return new id(v)
        },
        providerRun: (v) => {
          const id = Identity.ProviderRunIdentity
          return new id(v)
        },
      }
    })(),
  ])
  const result = new Outcome.AgentRunResult(
    roots.session('ses_child'),
    roots.authorityRoot('root_1'),
    roots.providerRun('run_1'),
    roles.of('Coder'),
    null, // Directory : string option -> null is None in Fable output
    terminalText,
    terminalText,
  )
  return new Events.TerminalOutcome(/* Completed */ 0, [result])
}

// ── MISSING_FINAL_REPORT is observation-only ─────────────────────────────────

test('EXEC_join_MissingFinalReport_Failed_keeps_run_pending_not_failed', async () => {
  const { Dictionary, comparer } = await loadDictionary()
  const pendingRuns = comparer ? new Dictionary([], comparer) : new Dictionary()
  const gate = {}
  const agentId = 'agent-mfr'
  const child = sessionId('ses_mfr_child')
  const parent = sessionId('ses_mfr_parent')
  const run = makeRun(agentId, child)
  makePendingRuns(pendingRuns, run)

  const mfrFailed = await failedOutcome('MISSING_FINAL_REPORT')
  pendingRunLifecycle.complete(gate, pendingRuns, undefined, parent, null, run, mfrFailed, undefined)

  // Observation only (like Aborted): the run is not claimed.
  assert.equal(run.Finished, false, 'MISSING_FINAL_REPORT must not settle the run')
  assert.equal(pendingRuns.has(agentId), true, 'run must stay in pendingRuns for a later proven terminal')

  // The completion cell must NOT be resolved — a later proven terminal will.
  const resolved = await Promise.race([
    run.Source.get_Task().then(() => true, () => true),
    new Promise((resolve) => setTimeout(() => resolve(false), 50)),
  ])
  assert.equal(resolved, false, 'MISSING_FINAL_REPORT must not resolve the run completion Source')
})

test('EXEC_join_empty_Completed_keeps_run_pending_not_failed', async () => {
  const { Dictionary, comparer } = await loadDictionary()
  const pendingRuns = comparer ? new Dictionary([], comparer) : new Dictionary()
  const gate = {}
  const agentId = 'agent-empty'
  const child = sessionId('ses_empty_child')
  const parent = sessionId('ses_empty_parent')
  const run = makeRun(agentId, child)
  makePendingRuns(pendingRuns, run)

  const emptyCompleted = await completedOutcome('')
  pendingRunLifecycle.complete(gate, pendingRuns, undefined, parent, null, run, emptyCompleted, undefined)

  assert.equal(run.Finished, false, 'empty Completed must not settle the run')
  assert.equal(pendingRuns.has(agentId), true, 'empty Completed must keep the run pending')

  const resolved = await Promise.race([
    run.Source.get_Task().then(() => true, () => true),
    new Promise((resolve) => setTimeout(() => resolve(false), 50)),
  ])
  assert.equal(resolved, false, 'empty Completed must not resolve the run completion Source')
})

// ── genuine failures still settle the run ────────────────────────────────────

test('EXEC_join_real_Failed_still_claims_run', async () => {
  const { Dictionary, comparer } = await loadDictionary()
  const pendingRuns = comparer ? new Dictionary([], comparer) : new Dictionary()
  const gate = {}
  const agentId = 'agent-real'
  const child = sessionId('ses_real_child')
  const parent = sessionId('ses_real_parent')
  const run = makeRun(agentId, child)
  makePendingRuns(pendingRuns, run)

  const realFailed = await failedOutcome('provider timeout')
  pendingRunLifecycle.complete(gate, pendingRuns, undefined, parent, null, run, realFailed, undefined)

  assert.equal(run.Finished, true, 'real failure must still claim the run')
  const agentOutcome = await Promise.race([
    run.Source.get_Task(),
    new Promise((_, reject) => setTimeout(() => reject(new Error('Source never completed')), 1000)),
  ])
  assert.equal(caseOf(agentOutcome), 'AgentFailed')
})

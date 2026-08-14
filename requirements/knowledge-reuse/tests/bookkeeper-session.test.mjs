// FROZEN — 2026-08-14. Bookkeeper session tests use local EventStore Current only.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CasebookWorkflow_archiveInspectorResult as archive, CasebookWorkflow_fetchCase as fetchCase } from '../../../dist/Repository/Knowledge/Casebook/Workflow.js'
import { refreshStale } from '../../../dist/Repository/Knowledge/Casebook/Bookkeeper.js'
import { collector, cleanupInspector, noteAnswer, notePrompt, setEnabled, tryFinalizeInspector } from '../../../dist/Repository/Knowledge/Casebook/Lifecycle.js'
import { ObservationCollector__Collect_Z15AE2BE0 as collect } from '../../../dist/Enforcer/ObservationCollector.js'
import { contentHash as hash } from '../../../dist/Repository/Knowledge/Casebook/Capture.js'
import { Observation } from '../../../dist/Repository/Knowledge/Casebook/Model.js'
import { acquire } from '../../../dist/OpenCode/Host/WorkspaceEventStore.js'
import { gitCommonDir } from '../../../dist/Persistence/Journal/RuntimePath.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'
import { idValue, resultOf, sessionId, toList } from '../../verification-system/tests/support/domain.mjs'
import { BookkeeperRuntime_setSessionPort as setSessionPort, BookkeeperRuntime_resetSessionPort as resetSessionPort, BookkeeperRuntime_txIdFor as txIdFor } from '../../../dist/Repository/Knowledge/Casebook/BookkeeperRuntime.js'

const { HostToolArguments_$ctor_4E60E31B: makeArgs, HostToolContext } = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js')
const { execute } = await import('../../../dist/Repository/Programming/Js/OpenCode/BookkeeperTool.js')
const { TerminalOutcome } = await import('../../../dist/OpenCode/Host/Events.js')
const { AgentRunResult } = await import('../../../dist/Foundation/Outcome.js')
const { Role } = await import('../../../dist/Foundation/Roles.js')

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, h) => new Observation(obsIndex('FileRead'), [path, h])
export const CANONICAL_Q = 'Canonical maintained question'
export const CANONICAL_A = 'Summary of Inspector answers across turns.'
const context = (sessionKey) => new HostToolContext(sessionKey, undefined, undefined, undefined, undefined, () => () => {})
const completedTerminal = (child) => new TerminalOutcome(0, [new AgentRunResult(child, undefined, undefined, Role.Inspector, undefined, 'wide', 'idle')])

export const scriptedBookkeeperPort = () => {
  const createCalls = []
  const prompts = []
  const programCalls = []
  const terminals = new Set()
  let seq = 0
  const port = {
    CreateChildSession: async (parentId, options) => {
      seq += 1
      const child = sessionId(`bk-child-${seq}`)
      createCalls.push({ parent: idValue.session(parentId), title: options?.Title, agent: options?.Agent, child: idValue.session(child) })
      return { tag: 0, fields: [child] }
    },
    AbortSession: async () => ({ tag: 0, fields: [] }),
    SubscribeTerminal: (_childId, callback) => {
      terminals.add(callback)
      return { Dispose: () => terminals.delete(callback) }
    },
    SendPrompt: async (childSession, text) => {
      prompts.push(text)
      const sid = idValue.session(childSession)
      const tx = txIdFor(sid)
      assert.equal(Boolean(tx), true)
      const out = await execute(makeArgs({ program: `class Js extends JsProgram { async run() { this.setQuestion(${JSON.stringify(CANONICAL_Q)}); this.setAnswer(${JSON.stringify(CANONICAL_A)}); return { changed: true }; } }` }), context(sid))
      assert.equal(String(out).includes('changed = true'), true, out)
      programCalls.push(tx)
      for (const callback of terminals) callback(childSession, completedTerminal(childSession))
      return { tag: 0, fields: [] }
    },
  }
  return { port, createCalls, prompts, programCalls }
}

const record = (session, q, a, observations) => ({ SessionId: session, Q: q, A: a, Observations: toList(observations), LastAccessOrder: 0 })

test('CASE006_create_child_once_per_refresh_via_js_bookkeeper', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-session-refresh-'))
  const local = createLocalEventStore()
  const { port, createCalls, programCalls, prompts } = scriptedBookkeeperPort()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal(resultOf(await archive(local.store, record('s-session-refresh', 'Q keep', 'A keep', [fileRead('a.txt', hash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    setSessionPort(port)
    const refreshed = resultOf(await refreshStale(local.store, dir, 's-session-refresh'))
    assert.equal(refreshed.ok, true)
    assert.equal(refreshed.value, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)
    assert.equal(prompts.some((text) => String(text).includes('CaseRefresh')), true)
    const fetched = resultOf(await fetchCase(local.store, 10, 's-session-refresh'))
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.equal(fetched.value.A, CANONICAL_A)
  } finally {
    resetSessionPort()
    local.close()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE010_finalize_create_child_once_and_cleanup_never_runs_bookkeeper', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-session-fin-'))
  const { port, createCalls, programCalls, prompts } = scriptedBookkeeperPort()
  try {
    execFileSync('git', ['init', '--quiet', dir])
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    setEnabled(dir)
    setSessionPort(port)
    const key = 'insp-session-fin'
    notePrompt(key, 'Who owns PromptAuthority?')
    noteAnswer(key, 'Host owns PromptAuthority.')
    notePrompt(key, 'Where do Case facts live?')
    collect(collector, key, 'read', { path: 'a.txt' }, 'hello')
    noteAnswer(key, 'Unified EventStore only.')
    assert.equal(resultOf(await tryFinalizeInspector(dir, key)).ok, true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)
    assert.equal(prompts.some((text) => String(text).includes('CaseFinalize')), true)

    const store = acquire(gitCommonDir(dir))
    const fetched = resultOf(await fetchCase(store, 10, key))
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.equal(fetched.value.A, CANONICAL_A)
    const before = createCalls.length
    notePrompt(key, 'cleanup Q')
    noteAnswer(key, 'cleanup A')
    cleanupInspector(key)
    assert.equal(createCalls.length, before)
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE006_missing_session_port_keeps_old_case', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-session-noport-'))
  const local = createLocalEventStore()
  try {
    resetSessionPort()
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal(resultOf(await archive(local.store, record('s-noport', 'Q keep', 'A keep', [fileRead('a.txt', hash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    const refreshed = resultOf(await refreshStale(local.store, dir, 's-noport'))
    assert.equal(refreshed.ok, false)
    assert.match(String(refreshed.error), /session port/)
    const fetched = resultOf(await fetchCase(local.store, 10, 's-noport'))
    assert.equal(fetched.value.Q, 'Q keep')
    assert.equal(fetched.value.A, 'A keep')
  } finally {
    local.close()
    rmSync(dir, { recursive: true, force: true })
  }
})

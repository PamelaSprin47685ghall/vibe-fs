import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CasebookWorkflow_archiveInspectorResult as archive } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { CasebookWorkflow_fetchCase as fetchCase } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { refreshStale } from '../../../dist/Infrastructure/CasebookBookkeeper.js'
import {
  collector,
  cleanupInspector,
  noteAnswer,
  notePrompt,
  setEnabled,
  tryFinalizeInspector,
} from '../../../dist/Infrastructure/CasebookLifecycle.js'
import {
  ObservationCollector__Collect_Z15AE2BE0 as collect,
} from '../../../dist/Infrastructure/ObservationCollector.js'
import { contentHash as hash } from '../../../dist/Infrastructure/CasebookCapture.js'
import { Observation } from '../../../dist/Domain/Casebook.js'
import { acquire } from '../../../dist/Infrastructure/OpenCode/Host/WorkspaceEventStore.js'
import { gitCommonDir } from '../../../dist/Journal/RuntimePath.js'
import { GitRawStore_createInMemory as createRaw } from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import { EventStore_create as createStore } from '../../../dist/Infrastructure/Persist/EventStore.js'
import { idValue, resultOf, sessionId, toList } from '../../verification-system/tests/support/domain.mjs'
import {
  BookkeeperRuntime_setSessionPort as setSessionPort,
  BookkeeperRuntime_resetSessionPort as resetSessionPort,
  BookkeeperRuntime_txIdFor as txIdFor,
} from '../../../dist/Infrastructure/BookkeeperRuntime.js'

const { HostToolArguments_$ctor_4E60E31B: makeArgs, HostToolContext } = await import(
  '../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js'
)
const { execute } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsBookkeeperTool.js')
const { TerminalOutcome } = await import('../../../dist/Infrastructure/OpenCode/Host/Events.js')
const { AgentRunResult } = await import('../../../dist/Kernel/Outcome.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, h) => new Observation(obsIndex('FileRead'), [path, h])

export const CANONICAL_Q = 'Canonical maintained question'
export const CANONICAL_A = 'Summary of Inspector answers across turns.'

const context = (sessionKey) =>
  new HostToolContext(sessionKey, undefined, undefined, undefined, undefined, () => () => {})

const completedTerminal = (child) =>
  new TerminalOutcome(0, [
    new AgentRunResult(child, undefined, undefined, Role.Inspector, undefined, 'wide', 'idle'),
  ])

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
      createCalls.push({
        parent: idValue.session(parentId),
        title: options?.Title,
        agent: options?.Agent,
        child: idValue.session(child),
      })
      return { tag: 0, fields: [child] }
    },
    AbortSession: async () => ({ tag: 0, fields: [] }),
    SubscribeTerminal: (_childId, callback) => {
      terminals.add(callback)
      return {
        Dispose: () => {
          terminals.delete(callback)
        },
      }
    },
    SendPrompt: async (childSession, text, _options) => {
      prompts.push(text)
      const sid = idValue.session(childSession)
      const tx = txIdFor(sid)
      assert.equal(Boolean(tx), true, 'SendPrompt must run against a bound Bookkeeper tx')
      const out = await execute(
        makeArgs({
          program: `class Js extends JsProgram {
            async run() {
              this.setQuestion(${JSON.stringify(CANONICAL_Q)});
              this.setAnswer(${JSON.stringify(CANONICAL_A)});
              return { changed: true };
            }
          }`,
        }),
        context(sid),
      )
      assert.equal(String(out).includes('changed = true'), true, out)
      programCalls.push(tx)
      for (const callback of terminals) callback(childSession, completedTerminal(childSession))
      return { tag: 0, fields: [] }
    },
  }

  return { port, createCalls, prompts, programCalls }
}

test('CASE006_create_child_once_per_refresh_via_js_bookkeeper', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-session-refresh-'))
  const { port, createCalls, programCalls, prompts } = scriptedBookkeeperPort()
  try {
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal(
      resultOf(
        await archive(store, raw, {
          SessionId: 's-session-refresh',
          Q: 'Q keep',
          A: 'A keep',
          Observations: toList([fileRead('a.txt', hash('hello'))]),
          LastAccessOrder: 0,
        }),
      ).ok,
      true,
    )
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    setSessionPort(port)

    const refreshed = resultOf(await refreshStale(store, raw, dir, 's-session-refresh'))
    assert.equal(refreshed.ok, true, JSON.stringify(refreshed.error))
    assert.equal(refreshed.value, true)
    assert.equal(createCalls.length, 1, 'exactly one CreateChildSession per refresh')
    assert.equal(programCalls.length >= 1, true, 'js-bookkeeper must reshape Q and A in one program')
    assert.equal(prompts.some((text) => String(text).includes('CaseRefresh')), true)

    const fetched = resultOf(await fetchCase(store, raw, 10, 's-session-refresh'))
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.notEqual(fetched.value.Q, 'Q keep')
    assert.equal(fetched.value.A, CANONICAL_A)
    assert.equal(fetched.value.A.includes('evidence:'), false)
  } finally {
    resetSessionPort()
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

    const sessionIdKey = 'insp-session-fin'
    notePrompt(sessionIdKey, 'Who owns PromptAuthority?')
    noteAnswer(sessionIdKey, 'Host owns PromptAuthority.')
    notePrompt(sessionIdKey, 'Where do Case facts live?')
    collect(collector, sessionIdKey, 'read', { path: 'a.txt' }, 'hello')
    noteAnswer(sessionIdKey, 'Unified EventStore only.')

    const first = resultOf(await tryFinalizeInspector(dir, sessionIdKey))
    assert.equal(first.ok, true, JSON.stringify(first.error))
    assert.equal(createCalls.length, 1, 'exactly one CreateChildSession per finalize')
    assert.equal(programCalls.length >= 1, true)
    assert.equal(prompts.some((text) => String(text).includes('CaseFinalize')), true)
    assert.equal(prompts.some((text) => String(text).includes('Q1')), true)
    assert.equal(prompts.some((text) => String(text).includes('Q2')), true)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const fetched = resultOf(await fetchCase(store, raw, 10, sessionIdKey))
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.notEqual(fetched.value.Q, 'Where do Case facts live?')
    assert.equal(fetched.value.A, CANONICAL_A)
    assert.equal(fetched.value.A.includes('evidence:'), false)

    const beforeCleanup = createCalls.length
    const beforeEdits = programCalls.length
    notePrompt(sessionIdKey, 'cleanup Q')
    noteAnswer(sessionIdKey, 'cleanup A')
    cleanupInspector(sessionIdKey)
    assert.equal(createCalls.length, beforeCleanup, 'unexpected cleanup must not CreateChildSession')
    assert.equal(programCalls.length, beforeEdits, 'unexpected cleanup must not call js-bookkeeper')
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE006_missing_session_port_keeps_old_case', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-session-noport-'))
  try {
    resetSessionPort()
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal(
      resultOf(
        await archive(store, raw, {
          SessionId: 's-noport',
          Q: 'Q keep',
          A: 'A keep',
          Observations: toList([fileRead('a.txt', hash('hello'))]),
          LastAccessOrder: 0,
        }),
      ).ok,
      true,
    )
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')

    const refreshed = resultOf(await refreshStale(store, raw, dir, 's-noport'))
    assert.equal(refreshed.ok, false)
    assert.equal(String(refreshed.error).includes('session port'), true)

    const fetched = resultOf(await fetchCase(store, raw, 10, 's-noport'))
    assert.equal(fetched.value.Q, 'Q keep')
    assert.equal(fetched.value.A, 'A keep')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

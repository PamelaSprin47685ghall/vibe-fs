// Split from tests/unit/tools/fork-tool.test.mjs (cutover Wave 2a); owner: capability-enforcement
// Fork tool spec/registry surface and execution-context fail-closed branches:
// tool names + argument schemas, disposed-scope refusal, caller-authority gate,
// and warm-start keyword admission (schema/gate 拒绝 — ENF-006 wording).

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { agentJournal, listItems, sessionId, toList } from '../../verification-system/tests/support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js')
const { managerSpec, orchestratorSpec } = await import('../../../dist/Execution/Delegation/Fork/OpenCode/Tool.js')
const { ToolRuntimeScope } = await import('../../../dist/OpenCode/Tools/ToolRuntimeScope.js')
const { HostForkRuntime, HostForkRuntime__List: listRuntimeAgents } = await import(
  '../../../dist/Execution/Delegation/Fork/Host/Runtime.js'
)

const chain = (kind, extra = {}) => ({
  kind,
  ...extra,
  int: () => chain(`${kind}-int`, extra),
  nonnegative: () => chain(`${kind}-nonnegative`, extra),
  describe: (description) => chain(`${kind}-described`, { ...extra, description }),
  optional: () => chain(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => chain('string'),
  number: () => chain('number'),
  enum: (values) => chain('enum', { values }),
  union: (parts) => chain('union', { parts }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const context = (sessionId = 'ses_fork') =>
  new HostToolContext(sessionId, undefined, undefined, undefined, undefined, () => () => {})

const PARENT = sessionId('ses_fork')

const fakeSessions = (behaviour = {}) => {
  const calls = []
  let childSeq = 0
  return {
    calls,
    CreateChildSession: async (parentId, options) => {
      childSeq += 1
      calls.push(['CreateChildSession', options])
      if (behaviour.createError) return { tag: 1, fields: [behaviour.createError] }
      return { tag: 0, fields: [sessionId(`child-${childSeq}`)] }
    },
    AbortSession: async (id) => {
      calls.push(['AbortSession', id.fields?.[0] ?? id])
      return { tag: 0, fields: [] }
    },
    SendPrompt: async (...args) => {
      calls.push(['SendPrompt', ...args])
      return { tag: 0, fields: [] }
    },
    SendPromptAsync: async (...args) => {
      calls.push(['SendPromptAsync', ...args])
      return { tag: 0, fields: [] }
    },
    SubscribeTerminal: (childId, callback) => {
      calls.push(['SubscribeTerminal', childId])
      return { Dispose: () => {} }
    },
    ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
  }
}

/** { scope, runtime, sessions, journal, cleanup } — real runtime, fake host. */
const liveScope = async (behaviour = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-forktool-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')

  const sessions = fakeSessions(behaviour)
  const runtime = new HostForkRuntime(
    PARENT,
    sessions,
    opened.journal,
    undefined, // onChildCreated
    undefined, // onChildCreatedDir
    undefined, // ptyPort
    undefined, // directoryFor
    undefined, // onRunStarted
    undefined, // parentWorkRecordFor
    undefined, // childWorkRecordFor
    undefined, // sessionSnapshot
    undefined, // cancelSignals
  )

  const scope = new ToolRuntimeScope(
    sessions,
    opened.journal,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    new Map(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )
  scope.runtimes.set('ses_fork', runtime)

  return {
    scope,
    runtime,
    sessions,
    journal: opened.journal,
    cleanup: () => {
      try {
        opened.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

/** A scope with no runtime seeded; an orchestrator host can still be pre-seeded. */
const bareScope = ({ orchestratorHost } = {}) => {
  const scope = new ToolRuntimeScope(
    undefined,
    undefined,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    new Map(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )
  if (orchestratorHost) scope.orchestratorHosts.set('ses_fork', orchestratorHost)
  return scope
}

const runManager = (spec, name, charge, extra = {}) =>
  spec.Execute(makeArgs({ name, charge, ...extra }), context())

test('WHAT[ENF-009] FORK_disposed_scope_surfaces_natural_execution_consequence', async () => {
  const live = await liveScope()
  live.scope.disposed = true
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'Ada', 'do work', { calling: 'coder' })
  assert.doesNotMatch(result, /disposed|\berror\s*=/i)
  assert.match(result, /cannot be placed from this execution context/i)
  live.cleanup()
})

test('WHAT[ENF-010] FORK_orchestrator_missing_authority_is_refused_without_session_identity', async () => {
  const spec = orchestratorSpec(factory, bareScope({ orchestratorHost: {} }))
  const emptyContext = new HostToolContext('', undefined, undefined, undefined, undefined, () => () => {})
  const result = await spec.Execute(
    makeArgs({ calling: 'coordinator', name: 'North Road', charge: 'x' }),
    emptyContext,
  )
  assert.match(result, /caller's authority is established/)
  assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
})

test('WHAT[ENF-009] FORK_specs_expose_expected_names_and_only_manager_fork_carries_keywords', () => {
  const fork = managerSpec(factory, bareScope())
  const commission = orchestratorSpec(factory, bareScope({ orchestratorHost: {} }))
  assert.equal(fork.Name, 'fork')
  assert.equal(commission.Name, 'commission')
  assert.deepEqual(listItems(fork.Arguments).map(([name]) => name), [
    'calling',
    'name',
    'charge',
    'keywords',
    'attach',
    'expected_tool_calls',
  ])
  assert.deepEqual(listItems(commission.Arguments).map(([name]) => name), [
    'calling',
    'name',
    'charge',
    'expected_tool_calls',
  ])
})

test('WHAT[ENF-009] FORK_non_repository_target_rejects_nonempty_warm_start_keywords_before_creation', async () => {
  const live = await liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'Web Road', 'browse', { calling: 'navigator', keywords: 'repository clue' })
  assert.match(result, /only available when fork targets Coder, Inspector, or DevOps/)
  assert.doesNotMatch(result, /\berror\s*=/i)
  assert.equal(listItems(listRuntimeAgents(live.runtime)[0]).length, 0)
  live.cleanup()
})

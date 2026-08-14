// EXEC-005 / EXEC-030 — horizon output must not carry id/status/state-machine DTO.

import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

import {
  blogProjection,
  completionKind,
  handleId,
  handleProjection,
  idValue,
  mapOfEntries,
  roles,
  sessionId,
  structuralComparer,
  toList,
} from '../support/domain.mjs'

const { HostToolContext } = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/HorizonTool.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { HostForkRuntime } = await import('../../../dist/Session/HostForkRuntime.js')
const { SessionAgentProjection } = await import('../../../dist/Journal/AgentProjection.js')
const { CompletionCell$1_$ctor: completionCell } = await import('../../../dist/Session/ChildRun.js')

const FORBIDDEN = /\b(agent_id|session_id|pty_id|child_session_id|status|kind|ordinal|has_pending_completion|current_run_id|fallback_peer|tier|role)\s*=|completed-awaiting-join|running|busy/

const HORIZON_SOURCE = new URL('../../../src/Wanxiangshu/Infrastructure/OpenCode/Tools/HorizonTool.fs', import.meta.url)
const sessionMap = (entries) => mapOfEntries(entries, structuralComparer)
const sha256Hex = (text) => createHash('sha256').update(text, 'utf8').digest('hex')

const context = () => new HostToolContext('ses_horizon', undefined, undefined, undefined, undefined, () => () => {})

const sessionProjection = ({ blog = undefined, handles = undefined } = {}) =>
  new SessionAgentProjection(
    undefined,
    undefined,
    blog,
    undefined,
    handles,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )

const yState = (entries) => {
  let state = blogProjection.empty

  entries.forEach(({ body, ref }, index) => {
    const next = index + 1
    const result = blogProjection.applyEntry(
      {
        epoch: 0,
        previous: index,
        next,
        previousCutoff: index,
        nextCutoff: next,
        digest: `coverage-${next}`,
        frame: blogProjection.frame({ kind: 'Entry', digest: sha256Hex(body), ref }),
      },
      state,
    )
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    state = result.value
  })

  return state
}

const fakeJournal = (handles, childBlogs = [], blobs = new Map()) => ({
  gate: { Enter: () => ({ Exit: () => {} }) },
  writer: {
    BlobWriter: {
      Read: async (ref) => {
        const key = idValue.blobRef(ref)
        return blobs.has(key) ? { tag: 0, fields: [blobs.get(key)] } : { tag: 1, fields: ['missing'] }
      },
    },
  },
  projection: {
    AgentProjections: {
      Sessions: sessionMap([
        [sessionId('ses_horizon'), sessionProjection({ handles })],
        ...childBlogs.map(([child, blog]) => [sessionId(child), sessionProjection({ blog })]),
      ]),
    },
  },
})

const scopeFor = (journal, runtime) => {
  const scope = new ToolRuntimeScope(
    {
      CreateChildSession: async () => ({ tag: 0, fields: [sessionId('child-0')] }),
      AbortSession: async () => ({ tag: 0, fields: [] }),
      SendPrompt: async () => ({ tag: 0, fields: [] }),
      SendPromptAsync: async () => ({ tag: 0, fields: [] }),
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
    },
    journal,
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
  scope.runtimes.set('ses_horizon', runtime)
  return scope
}

const runtimeWithAgent = () => {
  const runtime = new HostForkRuntime(
    sessionId('ses_horizon'),
    {
      CreateChildSession: async () => ({ tag: 0, fields: [sessionId('child-0')] }),
      AbortSession: async () => ({ tag: 0, fields: [] }),
      SendPrompt: async () => ({ tag: 0, fields: [] }),
      SendPromptAsync: async () => ({ tag: 0, fields: [] }),
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
    },
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )
  runtime.runtime.agents = mapOfEntries([
    [
      'ag-1',
      {
        AgentId: 'ag-1',
        AgentName: 'fast-coder',
        Prompt: 'work',
        Completion: completionCell(),
        Cancellation: { IsCancellationRequested: () => false },
        CreatedAt: new Date(),
      },
    ],
  ])
  return runtime
}

test('EXEC_005_horizon_description_says_work_record_and_pull_only_without_Y_jargon', () => {
  const description = spec(scopeFor(fakeJournal(handleProjection.empty), runtimeWithAgent())).Description
  assert.match(description, /latest work record/i)
  assert.match(description, /pull-only/i)
  assert.match(description, /do not poll/i)
  assert.doesNotMatch(description, /\bY\s+work record\b/i)
})

test('HORIZON_SURFACE_has_no_legacy_roster_dto', async () => {
  const handles = handleProjection.link(
    handleId.agent('ag-1'),
    sessionId('child-1'),
    'fast-coder',
    roles.of('Coder'),
    handleProjection.empty,
  ).value

  const text = await spec(scopeFor(fakeJournal(handles), runtimeWithAgent())).Execute({}, context())
  assert.match(text, /# fast-coder is still away\./)
  assert.ok(!FORBIDDEN.test(text), text)
})

test('EXEC_005_horizon_shows_only_each_visible_subagent_latest_work_record', async () => {
  const firstBody = 'Investigated the parser and found the old edge case.'
  const latestBody = 'Patched the parser and the focused regression is green.'
  const secondBody = 'Mapped the release boundary and found no remaining blocker.'
  const blobs = new Map([
    ['frame-old', firstBody],
    ['frame-latest', latestBody],
    ['frame-second', secondBody],
  ])
  const firstBlog = yState([
    { body: firstBody, ref: 'frame-old' },
    { body: latestBody, ref: 'frame-latest' },
  ])
  const secondBlog = yState([{ body: secondBody, ref: 'frame-second' }])
  const firstHandle = handleProjection.link(
    handleId.agent('ag-1'),
    sessionId('child-1'),
    'fast-coder',
    roles.of('Coder'),
    handleProjection.empty,
  ).value
  const handles = handleProjection.link(
    handleId.agent('ag-2'),
    sessionId('child-2'),
    'deep-inquiry',
    roles.of('Inquiry'),
    firstHandle,
  ).value

  const text = await spec(
    scopeFor(
      fakeJournal(
        handles,
        [
          ['child-1', firstBlog],
          ['child-2', secondBlog],
        ],
        blobs,
      ),
      runtimeWithAgent(),
    ),
  ).Execute({}, context())

  assert.match(text, /latest work record/i)
  assert.match(text, /Patched the parser and the focused regression is green\./)
  assert.match(text, /Mapped the release boundary and found no remaining blocker\./)
  assert.doesNotMatch(text, /Investigated the parser and found the old edge case\./)
})

test('EXEC_005_horizon_says_when_visible_subagent_has_no_work_record', async () => {
  const handles = handleProjection.link(
    handleId.agent('ag-1'),
    sessionId('child-1'),
    'fast-coder',
    roles.of('Coder'),
    handleProjection.empty,
  ).value

  const text = await spec(scopeFor(fakeJournal(handles, [['child-1', blogProjection.empty]]), runtimeWithAgent())).Execute({}, context())
  assert.match(text, /fast-coder has no work record yet\./i)
})

test('EXEC_005_horizon_does_not_fall_back_when_latest_work_record_is_unreadable', async () => {
  const oldBody = 'Old record that must not masquerade as current progress.'
  const latestBody = 'Newest record whose blob is unavailable.'
  const blog = yState([
    { body: oldBody, ref: 'frame-old' },
    { body: latestBody, ref: 'frame-latest-missing' },
  ])
  const handles = handleProjection.link(
    handleId.agent('ag-1'),
    sessionId('child-1'),
    'fast-coder',
    roles.of('Coder'),
    handleProjection.empty,
  ).value
  const blobs = new Map([['frame-old', oldBody]])

  const text = await spec(scopeFor(fakeJournal(handles, [['child-1', blog]], blobs), runtimeWithAgent())).Execute({}, context())

  assert.match(text, /latest work record cannot be read right now/i)
  assert.doesNotMatch(text, /Old record that must not masquerade as current progress\./)
})

test('EXEC_005_horizon_has_no_polling_or_background_wait_primitive', async () => {
  const source = await readFile(HORIZON_SOURCE, 'utf8')
  assert.doesNotMatch(source, /AwaitChangeFrom|Task\.Delay|setInterval|setTimeout|System\.Timers|PeriodicTimer/)
})

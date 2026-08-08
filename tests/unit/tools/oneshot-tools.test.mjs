// tests/unit/tools/oneshot-tools.test.mjs — VERIFY-009 coverage: coder /
// inspector one-shot tools over the real OneShotAgentTool lifecycle.
//
// Real AgentJournal (PROMPT-005 dispatch claims the prompt) + fake
// ISessionHostPort. The terminal subscription is installed BEFORE the prompt
// send (production order), so the fake fires the terminal from inside
// SendPrompt — the same interleaving the Host produces.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { agentJournal, listItems, sessionId } from '../support/domain.mjs'
import { uncurry2 } from '../../../dist/fable_modules/fable-library-js.5.13.0/Util.js'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec: coderSpec } = await import('../../../dist/Infrastructure/OpenCode/Tools/CoderTool.js')
const { spec: inspectorSpec } = await import('../../../dist/Infrastructure/OpenCode/Tools/InspectorTool.js')
const { ToolRuntimeScope, ToolRuntimeScope__DirectoryFor_Z721C83C5: directoryFor } = await import(
  '../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js'
)
const { TerminalOutcome } = await import('../../../dist/Infrastructure/OpenCode/Host/Events.js')
const { AgentRunResult } = await import('../../../dist/Kernel/Outcome.js')

const chain = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => chain(`${kind}-described`, extra),
  optional: () => chain(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => chain('string'),
  enum: (values) => chain('enum', { values }),
  array: (inner) => chain('array', { inner }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const context = (session = 'ses-call', attachAbort) =>
  new HostToolContext(session, undefined, undefined, undefined, undefined, attachAbort ?? (() => () => {}))

const parseToml = (text) =>
  Object.fromEntries(
    text
      .split('\n')
      .filter((line) => /^[a-z_0-9]+ = /.test(line))
      .map((line) => {
        const [name, ...rest] = line.split(' = ')
        const raw = rest.join(' = ')
        return [name, raw.startsWith('"') ? JSON.parse(raw) : raw]
      }),
  )

/** Fake ISessionHostPort capturing the terminal subscription and every call.
 * The Host's SubscribeTerminal is a multi-listener bus: the PromptDispatcher
 * installs a NoOp listener per send, so the fake must keep ALL callbacks. */
const fakeSessions = ({ createError } = {}) => {
  const calls = { create: 0, abort: 0, prompt: [], disposedSub: 0 }
  const terminals = new Set()
  return {
    calls,
    fireTerminal: (outcome) => {
      for (const callback of terminals) callback(sessionId('child-1'), outcome)
    },
    CreateChildSession: async (_parentId, _options) => {
      calls.create += 1
      if (createError) return { tag: 1, fields: [createError] }
      return { tag: 0, fields: [sessionId('child-1')] }
    },
    AbortSession: async () => {
      calls.abort += 1
      return { tag: 0, fields: [] }
    },
    SendPrompt: async (...args) => {
      calls.prompt.push(args)
      return { tag: 0, fields: [] }
    },
    SubscribeTerminal: (_childId, callback) => {
      terminals.add(callback)
      return {
        Dispose: () => {
          terminals.delete(callback)
          calls.disposedSub += 1
        },
      }
    },
  }
}

const completedTerminal = (formalText) =>
  new TerminalOutcome(0, [
    new AgentRunResult(
      sessionId('child-1'),
      undefined,
      undefined,
      { tag: 2, fields: [] },
      undefined,
      'session-wide text',
      formalText,
    ),
  ])

/** { scope, sessions, cleanup } — real journal + fake host. */
const liveScope = ({ sessions = fakeSessions(), parentWorkRecord, directories } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-oneshot-'))
  const opened = agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')

  const scope = new ToolRuntimeScope(
    sessions,
    opened.journal,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    directories ?? new Map(),
    undefined,
    parentWorkRecord ? () => parentWorkRecord : undefined,
    undefined,
    undefined,
    undefined,
  )
  return {
    scope,
    sessions,
    cleanup: () => {
      try {
        opened.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

/** Scope without a journal: the one-shot prompt cannot be claimed. */
const bareScope = ({ sessions = fakeSessions() } = {}) =>
  new ToolRuntimeScope(
    sessions,
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

// ── spec surface ─────────────────────────────────────────────────────────────

test('COD_spec_exposes_agent_tdd_and_prompt_arguments', () => {
  const tool = coderSpec(factory, bareScope())
  assert.equal(tool.Name, 'coder')
  assert.match(tool.Description, /tdd=red\|green/)
  const args = listItems(tool.Arguments).map(([name]) => name)
  assert.deepEqual(args, ['agent', 'tdd', 'prompt', 'prompts'])
})

test('INSPECTOR_spec_exposes_agent_and_prompt_arguments', () => {
  const tool = inspectorSpec(factory, bareScope())
  assert.equal(tool.Name, 'inspector')
  assert.match(tool.Description, /read-only investigation/)
  const args = listItems(tool.Arguments).map(([name]) => name)
  assert.deepEqual(args, ['agent', 'prompt', 'prompts'])
})

// ── validation order (no spawn on any refusal) ───────────────────────────────

test('COD_blank_session_is_refused_before_spawn', async () => {
  const sessions = fakeSessions()
  const result = parseToml(
    await coderSpec(factory, bareScope({ sessions })).Execute(
      makeArgs({ agent: 'fast-coder', tdd: 'red', prompt: 'work' }),
      context(''),
    ),
  )
  assert.equal(result.error, 'Missing sessionID')
  assert.equal(sessions.calls.create, 0)
})

test('INSPECTOR_missing_prompt_is_refused', async () => {
  const result = parseToml(
    await inspectorSpec(factory, bareScope()).Execute(makeArgs({ agent: 'fast-inspector' }), context()),
  )
  assert.equal(result.error, 'inspector prompt required')
})

test('COD_missing_tdd_phase_is_rejected_before_spawn', async () => {
  const sessions = fakeSessions()
  const result = parseToml(
    await coderSpec(factory, bareScope({ sessions })).Execute(
      makeArgs({ agent: 'fast-coder', prompt: 'work' }),
      context(),
    ),
  )
  assert.match(result.error, /tdd|TDD|red|green/i)
  assert.equal(sessions.calls.create, 0)
})

test('COD_invalid_tdd_phase_is_rejected_before_spawn', async () => {
  const sessions = fakeSessions()
  const result = parseToml(
    await coderSpec(factory, bareScope({ sessions })).Execute(
      makeArgs({ agent: 'fast-coder', tdd: 'blue', prompt: 'work' }),
      context(),
    ),
  )
  assert.match(result.error, /tdd|TDD|red|green/i)
  assert.equal(sessions.calls.create, 0)
})

test('COD_missing_agent_names_the_expected_agents', async () => {
  const result = parseToml(
    await coderSpec(factory, bareScope()).Execute(makeArgs({ tdd: 'red', prompt: 'work' }), context()),
  )
  assert.equal(result.error, 'agent is required; use fast-coder or deep-coder')
})

test('COD_wrong_agent_for_the_tool_is_explained', async () => {
  const result = parseToml(
    await coderSpec(factory, bareScope()).Execute(
      makeArgs({ agent: 'fast-inspector', tdd: 'red', prompt: 'work' }),
      context(),
    ),
  )
  assert.equal(result.error, 'Coder tool requires agent fast-coder or deep-coder')
})

test('INSPECTOR_wrong_agent_for_the_tool_is_explained', async () => {
  const result = parseToml(
    await inspectorSpec(factory, bareScope()).Execute(
      makeArgs({ agent: 'fast-coder', prompt: 'work' }),
      context(),
    ),
  )
  assert.equal(result.error, 'Inspector tool requires agent fast-inspector or deep-inspector')
})

test('COD_malformed_agent_name_reports_parse_error', async () => {
  const result = parseToml(
    await coderSpec(factory, bareScope()).Execute(
      makeArgs({ agent: 'codr!', tdd: 'red', prompt: 'work' }),
      context(),
    ),
  )
  assert.match(result.error, /Malformed managed agent name 'codr!'/)
})

test('COD_unknown_managed_agent_gets_a_suggestion', async () => {
  const result = parseToml(
    await coderSpec(factory, bareScope()).Execute(
      makeArgs({ agent: 'fast-codr', tdd: 'red', prompt: 'work' }),
      context(),
    ),
  )
  assert.match(result.error, /Unknown managed agent 'fast-codr'/)
})

test('COD_create_session_failure_surfaces_host_error', async () => {
  const live = liveScope({ sessions: fakeSessions({ createError: 'host refused' }) })
  const result = parseToml(
    await coderSpec(factory, live.scope).Execute(
      makeArgs({ agent: 'fast-coder', tdd: 'red', prompt: 'work' }),
      context(),
    ),
  )
  assert.equal(result.error, 'host refused')
  live.cleanup()
})

// ── full lifecycle ───────────────────────────────────────────────────────────

test('COD_success_reports_outcome_and_disposes_the_child', async () => {
  const sessions = fakeSessions()
  const live = liveScope({ sessions })
  const tool = coderSpec(factory, live.scope)

  const pending = tool.Execute(makeArgs({ agent: 'fast-coder', tdd: 'red', prompt: 'implement it' }), context())
  // The prompt send is the readiness signal: the terminal subscription is up.
  for (let attempt = 0; attempt < 100 && sessions.calls.prompt.length === 0; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, 5))
  }
  assert.equal(sessions.calls.prompt.length, 1, 'the child prompt must be sent')
  sessions.fireTerminal(completedTerminal('the formal report'))

  const text = await pending
  const result = parseToml(text)
  assert.equal(result.coder_id, 'child-1')
  assert.equal(result.agent, 'fast-coder')
  assert.equal(result.tier, 'fast')
  assert.equal(result.fallback_peer, 'deep-coder')
  assert.equal(result.tdd, 'red')
  assert.equal(result.parent_b_digest, '')
  // COMPANION-005: the report is the turn-formal text, not the session-wide text.
  assert.match(text, /the formal report/)
  assert.equal(sessions.calls.abort, 1, 'the child is physically aborted after the terminal')
  // The PromptDispatcher installs and disposes its own NoOp terminal listener per
  // send, so the count covers both the tool's subscription and the dispatcher's.
  assert.ok(sessions.calls.disposedSub >= 1, 'the terminal subscription is disposed')
  live.cleanup()
})

test('COD_green_phase_and_prompts_array_compose_the_assignment', async () => {
  const sessions = fakeSessions()
  const live = liveScope({ sessions })
  const tool = coderSpec(factory, live.scope)

  const pending = tool.Execute(
    makeArgs({ agent: 'deep-coder', tdd: 'green', prompts: ['first part', 'second part'] }),
    context(),
  )
  for (let attempt = 0; attempt < 100 && sessions.calls.prompt.length === 0; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, 5))
  }
  sessions.fireTerminal(completedTerminal('done'))

  const result = parseToml(await pending)
  assert.equal(result.agent, 'deep-coder')
  assert.equal(result.tdd, 'green')
  const sentText = JSON.stringify(sessions.calls.prompt)
  assert.match(sentText, /# first part\\n# second part/, 'prompts join with newlines under the relay envelope')
  live.cleanup()
})

test('COD_parent_work_record_lands_in_the_digest_field', async () => {
  const sessions = fakeSessions()
  const live = liveScope({ sessions, parentWorkRecord: 'the parent background record' })
  const tool = coderSpec(factory, live.scope)

  const pending = tool.Execute(makeArgs({ agent: 'fast-coder', tdd: 'red', prompt: 'work' }), context())
  for (let attempt = 0; attempt < 100 && sessions.calls.prompt.length === 0; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, 5))
  }
  sessions.fireTerminal(completedTerminal('report'))

  const result = parseToml(await pending)
  assert.ok(result.parent_b_digest.length > 0, 'a parent work record must produce a digest')
  live.cleanup()
})

test('COD_child_inherits_the_parent_directory', async () => {
  const sessions = fakeSessions()
  const live = liveScope({ sessions, directories: new Map([['ses-call', '/tmp']]) })
  const tool = coderSpec(factory, live.scope)

  const pending = tool.Execute(makeArgs({ agent: 'fast-coder', tdd: 'red', prompt: 'work' }), context())
  for (let attempt = 0; attempt < 100 && sessions.calls.prompt.length === 0; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, 5))
  }
  sessions.fireTerminal(completedTerminal('report'))
  await pending

  assert.equal(directoryFor(live.scope, 'child-1'), '/tmp', 'the child directory is registered')
  live.cleanup()
})

test('COD_send_failure_is_reported_as_output_not_thrown', async () => {
  const sessions = fakeSessions()
  const tool = coderSpec(factory, bareScope({ sessions }))

  const pending = tool.Execute(makeArgs({ agent: 'fast-coder', tdd: 'red', prompt: 'work' }), context())
  for (let attempt = 0; attempt < 100 && sessions.calls.create === 0; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, 5))
  }
  // No journal → the prompt claim fails; the tool still completes the one-shot.
  const text = await pending
  assert.match(text, /send failed: No journal/)
  assert.equal(sessions.calls.abort, 1, 'the child is still physically aborted')
  live0: void 0
})

test('COD_aborted_terminal_surfaces_an_error', async () => {
  const sessions = fakeSessions()
  const live = liveScope({ sessions })
  const tool = coderSpec(factory, live.scope)

  const pending = tool.Execute(makeArgs({ agent: 'fast-coder', tdd: 'red', prompt: 'work' }), context())
  for (let attempt = 0; attempt < 100 && sessions.calls.prompt.length === 0; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, 5))
  }
  sessions.fireTerminal(new TerminalOutcome(1, ['operator killed it']))

  await assert.rejects(pending, /Coder aborted: operator killed it/)
  live.cleanup()
})

test('COD_failed_terminal_surfaces_an_error', async () => {
  const sessions = fakeSessions()
  const live = liveScope({ sessions })
  const tool = coderSpec(factory, live.scope)

  const pending = tool.Execute(makeArgs({ agent: 'fast-coder', tdd: 'red', prompt: 'work' }), context())
  for (let attempt = 0; attempt < 100 && sessions.calls.prompt.length === 0; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, 5))
  }
  sessions.fireTerminal(new TerminalOutcome(2, ['provider exploded']))

  await assert.rejects(pending, /Coder failed: provider exploded/)
  live.cleanup()
})

test('COD_parent_abort_completes_as_cancelled_and_aborts_the_child', async () => {
  const sessions = fakeSessions()
  const live = liveScope({ sessions })
  const tool = coderSpec(factory, live.scope)

  let cancelParent
  // HostToolContext.AttachAbort is compiled as an uncurried pair with a curry
  // lookup (decodeContext uses uncurry2); a plain function field would defer
  // registration to the detach call. uncurry2 makes the register path immediate.
  const attachAbort = uncurry2((cancel) => {
    cancelParent = cancel
    return () => {}
  })
  const pending = tool.Execute(
    makeArgs({ agent: 'fast-coder', tdd: 'red', prompt: 'work' }),
    context('ses-call', attachAbort),
  )
  for (let attempt = 0; attempt < 100 && cancelParent === undefined; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, 5))
  }
  cancelParent()

  const text = await pending
  assert.match(text, /aborted: parent cancelled/)
  assert.ok(sessions.calls.abort >= 1, 'the child session is aborted')
  live.cleanup()
})

test('INSPECTOR_success_reports_outcome_without_a_tdd_field', async () => {
  const sessions = fakeSessions()
  const live = liveScope({ sessions })
  const tool = inspectorSpec(factory, live.scope)

  const pending = tool.Execute(makeArgs({ agent: 'fast-inspector', prompt: 'read the code' }), context())
  for (let attempt = 0; attempt < 100 && sessions.calls.prompt.length === 0; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, 5))
  }
  sessions.fireTerminal(completedTerminal('inspector findings'))

  const text = await pending
  const result = parseToml(text)
  assert.equal(result.inspector_id, 'child-1')
  assert.equal(result.agent, 'fast-inspector')
  assert.equal(result.fallback_peer, 'deep-inspector')
  assert.equal(result.tdd, undefined, 'inspector reports no tdd field')
  assert.match(text, /inspector findings/)
  live.cleanup()
})

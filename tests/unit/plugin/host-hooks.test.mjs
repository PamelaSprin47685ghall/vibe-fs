// tests/unit/Plugin/host-hooks.test.mjs — HOST-009, VERIFY-008.
//
// Every hook the plugin registers must be callable the way the Host calls it:
//
//   packages/opencode/src/plugin/index.ts:290
//     yield* Effect.promise(async () => fn(input, output))
//
// One positional call, two arguments. ARCH-003 forbids changing the Host, so this
// is a contract the plugin has to satisfy rather than negotiate.
//
// ── why this file exists ────────────────────────────────────────────────────
//
// Three of the five hooks threw on their first call. `dotnet build` was green and
// every layer 1 test passed, because the mismatch lived in an `[<Emit>]` template
// rather than in F#:
//
//   [<Emit("(args, context) => $0(args)(context)")>]     assumes a curried chain
//
// Fable emits a curried chain for an `obj`-typed record field or a partial
// application, and a TWO-ARITY arrow for a plain two-parameter `let`. Applying the
// curried template to a two-arity arrow calls it with one argument, so the body ran
// with `output = undefined`:
//
//   experimental.chat.messages.transform   Cannot read properties of undefined (reading 'messages')
//   experimental.session.compacting        (intermediate value)(...) is not a function
//   experimental.compaction.autocontinue   Cannot set properties of undefined (setting 'enabled')
//
// `prompt.ts:1255` triggers the transform on every provider step, so the plugin
// threw on every turn of every session. The F# compiler cannot see inside an emit
// template, and `domain.mjs` never imports `Infrastructure/OpenCode/*` — neither side had a reason
// to call a hook.

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { withPlugin, withPluginClient } from './plugin-fixture.mjs'

const SESSION = 'ses_hook_probe'

/** AGENT-002/003: the Host-final `opencode.json` names every managed agent. */
const ROLES = [
  'orchestrator',
  'manager',
  'coder',
  'inspector',
  'devops',
  'browser',
  'meditator',
  'reviewer',
  'blogger',
  'executor',
]

const hostFinalConfig = () => {
  const agent = {}
  for (const role of ROLES) {
    for (const tier of ['fast', 'deep']) {
      agent[`${tier}-${role}`] = { model: `provider/${tier}-${role}-model` }
    }
  }
  return { agent }
}

/**
 * One fixture per hook, plus a completeness gate below.
 *
 * A single lowest-common-denominator input would be worse than no test: it would
 * either be too empty to exercise the argument that went missing, or — as measured
 * while writing this — drive `chat.message` into real child-session creation and
 * leak async work past the end of the test.
 *
 * `assert` runs after the call, so a hook that accepted both arguments and then did
 * nothing with them still fails.
 */
const HOOK_FIXTURES = {
  // PROMPT-005: a physical user message arriving from the Host. `parts` empty, so
  // ingress finds nothing to claim and forks no child.
  'chat.message': {
    input: { sessionID: SESSION },
    output: { message: { id: 'msg_probe', role: 'user', sessionID: SESSION }, parts: [] },
  },

  'chat.params': {
    input: { sessionID: SESSION },
    output: {},
  },

  // COMPANION-005 / CTX-002: with no committed prefix snapshot, X sees raw history,
  // so an empty view stays empty rather than gaining a synthetic memory head.
  'experimental.chat.messages.transform': {
    input: { sessionID: SESSION },
    output: { messages: [] },
    assert: (output) => assert.deepEqual(output.messages, [], 'no snapshot means raw history'),
  },

  // HOST-006 containment: this hook has no cancel field, so the plugin can only
  // observe. It must not throw — a throw here escapes into a Host callback.
  'experimental.session.compacting': {
    input: { sessionID: SESSION },
    output: { context: '', prompt: undefined },
  },

  // HOST-006 prevention: written into the output object. Losing that argument turned
  // "close autocontinue" into an unobservable no-op — the prevention layer would
  // silently not exist.
  'experimental.compaction.autocontinue': {
    input: { sessionID: SESSION },
    output: { enabled: true },
    assert: (output) => assert.equal(output.enabled, false, 'HOST-006 requires autocontinue closed'),
  },

  // The odd one out: `config` receives the live instance-state object ALONE and the
  // plugin writes agent prompts into it. AGENT-004/005 fail closed on a config with
  // no agent map, so the fixture has to be Host-final.
  config: {
    input: hostFinalConfig(),
    output: undefined,
    assert: (_output, input) => {
      assert.equal(typeof input.agent['fast-manager'].prompt, 'string')
      assert.equal(typeof input.agent['fast-coder'].prompt, 'string')
    },
  },
}

const toolContext = (sessionID, messageID = 'msg_tool_probe') => ({
  sessionID,
  agent: 'fast-manager',
  messageID,
  callID: `call_${messageID}`,
  abort: new AbortController().signal,
})

test('PROMPT_004_human_root_survives_host_synthetic_file_parts', async () => {
  await withPlugin(async (hooks) => {
    await hooks['chat.message'](
      { sessionID: SESSION, agent: 'fast-manager' },
      {
        message: { id: 'msg_file_root', role: 'user', sessionID: SESSION, agent: 'fast-manager' },
        parts: [
          { type: 'text', synthetic: true, text: 'Called the Read tool with the following input: {"filePath":"spec/13.md"}' },
          { type: 'text', synthetic: true, text: '# document body' },
          { type: 'file', mime: 'text/plain', filename: 'spec/13.md', url: 'file:///repo/spec/13.md' },
        ],
      },
    )

    const listResult = parseToml(await hooks.tool.list.execute({}, toolContext(SESSION)))
    assert.deepEqual(listResult, {})
    assert.equal('item' in listResult, false)
  })
})

test('AGENT_007_tool_gate_recovers_human_root_from_host_snapshot_on_resume', async () => {
  const sessionID = 'ses_resume_probe'
  const rootID = 'msg_resume_root'
  const assistantID = 'msg_resume_assistant'
  const client = {
    session: {
      messages: async () => ({
        data: [
          { info: { id: rootID, role: 'user', sessionID, agent: 'fast-manager' }, parts: [{ type: 'text', text: 'fork a coder' }] },
          { info: { id: assistantID, role: 'assistant', sessionID, parentID: rootID, agent: 'fast-manager' }, parts: [] },
        ],
      }),
    },
  }

  await withPluginClient(client, async (hooks) => {
    const listResult = parseToml(await hooks.tool.list.execute({}, toolContext(sessionID, assistantID)))
    assert.deepEqual(listResult, {})
    assert.equal('item' in listResult, false)
  })
})

/** Hooks the Host triggers. `tool` is a registry; `event`/`dispose` are lifecycle. */
const triggeredHooks = (hooks) =>
  Object.entries(hooks).filter(
    ([name, value]) => typeof value === 'function' && name !== 'event' && name !== 'dispose',
  )

test('HOST_009_every_registered_hook_has_a_fixture_here', async () => {
  // The completeness gate. Without it a newly registered hook would be silently
  // uncovered, which is exactly how the transform family went unchecked.
  await withPlugin(async (hooks) => {
    const registered = triggeredHooks(hooks)
      .map(([name]) => name)
      .sort()

    assert.deepEqual(
      registered,
      Object.keys(HOOK_FIXTURES).sort(),
      'a hook without a fixture is an uncovered Host entry point',
    )
  })
})

test('HOST_009_every_hook_accepts_its_arguments_positionally', async () => {
  await withPlugin(async (hooks) => {
    const failures = []

    for (const [name, fixture] of Object.entries(HOOK_FIXTURES)) {
      try {
        await hooks[name](fixture.input, fixture.output)
        fixture.assert?.(fixture.output, fixture.input)
      } catch (error) {
        failures.push(`${name}: ${error.message ?? JSON.stringify(error)}`)
      }
    }

    assert.deepEqual(failures, [], 'every hook must accept the Host call shape and act on both arguments')
  })
})

test('HOST_009_the_tool_registry_is_a_registry_not_a_triggered_hook', async () => {
  // `tool` holds `{ args, execute }` records the Host calls per tool. Sweeping it
  // into the loop above would call a registry object as a function.
  await withPlugin(async (hooks) => {
    assert.equal(typeof hooks.tool, 'object')

    const toolNames = Object.keys(hooks.tool).sort()
    assert.deepEqual(toolNames, [
      'blog',
      'coder',
      'executor',
      'fork',
      'fork-manager',
      'fork-pty',
      'inspector',
      'join',
      'list',
      'mv',
      'rm',
      'verdict',
    ])

    for (const name of toolNames) {
      assert.equal(typeof hooks.tool[name].execute, 'function', `${name} must expose execute`)
    }
  })
})

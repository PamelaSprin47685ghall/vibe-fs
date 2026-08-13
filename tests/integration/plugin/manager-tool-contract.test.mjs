// tests/unit/Plugin/manager-tool-contract.test.mjs — AGENT-004/006/009/010, CTX-002.
//
// Layer 2 (resource contract): what the Host sees after `initSpikePlugin` — the tool
// registry, the argument schemas the provider is offered, and the `opencode.json`
// mutation `hooks.config` performs. No mock provider, no HTTP server, no port or
// HOME/XDG isolation; a `git init` into a `mkdtemp` dir is the whole world, because
// the journal is addressed through the Git common directory (PERSIST-006).
//
// The plugin entry (`Infrastructure/OpenCode/Plugin/SpikePlugin.js`) is imported directly rather than through
// `tests/unit/domain.mjs`. That facade deliberately exports zero `Infrastructure/OpenCode/*` modules,
// and the schemas here are not F# values at all: `ToolHostCodec.fs:78-96` emits
// `$0.schema.string()` / `$0.schema.union([...])` against the Host's own zod builder,
// so only a real `initSpikePlugin({ client: {} , ... })` produces them. A direct
// import of the plugin entry is the same precedent `host-hooks.test.mjs` sets.
//
// `domain.mjs` is imported for `roles.permissions` alone, as an independent second
// source for the permission matrix — see the cross-check note below.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync } from 'node:fs'
import { isAbsolute, join, resolve } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { agentJournal, handleId, handleProjection, idValue, roles, sessionId } from '../../unit/support/domain.mjs'
import { RuntimeResourcesModule_current as currentRuntimeResources } from '../../../dist/Infrastructure/Resources/RuntimeResources.js'
import {
  withPlugin,
  withExecutablePlugin,
  acceptAuthorityRoot,
  acceptChildAgentOwnerRoot,
  notifyCompleted,
  activateLife,
  acceptFirstTodoWrite,
} from '../../unit/plugin/plugin-fixture.mjs'

/** AGENT-002: Role-backed managed agents (Bookkeeper pair added in hostFinalConfig). */
const ROLE_NAMES = [
  'orchestrator',
  'manager',
  'coder',
  'inspector',
  'devops',
  'browser',
  'inquiry',
  'reviewer',
  'blogger',
  'distiller',
]

const hostFinalConfig = () => {
  const agent = {}
  for (const role of ROLE_NAMES) {
    for (const tier of ['fast', 'deep']) {
      agent[`${tier}-${role}`] = { model: `provider/${tier}-${role}-model` }
    }
  }
  for (const tier of ['fast', 'deep']) {
    agent[`${tier}-bookkeeper`] = { model: `provider/${tier}-bookkeeper-model` }
  }
  return { agent }
}

// ── the model-visible tool surface ───────────────────────────────────────────

/** Every argument of every tool, so a new or renamed argument fails here first. */
const EXPECTED_ARGUMENTS = {
  'auto-injected': {},
  'bash-honeypot': {},
  chronicle: {
    entry: 'required',
    tip: 'required',
  },
  'establish-behavior': { charge: 'required', keywords: 'optional' },
  'repair-behavior': { charge: 'required', keywords: 'optional' },
  run: {
    command: 'required',
    deadline_seconds: 'required',
    output_budget_bytes: 'required',
    world_lock: 'required',
  },
  'query-shell': {
    command: 'required',
  },
  fork: { calling: 'optional', name: 'required', charge: 'required', keywords: 'optional' },
  commission: { calling: 'optional', name: 'required', charge: 'required' },
  'open-terminal': { name: 'required', command: 'required' },
  'send-terminal': { name: 'required', input: 'required' },
  'read-terminal': { name: 'required' },
  'signal-terminal': { name: 'required', signal: 'required' },
  inspect: { charge: 'required', keywords: 'optional' },
  join: {},
  'js-browser': { program: 'required' },
  'js-coder': { program: 'required' },
  'js-devops': { program: 'required' },
  'js-inspector': { program: 'required' },
  'js-reviewer': { program: 'required' },
  horizon: {},
  fission: { prompts: 'required' },
  mv: { source: 'required', destination: 'required' },
  rm: { path: 'required' },
  suicide: { last_words: 'required' },
  judge: { verdict: 'required' },
}

/**
 * AGENT-009: the agents each schema advertises, and AGENT-008: the two internal
 * agents that must never appear in one.
 *
 * The original assertion was `assert.match(JSON.stringify(schema.def), /fast-coder/)`
 * plus a handful of `doesNotMatch`. That answers "is this substring somewhere in the
 * serialized schema" — it cannot see an agent nobody thought to forbid. Reading the
 * enum entries out and comparing the whole sorted set does, and it subsumes every
 * `doesNotMatch` line at the same time.
 */
const EXPECTED_AGENT_ENUMS = {
  fork: [
    'analyst',
    'coder',
    'engineer',
    'inquirer',
    'investigator',
    'navigator',
    'operator',
    'researcher',
    'scout',
    'technician',
  ],
  commission: ['coordinator', 'lead'],
}

/**
 * The enum arm of an agent argument, whether or not it is wrapped in a union.
 *
 * `fork.name` is `union([enum(...), string()])` while `commission.name` is a bare
 * enum (`ToolHostCodec.fs:90` vs `:78`). Measured consequence worth stating: the
 * string arm makes `fork.name.safeParse('garbage')` SUCCEED, so this enum is a
 * provider-visible offer, not a validator. Rejecting an unknown agent happens inside
 * `execute` — which is the part of the original file that has never passed and is
 * recorded as a pending defect rather than migrated.
 */
const agentEnumEntries = (schema) => {
  const visit = (node) => {
    if (!node) return []
    const def = node.def ?? node._def
    if (!def) return []
    if (def.type === 'enum') return Object.keys(def.entries)
    if (def.type === 'union') return def.options.flatMap(visit)
    if (def.type === 'optional') return visit(def.innerType)
    return []
  }

  return visit(schema).sort()
}

// ── the permission matrix ────────────────────────────────────────────────────

/**
 * Every key `StaticTools.permissionObj` emits. Pinned here as a literal rather than
 * read back off the emitted object: deriving the expectation from `Object.keys(actual)`
 * would make a renamed key agree with itself.
 */
const KNOWN_TOOL_KEYS = [
  '*',
  'external_directory',
  'fork',
  'commission',
  'open-terminal',
  'send-terminal',
  'read-terminal',
  'signal-terminal',
  'join',
  'horizon',
  'todowrite',
  'fission',
  'read',
  'write',
  'edit',
  'glob',
  'grep',
  'mv',
  'rm',
  'bash-honeypot',
  'auto-injected',
  'inspect',
  'establish-behavior',
  'repair-behavior',
  'run',
  'query-shell',
  'sphinx_*',
  'stealth-browser-mcp_*',
  'judge',
  'chronicle',
  'fetch',
  'suicide',
  'js-manager',
  'js-orchestrator',
  'js-coder',
  'js-inspector',
  'js-browser',
  'js-inquiry',
  'js-reviewer',
  'js-devops',
  'js-distiller',
  'js-blogger',
  'js-bookkeeper',
]

/** AGENT-006/011/013/014/015: the allowed tools per role. Everything else denies. */
const ALLOWED_TOOLS = {
  orchestrator: ['commission', 'join', 'horizon', 'auto-injected'],
  manager: ['fork', 'join', 'horizon', 'todowrite', 'fission', 'suicide', 'auto-injected'],
  coder: ['read', 'write', 'edit', 'glob', 'grep', 'inspect', 'fetch', 'mv', 'rm', 'bash-honeypot', 'auto-injected'],
  inspector: ['read', 'glob', 'grep', 'query-shell', 'fetch', 'auto-injected'],
  devops: [
    'open-terminal',
    'send-terminal',
    'read-terminal',
    'signal-terminal',
    'join',
    'horizon',
    'read',
    'glob',
    'grep',
    'inspect',
    'establish-behavior',
    'repair-behavior',
    'run',
    'auto-injected',
  ],
  browser: ['read', 'glob', 'grep', 'stealth-browser-mcp_*', 'auto-injected'],
  inquiry: ['inspect', 'sphinx_*', 'auto-injected'],
  reviewer: ['read', 'glob', 'grep', 'judge', 'auto-injected'],
  // ENFORCER-010: Blogger's tool set is exactly { chronicle }.
  blogger: ['chronicle'],
  distiller: [],
  // JS-001: the generated js-ROLE tool — allowed iff the role has a
  // filesystem capability (Coder/Inspector/DevOps/Browser/Reviewer).
  'js-tools': {
    coder: ['js-coder'],
    inspector: ['js-inspector'],
    devops: ['js-devops'],
    browser: ['js-browser'],
    reviewer: ['js-reviewer'],
  },
}

/**
 * Whole-object expectation, chosen over cross-checking `roles.permissions(role)`.
 *
 * The facade returns `ToolPermission` case names (`Exec`, `Pty`, `Fork`), not tool
 * keys. Turning one into the other means re-implementing `StaticTools.permissionObj`'s
 * rename table — `Exec`→`executor`, `Pty`→`open-terminal`, Orchestrator's `Fork`→
 * `commission`, DevOps' write/edit override. A test that mirrors the table it is
 * checking stays green when the table is wrong, which is the false green
 * `design-script-forest.md:630` calls more dangerous than no verification at all.
 *
 * So the matrix is pinned literally against docs/what/agent.md AGENT-006, and the facade is used
 * below only for what it can say independently: how many tools a role may hold.
 *
 * `external_directory` is Host meta-permission, not a role tool: Host defaults it to
 * ask; every managed agent overrides to allow so project-external paths do not prompt.
 */
// JS-001: a role allows exactly its own generated js-* tool, and only when
// its capability set holds a filesystem permission.
const JS_TOOL_ROLES = ['coder', 'inspector', 'devops', 'browser', 'reviewer']
const jsToolAllowed = (role, key) =>
  key.startsWith('js-') && key === `js-${role}` && JS_TOOL_ROLES.includes(role)

const expectedPermission = (role) =>
  Object.fromEntries(
    KNOWN_TOOL_KEYS.map((key) => [
      key,
      key === 'external_directory' || ALLOWED_TOOLS[role].includes(key) || jsToolAllowed(role, key)
        ? 'allow'
        : 'deny',
    ]),
  )

/** AGENT-001 case names, in the order of `ROLE_NAMES`. */
const FACADE_ROLE_CASES = {
  orchestrator: 'Orchestrator',
  manager: 'Manager',
  coder: 'Coder',
  inspector: 'Inspector',
  devops: 'DevOps',
  browser: 'Browser',
  inquiry: 'Inquiry',
  reviewer: 'Reviewer',
  blogger: 'Blogger',
  distiller: 'Distiller',
}

/** StaticTools.toolNames expansion with role-specific Exec / Fork splits. */
const toolKeysForPermission = (role, permission) => {
  switch (permission) {
    case 'Fork':
      return role === 'orchestrator' ? ['commission'] : ['fork']
    case 'Join':
      return ['join']
    case 'Horizon':
      return ['horizon']
    case 'TodoWrite':
      return ['todowrite']
    case 'Fission':
      return ['fission']
    case 'Read':
      return ['read']
    case 'Write':
      return ['write']
    case 'Edit':
      return ['edit']
    case 'Glob':
      return ['glob']
    case 'Grep':
      return ['grep']
    case 'Move':
      return ['mv']
    case 'Remove':
      return ['rm']
    case 'BashHoneypot':
      return ['bash-honeypot']
    case 'AutoInjected':
      return ['auto-injected']
    case 'Inspect':
      return ['inspect']
    case 'Behavior':
      return ['establish-behavior', 'repair-behavior']
    case 'Exec':
      if (role === 'inspector') return ['query-shell']
      if (role === 'devops') return ['run']
      return ['run', 'query-shell']
    case 'Pty':
      return ['open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal']
    case 'Network':
      return ['stealth-browser-mcp_*']
    case 'Judge':
      return ['judge']
    case 'Chronicle':
      return ['chronicle']
    case 'Fetch':
      return ['fetch']
    case 'Sphinx':
      return ['sphinx_*']
    case 'Finality':
      return ['suicide']
    default:
      return []
  }
}

const facadeToolKeyCount = (role) =>
  roles.permissions(roles.of(FACADE_ROLE_CASES[role])).flatMap((permission) => toolKeysForPermission(role, permission)).length

const agentHandleForChild = (runtime, parentSessionId, childSessionId) => {
  const projection = agentJournal.handleProjection(runtime.journal, sessionId(parentSessionId))
  const record = handleProjection.tryFindByChildSession(sessionId(childSessionId), projection)
  assert.ok(record, `HandleLinked missing for child ${childSessionId}`)
  const raw = handleId.tryAgent(record.Handle)
  assert.ok(raw, 'expected agent handle')
  return idValue.agentHandle(raw)
}

// ── tests ───────────────────────────────────────────────────────────────────

test('AGENT_009_the_tool_registry_exposes_exactly_the_declared_arguments', async () => {
  await withPlugin(async (hooks) => {
    assert.deepEqual(Object.keys(hooks.tool).sort(), Object.keys(EXPECTED_ARGUMENTS).sort())

    const observed = {}
    const notHostSchemas = []

    for (const [toolName, definition] of Object.entries(hooks.tool)) {
      const args = {}
      for (const [argName, schema] of Object.entries(definition.args)) {
        // Every argument has to be a Host-built zod schema, not a hand-rolled record:
        // the Host validates provider input through it before `execute` ever runs.
        if (typeof schema?.safeParse !== 'function') notHostSchemas.push(`${toolName}.${argName}`)
        args[argName] = schema?.isOptional?.() ? 'optional' : 'required'
      }
      observed[toolName] = args
    }

    assert.deepEqual(notHostSchemas, [], 'every argument must come from the Host schema builder')
    assert.deepEqual(observed, EXPECTED_ARGUMENTS)
  })
})

test('AGENT_008_009_every_agent_argument_offers_exactly_its_declared_agents', async () => {
  await withPlugin(async (hooks) => {
    const observed = Object.fromEntries(
      Object.keys(EXPECTED_AGENT_ENUMS).map((toolName) => [
        toolName,
        agentEnumEntries(hooks.tool[toolName].args.calling),
      ]),
    )

    assert.deepEqual(observed, EXPECTED_AGENT_ENUMS)

    // EXEC-003: the PTY signal set, the only other enum on the model-visible surface.
    assert.deepEqual(agentEnumEntries(hooks.tool['signal-terminal'].args.signal).sort(), [
      'HUP',
      'INT',
      'KILL',
      'QUIT',
      'TERM',
      'USR1',
      'USR2',
    ])

    // REVIEW-002: a verdict is a tool argument with exactly two values.
    assert.deepEqual(agentEnumEntries(hooks.tool.judge.args.verdict), ['PERFECT', 'REVISE'])

    // Clean break: calling is omitted only for continuation; Byname itself is
    // always required and carries the stable provider identity.
    for (const toolName of Object.keys(EXPECTED_AGENT_ENUMS)) {
      assert.equal(hooks.tool[toolName].args.calling.safeParse(undefined).success, true)
      assert.equal(hooks.tool[toolName].args.name.safeParse(undefined).success, false)
    }
  })
})

test('AGENT_004_006_010_config_gains_a_prompt_and_the_whole_permission_matrix', async () => {
  await withPlugin(async (hooks) => {
    const config = hostFinalConfig()
    hooks.config(config)

    // AGENT-006: one whole-object comparison per agent. Twenty of them, because
    // AGENT-010 makes fast and deep hold the same tools and a per-tier divergence has
    // to be visible rather than assumed.
    const permissions = {}
    const expected = {}
    for (const role of ROLE_NAMES) {
      for (const tier of ['fast', 'deep']) {
        permissions[`${tier}-${role}`] = config.agent[`${tier}-${role}`].permission
        expected[`${tier}-${role}`] = expectedPermission(role)
      }
    }
    assert.deepEqual(permissions, expected)

    // Independent second source. The facade cannot supply the tool keys without
    // re-deriving the rename table, but it can say how many tools a role holds, and
    // that number comes from `Kernel/Roles.fs` rather than from this file.
    // external_directory is Host meta-permission (always allow), not a role tool key.
    const allowedCount = ROLE_NAMES.map((role) => [
      role,
      KNOWN_TOOL_KEYS.filter(
        (key) =>
          key !== '*' &&
          key !== 'external_directory' &&
          !key.startsWith('js-') && // js-* is one capability-projected surface, not a ToolPermission
          permissions[`fast-${role}`][key] === 'allow',
      ).length,
    ])
    const facadeCount = ROLE_NAMES.map((role) => [role, facadeToolKeyCount(role)])
    assert.deepEqual(allowedCount, facadeCount, 'an allow appearing without a ToolPermission behind it')

    // AGENT-004/005: every managed agent receives a prompt, and the clauses that
    // define its role are present. `mode` is asserted alongside because
    // `applyOwnedFields` writes both and a lost `mode` would strand the agent.
    const shape = {}
    const canonicalPrompts = currentRuntimeResources().Prompts
    const promptField = {
      orchestrator: 'OrchestratorSystemPrompt',
      manager: 'ManagerSystemPrompt',
      coder: 'CoderSystemPrompt',
      inspector: 'InspectorSystemPrompt',
      devops: 'DevopsSystemPrompt',
      browser: 'BrowserSystemPrompt',
      inquiry: 'InquirySystemPrompt',
      reviewer: 'ReviewerSystemPrompt',
      blogger: 'BloggerSystemPrompt',
      distiller: 'DistillerSystemPrompt',
    }
    for (const role of ROLE_NAMES) {
      for (const tier of ['fast', 'deep']) {
        const entry = config.agent[`${tier}-${role}`]
        shape[`${tier}-${role}`] = { mode: entry.mode, prompt: typeof entry.prompt }
      }
      // AGENT-001: fast and deep share one prompt, so checking clauses once per role
      // is not a gap — the equality below is what makes it one.
      assert.equal(
        config.agent[`fast-${role}`].prompt,
        config.agent[`deep-${role}`].prompt,
        `fast-${role} and deep-${role} must share one system prompt`,
      )
      assert.equal(
        config.agent[`fast-${role}`].prompt,
        canonicalPrompts[promptField[role]],
        `${role} must receive the canonical RuntimeResources prompt`,
      )
    }

    assert.deepEqual(
      shape,
      Object.fromEntries(
        ROLE_NAMES.flatMap((role) =>
          ['fast', 'deep'].map((tier) => [`${tier}-${role}`, { mode: 'primary', prompt: 'string' }]),
        ),
      ),
    )
  })
})

// ── HOST-013: the pair-programming thought marker (docs/what/host.md) ───────────────────

// HOST-013: the frozen provider-visible thought text and source identity, read
// from the build artifact so a rewording fails here instead of asserting stale
// bytes (single point of definition, docs/what/host.md).
import {
  source as PAIR_PROGRAMMING_THOUGHT_SOURCE,
  text as PAIR_PROGRAMMING_THOUGHT_TEXT,
} from '../../../dist/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.js'

/** HOST-013: count synthetic pair messages by source identity, never by text. */
const markerCount = (messages) =>
  messages.filter((message) => message?.info?.source === PAIR_PROGRAMMING_THOUGHT_SOURCE).length

const withSession = (messages, sessionID = 'ses-host-013') =>
  messages.map((message, index) => ({
    ...message,
    info: {
      ...(message.info ?? {}),
      id: message.info?.id ?? `msg-${index}`,
      role: message.info?.role ?? message.role ?? 'user',
      sessionID,
    },
  }))

test('CTX_002_transform_appends_one_pair_programming_pair', async () => {
  await withPlugin(async (hooks) => {
    // HOST-013: every transform inserts one completed auto-injected row before trailing user.
    const transformed = { messages: withSession([{ role: 'user', text: 'hello' }]) }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(transformed.messages.length, 2)
    assert.equal(markerCount(transformed.messages), 1)

    const pair = transformed.messages[0]
    const user = transformed.messages[1]
    assert.equal(pair.info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
    assert.equal(pair.parts[0].tool, 'auto-injected')
    assert.equal(pair.parts[0].state.status, 'completed')
    assert.notEqual(pair.parts[0].state.status, 'pending')
    assert.equal(pair.parts[0].state.output, PAIR_PROGRAMMING_THOUGHT_TEXT)
    assert.equal(user.role ?? user.info?.role, 'user')

    const markerRe = /\[(CAPS|REVIEW|HINT):/
    const marked = transformed.messages
      .flatMap((message) => [
        message.text ?? '',
        ...(message.parts ?? []).flatMap((part) => [part.text ?? '', part.state?.output ?? '']),
      ])
      .filter((text) => markerRe.test(text))
    assert.deepEqual(marked, [])
  })
})

test('HOST_013_pair_lands_at_end_when_transcript_ends_with_assistant_tail', async () => {
  await withPlugin(async (hooks) => {
    // Transcript ends with assistant text (no trailing user, no tool batch):
    // the new pair must land at the transcript END. The old "before the last
    // user anywhere" rule inserted the pair mid-transcript on continuation
    // transcripts, rewriting already-sent bytes and breaking the append-only
    // prefix (HOST-013 constraint 5).
    const transformed = {
      messages: withSession([
        { role: 'user', text: 'hello' },
        { role: 'assistant', text: 'ok' },
      ]),
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(transformed.messages.length, 3)
    assert.equal(markerCount(transformed.messages), 1)
    assert.equal(transformed.messages[0].role ?? transformed.messages[0].info?.role, 'user')
    assert.equal(transformed.messages[1].role ?? transformed.messages[1].info?.role, 'assistant')
    assert.equal(transformed.messages[2].info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
  })
})

test('HOST_013_empty_messages_still_append_pair', async () => {
  await withPlugin(async (hooks) => {
    // HOST-013: no anchor threshold; empty history also receives one pair.
    // sessionID is required for durable transcript identity in plugin path.
    const transformed = {
      messages: withSession([]).length
        ? withSession([])
        : [{ info: { id: 'seed', role: 'user', sessionID: 'ses-empty' }, parts: [] }],
    }
    // Keep a non-empty session-tagged array so projectionSessionId resolves,
    // while content-less seed is filtered by transform's non-marker retention.
    transformed.messages = [
      { info: { id: 'seed', role: 'assistant', sessionID: 'ses-empty' }, parts: [{ type: 'text', text: '' }] },
    ]
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(markerCount(transformed.messages) >= 1, true)
    // no trailing user → pair at end
    assert.equal(transformed.messages.at(-1).parts[0].tool, 'auto-injected')
  })
})

test('HOST_013_system_and_assistant_history_still_appends_pair', async () => {
  await withPlugin(async (hooks) => {
    const transformed = {
      messages: withSession([
        { role: 'system', text: 'rules' },
        { role: 'assistant', text: 'ok' },
      ]),
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(markerCount(transformed.messages), 1)
    // no user → pair at end
    assert.equal(transformed.messages.at(-1).info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
  })
})

test('HOST_013_pair_before_trailing_user_in_mixed_history', async () => {
  await withPlugin(async (hooks) => {
    // Keep messages in the bare shape used by other HOST-013 cases so Companion
    // recovery is not armed; only the permanent pair contract is under test.
    const transformed = {
      messages: withSession(
        [
          { role: 'user', text: 'hello' },
          { role: 'assistant', text: 'thinking' },
          { role: 'user', text: 'continue' },
        ],
        'ses-tools',
      ),
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(markerCount(transformed.messages), 1)
    assert.equal(transformed.messages.at(-1).role ?? transformed.messages.at(-1).info?.role, 'user')
    assert.equal(transformed.messages.at(-2).info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
  })
})

test('HOST_013_repeated_transform_of_same_placement_replays_only', async () => {
  await withPlugin(async (hooks) => {
    // HOST-013: a placement occasion that already has a bracket only replays —
    // repeated transform of the same real transcript must not append a pair.
    // Use non-synthetic base so history is re-hydrated from durable/memory ledger.
    const first = { messages: withSession([{ role: 'user', text: 'hello' }], 'ses-repeat') }
    await hooks['experimental.chat.messages.transform']({}, first)
    assert.equal(markerCount(first.messages), 1)
    assert.equal(first.messages.at(-1).role ?? first.messages.at(-1).info?.role, 'user')

    const second = { messages: withSession([{ role: 'user', text: 'hello' }], 'ses-repeat') }
    await hooks['experimental.chat.messages.transform']({}, second)
    assert.equal(markerCount(second.messages), 1, 'same placement must replay, not append a second pair')
    assert.equal(second.messages.at(-1).role ?? second.messages.at(-1).info?.role, 'user')
  })
})

test('HOST_013_new_user_turn_keeps_history_and_appends_new_pair', async () => {
  await withPlugin(async (hooks) => {
    const first = {
      messages: withSession([{ role: 'user', text: 'hello' }], 'ses-turn'),
    }
    await hooks['experimental.chat.messages.transform']({}, first)
    // first: [pair, user]
    const firstCallId = first.messages[0].parts[0].callID

    const second = {
      messages: withSession(
        [
          { role: 'user', text: 'hello' },
          { role: 'user', text: 'second turn' },
        ],
        'ses-turn',
      ),
    }
    await hooks['experimental.chat.messages.transform']({}, second)

    // second: [hist-pair, user hello, next-pair, user second]
    assert.equal(markerCount(second.messages), 2)
    assert.equal(second.messages[0].parts[0].callID, firstCallId)
    assert.notEqual(second.messages[2].parts[0].callID, firstCallId)
    assert.equal(second.messages.at(-1).role ?? second.messages.at(-1).info?.role, 'user')
  })
})

test('HOST_013_companion_blogger_skips_guideline_injection', async () => {
  // HOST-013 scope: durable Companion (Blogger) transcripts must not receive
  // pair-programming auto-injected pairs — they pollute the blog tool contract.
  const { agentFact, sessionId, caseOf, stream } = await import('../../unit/support/domain.mjs')
  const { AgentJournalModule_appendAgent } = await import('../../../dist/Journal/AgentJournal.js')

  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const main = sessionId('ses-main-no-auto-injected')
    const blogger = sessionId('ses-blogger-no-auto-injected')
    const linked = AgentJournalModule_appendAgent(
      stream.session(main),
      undefined,
      agentFact('CompanionBloggerLinked', {
        SessionId: main,
        BloggerSessionId: blogger,
        BloggerAgent: 'fast-blogger',
      }),
      runtime.journal,
    )
    assert.equal(caseOf(linked), 'Ok')

    const transformed = {
      messages: withSession(
        [{ role: 'user', text: 'record this delta', parts: [{ type: 'text', text: 'record this delta' }] }],
        'ses-blogger-no-auto-injected',
      ),
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(markerCount(transformed.messages), 0, 'blogger must not receive auto-injected pairs')
    assert.equal(
      transformed.messages.some((m) => m?.parts?.some((p) => p?.tool === 'auto-injected')),
      false,
    )
    assert.equal(
      transformed.messages.every((m) => m?.info?.source !== PAIR_PROGRAMMING_THOUGHT_SOURCE),
      true,
    )
  })
})



// ── the execute path (EXEC-002, EXEC-004, AGENT-007 layer two) ───────────────
//
// Everything above is layer 2: what the Host is OFFERED. The three tests below
// are what the shock-anneal archive (FINAL-REPORT §8) recorded as never passing in the deleted
// `tests/e2e/tests/manager-tool-contract.mjs`: actually invoking
// `hooks.tool.*.execute`. Two independent defects kept them red:
//
//   1. No session transport under `client: {}` — production had briefly
//      FABRICATED a completed AgentRunResult carrying "test output"
//      (src/Wanxiangshu/Infrastructure/OpenCode/Host/Sessions.fs:149-153 records its removal), so the old
//      expectations were written against a fake. The fixture now supplies a
//      real minimal SDK client and completions arrive as real
//      `TerminalOutcome.Completed` payloads with distinct SessionWide/TurnFormal
//      texts; `output` is asserted to be the delivered TurnFormalText.
//   2. The execute gate is AGENT-007's second layer: without an accepted
//      Authority Root for the calling session the role is unresolved and every
//      tool returns `{"error":"...no Authority Root..."}`. The fixture writes a
//      real durable HumanRoot through `PromptDispatcher.AcceptHumanRoot`
//      (PROMPT-002) — the production authority fact, not a test backdoor.

test('AGENT_007_unresolved_role_denies_all_tools', async () => {
  // AGENT-007 layer two, fail-closed branch: with no accepted Authority Root
  // for the calling session, `RoleFor` is None and the tool set must be empty —
  // every tool, read-only or not, returns the structured rejection. `inspector`
  // is the tool the old code exempted while the role was unresolved, so it is
  // the one the clause names as the thing to delete (docs/what/agent.md).
  await withExecutablePlugin(async (hooks, _directory, _createdIds, _runtime) => {
    // Deliberately NO acceptAuthorityRoot: this session has no root at all.
    const context = { sessionID: 'unresolved-role', agent: 'fast-manager' }

    for (const [toolName, args] of [
      ['horizon', {}],
      ['inspect', { charge: 'git status' }],
      ['fork', { calling: 'coder', name: 'Ada', charge: 'work' }],
    ]) {
      const text = await hooks.tool[toolName].execute(args, context)
      assert.match(text, /This tool is unavailable until the caller's authority is established\./)
      assert.deepEqual(parseToml(text), {}, `${toolName} must reject without an error DTO`)
    }
  })
})

test('EXEC_002_sync_delegate_inspector_coder_refuse_invalid_args_via_plugin', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'inquiry-contract', 'fast-inquiry')
    acceptAuthorityRoot(runtime, 'devops-contract', 'fast-devops')

    const inquiry = { sessionID: 'inquiry-contract', agent: 'fast-inquiry' }
    const devops = { sessionID: 'devops-contract', agent: 'fast-devops' }

    for (const args of [{}, { charge: '   ' }]) {
      const text = await hooks.tool.inspect.execute(args, inquiry)
      assert.match(text, /inspect needs a charge\./)
      assert.equal(parseToml(text).error, undefined)
    }

    const missingCharge = await hooks.tool['establish-behavior'].execute({}, devops)
    assert.match(missingCharge, /establish-behavior needs a charge\./)
    assert.equal(parseToml(missingCharge).error, undefined)

    const repairMissing = await hooks.tool['repair-behavior'].execute({}, devops)
    assert.match(repairMissing, /repair-behavior needs a charge\./)
    assert.equal(parseToml(repairMissing).error, undefined)
  })
})
test('GLORY_031_manager_fork_of_a_reviewer_is_denied_role_based', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-reverted-root', 'fast-manager')

    // GLORY-002/031: a Manager must never create, reuse or nudge a Reviewer;
    // the Reviewer is Host-owned. Denied by durable role, before any prompt.
    const result = await hooks.tool.fork.execute(
      { calling: 'examiner', name: 'Rhea', charge: 'Review the current tree.' },
      { sessionID: 'manager-reverted-root', agent: 'fast-manager' },
    )

    assert.match(result, /Unknown or unavailable calling/)
    assert.equal(parseToml(result).error, undefined)
    assert.doesNotMatch(result, /Reviewer|fast-reviewer|\berror\s*=/i)
    assert.equal(runtime.prompts.length, 0)

    const deepResult = await hooks.tool.fork.execute(
      { calling: 'auditor', name: 'Rhea', charge: 'Review the same current tree.' },
      { sessionID: 'manager-reverted-root', agent: 'fast-manager' },
    )
    assert.match(deepResult, /Unknown or unavailable calling/)
    assert.equal(parseToml(deepResult).error, undefined)
    assert.equal(runtime.prompts.length, 0)
  })
})

test('EXEC_002_EXEC_004_fork_join_and_horizon_carry_natural_language_identity', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-contract', 'fast-manager')
    const context = { sessionID: 'manager-contract', agent: 'fast-manager' }

    const unknown = await hooks.tool.fork.execute({ calling: 'wizard', name: 'Ada', charge: 'work' }, context)
    assert.match(unknown, /Unknown or unavailable calling/)
    assert.equal(parseToml(unknown).error, undefined)
    assert.doesNotMatch(unknown, /fast-|deep-|\berror\s*=/i)

    const forkText = await hooks.tool.fork.execute({ calling: 'coder', name: 'Ada', charge: 'work' }, context)
    assert.match(forkText, /# Ada carries this charge now\./)
    assert.equal(parseToml(forkText).error, undefined)

    runtime.recordFork('manager-contract', agentHandleForChild(runtime, 'manager-contract', createdIds[0]), createdIds[0])

    const joinResultP = hooks.tool.join.execute({}, context)
    notifyCompleted(runtime, createdIds[0], 'forked coder session-wide A', 'forked coder turn formal report')
    const joinText = await joinResultP

    assert.match(joinText, /# Ada has returned\./)
    assert.doesNotMatch(joinText, /fast-coder/)
    assert.ok(!/\b(status|count|ordinal|kind|agent_id)\s*=/.test(joinText))

    const horizonText = await hooks.tool.horizon.execute({}, context)
    assert.match(horizonText, /Nothing beyond your immediate sight|has returned|still away/i)
    assert.equal(parseToml(horizonText).error, undefined)
  })
})

// Phase 4 / corrective §7.1: real chat.message (keyless external human) wakes a
// blocked JoinTool via JoinInterruptRegistry → reason=user_message. Must not use
// OperatorAbort or tool abort controller as the primary stimulus.
test('EXEC_017_blocked_join_wakes_on_user_message_from_chat_message', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-user-wake', 'fast-manager')
    const context = { sessionID: 'manager-user-wake', agent: 'fast-manager' }

    const forkText = await hooks.tool.fork.execute({ calling: 'coder', name: 'Ada', charge: 'work' }, context)
    assert.match(forkText, /# Ada carries this charge now\./)
    assert.equal(parseToml(forkText).error, undefined, `fork failed: ${forkText}`)
    const agentId = agentHandleForChild(runtime, 'manager-user-wake', createdIds[0])
    runtime.recordFork('manager-user-wake', agentId, createdIds[0])

    // No AttachAbort / abort controller — join waits on child + registry wake.
    const joinP = hooks.tool.join.execute({}, context)
    // Allow JoinTool to Register on the session interrupt registry before pulse.
    await new Promise((r) => setTimeout(r, 20))

    // Keyless external human: PhysicalUserMessageId present, no PromptKey metadata,
    // not host compaction. HostSignalBootstrap signals JoinInterrupts first.
    await hooks['chat.message'](
      { sessionID: 'manager-user-wake' },
      {
        message: { id: 'msg-user-wake-1', role: 'user', sessionID: 'manager-user-wake' },
        parts: [{ type: 'text', text: 'new instruction from user' }],
      },
    )

    const raceTimeoutMs = 2000
    const text = await Promise.race([
      joinP,
      new Promise((_, reject) =>
        setTimeout(
          () => reject(new Error(`join did not wake from chat.message within ${raceTimeoutMs}ms`)),
          raceTimeoutMs,
        ),
      ),
    ])
    assert.match(text, /# Something nearer has arrived\./)
    assert.ok(!/\b(status|count|ordinal|kind|agent_id|reason)\s*=/.test(text))
    assert.ok(!text.includes('operator_abort'), 'user_message path must not emit operator_abort')
    assert.equal(parseToml(text).error, undefined)

    // Negative shape: PromptKey continuation is not the external-human signal path
    // (HostSignalBootstrap only SignalUserMessage when PromptKey is absent). Do not
    // hang proving non-wake; just show a PromptKey ingress does not force OperatorAbort.
    await hooks['chat.message'](
      { sessionID: 'manager-user-wake' },
      {
        message: {
          id: 'msg-prompt-key-cont',
          role: 'user',
          sessionID: 'manager-user-wake',
          metadata: { wanxiangshu_prompt_key: 'pk-continuation-not-user-wake' },
        },
        parts: [
          {
            type: 'text',
            text: 'continuation with prompt key',
            metadata: { wanxiangshu_prompt_key: 'pk-continuation-not-user-wake' },
          },
        ],
      },
    )

    // Resource safety: child was not cancelled by user_message interrupt.
    // Late terminal still claims the completion cell for a subsequent join.
    notifyCompleted(runtime, createdIds[0], 'late session-wide A', 'late turn formal report')
    const join2Text = await hooks.tool.join.execute({}, context)
    assert.match(join2Text, /# Ada has returned\./, `late join after user_message must harvest child: ${join2Text}`)
    assert.ok(!/\b(status|count|ordinal|kind|agent_id)\s*=/.test(join2Text))
    assert.equal(runtime.abortedIds.includes(createdIds[0]), false, 'user_message must not abort the child session')
  })
})

test('EXEC_002_fork_reuse_by_byname_and_create_by_calling', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-reuse', 'fast-manager')
    const context = { sessionID: 'manager-reuse', agent: 'fast-manager' }

    const createdText = await hooks.tool.fork.execute(
      { calling: 'coder', name: 'Ada', charge: 'first assignment' },
      context,
    )
    assert.match(createdText, /# Ada carries this charge now\./)
    assert.equal(parseToml(createdText).error, undefined, `create fork failed: ${createdText}`)
    assert.equal(createdIds.length, 1, 'calling + Byname creates exactly one child session')
    const agentId = agentHandleForChild(runtime, 'manager-reuse', createdIds[0])
    assert.match(agentId, /^[a-z0-9]{6}$/)
    const promptsAfterCreate = runtime.prompts.length
    assert.ok(promptsAfterCreate >= 1, 'create path must send a child prompt')

    // PROMPT-005: the create fork is AwaitMode.Detached with a receipt-only stub —
    // Claimed → Submitted, no PhysicalAccepted, so the child has NO ActiveLogicalRun.
    // BusyAgentNudge requires one (HostForkBusyNudge.fs:37). Accept the pending
    // AgentOwnerRoot claim on the child before the busy reuse below.
    const childSessionId = createdIds[0]
    const childPrompt = [...runtime.prompts].reverse().find((p) => {
      const id = p?.path?.id ?? p?.sessionID ?? p?.sessionId
      return id === childSessionId
    })
    assert.ok(childPrompt, 'create must record a child prompt')
    const promptKey =
      childPrompt?.body?.metadata?.wanxiangshu_prompt_key ??
      childPrompt?.body?.parts?.find((part) => part?.type === 'text')?.metadata?.wanxiangshu_prompt_key
    assert.equal(typeof promptKey, 'string', 'child prompt must carry PromptKey metadata')
    acceptChildAgentOwnerRoot(runtime, childSessionId, promptKey)

    // Busy reuse: continue by Byname. Internal handle remains wall-internal.
    const nudgedText = await hooks.tool.fork.execute({ name: 'Ada', charge: 'nudge: add one constraint' }, context)
    assert.match(nudgedText, /Ada carries this charge now\./)
    assert.doesNotMatch(nudgedText, new RegExp(agentId))
    assert.equal(parseToml(nudgedText).error, undefined, `reuse/nudge failed: ${nudgedText}`)
    assert.equal(createdIds.length, 1, 'reuse must not create a second child session')
    assert.ok(
      runtime.prompts.length > promptsAfterCreate,
      'busy reuse must deliver a nudge prompt to the existing child',
    )

    // A second person needs a distinct Byname even when the calling is the same.
    const twinText = await hooks.tool.fork.execute(
      { calling: 'coder', name: 'Grace', charge: 'parallel twin work' },
      context,
    )
    assert.match(twinText, /# Grace carries this charge now\./)
    assert.equal(parseToml(twinText).error, undefined, `second create failed: ${twinText}`)
    assert.equal(createdIds.length, 2, 'distinct Byname creates a second child record')
    assert.notEqual(
      agentHandleForChild(runtime, 'manager-reuse', createdIds[1]),
      agentId,
      'distinct Byname creates a distinct internal handle',
    )
  })
})

const schemaDescription = (schema) => {
  const seen = new Set()
  const visit = (node) => {
    if (!node || typeof node !== 'object' || seen.has(node)) return ''
    seen.add(node)
    if (typeof node.description === 'string' && node.description.trim()) return node.description
    const meta = typeof node.meta === 'function' ? node.meta() : node.meta
    if (typeof meta?.description === 'string' && meta.description.trim()) return meta.description
    const def = node.def ?? node._def
    if (typeof def?.description === 'string' && def.description.trim()) return def.description
    for (const inner of [def?.innerType, def?.inner, node.innerType]) {
      const found = visit(inner)
      if (found) return found
    }
    return ''
  }
  return visit(schema)
}

test('EXEC_002_fork_tool_description_is_an_office_capability_map', async () => {
  await withPlugin(async (hooks) => {
    const description = hooks.tool.fork?.description
    assert.equal(typeof description, 'string', 'fork tool must expose description')
    assert.match(description, /another office within this mission/i)
    assert.match(description, /Coder \/ Engineer[\s\S]{0,120}Changes repository source/i)
    assert.match(description, /Scout \/ Investigator[\s\S]{0,160}already exist in the repository/i)
    assert.match(description, /Technician \/ Operator[\s\S]{0,160}running world/i)
    assert.match(description, /Navigator \/ Researcher[\s\S]{0,160}external world with provenance/i)
    assert.match(description, /Analyst \/ Inquirer[\s\S]{0,160}not yet clear/i)
    assert.match(description, /differ in persona and reasoning depth,[\s\S]{0,40}not in the office's authority/i)
    assert.match(description, /calling \+ name \+ charge[\s\S]{0,80}same name/i)
    assert.doesNotMatch(description, /another witness/i)
    assert.doesNotMatch(description, /\bwitnesses\b/i)
    assert.doesNotMatch(description, /fast-|deep-|handle/i)
    const commission = hooks.tool.commission?.description
    assert.equal(typeof commission, 'string')
    assert.match(commission, /calling \+ name \+ charge|known road/i)
    assert.doesNotMatch(commission, /job id|handle as name|fast-|deep-/i)
  })
})

test('EXEC_002_inspect_tool_description_forbids_mutation_and_execution', async () => {
  await withPlugin(async (hooks) => {
    const description = hooks.tool.inspect?.description
    assert.equal(typeof description, 'string', 'inspect tool must expose description')
    assert.match(description, /facts that already exist in the repository/i)
    assert.match(description, /read-only in the causal sense/i)
    assert.match(description, /Do not use inspect to ask for code changes/i)
    assert.match(description, /make the project run[\s\S]{0,80}behavioral evidence/i)
    assert.match(description, /evidence from a witness, not a mutation/i)
  })
})

test('EXEC_002_fork_and_inspect_argument_descriptions_state_parameter_meaning', async () => {
  await withPlugin(async (hooks) => {
    assert.match(schemaDescription(hooks.tool.fork.args.calling), /office\/persona|Omit when continuing/i)
    assert.match(schemaDescription(hooks.tool.fork.args.charge), /bounded consequence|Do not prescribe hidden tools/i)
    assert.match(schemaDescription(hooks.tool.fork.args.keywords), /retrieval hints|do not enlarge/i)
    assert.match(schemaDescription(hooks.tool.inspect.args.charge), /repository fact|Do not ask for code changes/i)
    assert.match(schemaDescription(hooks.tool.inspect.args.keywords), /retrieval hints|Inspector/i)
  })
})

test('EXEC_002_the_fixture_delivers_the_real_journal_and_terminal_port', async () => {
  await withExecutablePlugin(async (_hooks, directory, _createdIds, runtime) => {
    // The runtime the fixture hands over must BE the production instances:
    // - the journal's RuntimeId matches the EventStore RuntimeStarted tip, and
    // - NotifyTerminal through the handed-over port is what join observed above
    //   (proven there by a join that only returns after the notification).
    acceptAuthorityRoot(runtime, 'manager-fixture-probe', 'fast-manager')

    assert.equal(typeof runtime.runtimeId, 'string')
    assert.ok(runtime.runtimeId.length > 0, 'fixture must expose runtimeId')
    assert.equal(runtime.journal != null, true, 'fixture must hand over a live AgentJournal')

    const commonDirectory = execFileSync('git', ['-C', directory, 'rev-parse', '--git-common-dir'], {
      encoding: 'utf8',
    }).trim()
    const gitDirectory = isAbsolute(commonDirectory) ? commonDirectory : resolve(directory, commonDirectory)
    // EventStore tip lives at refs/wanxiang/store — not wanxiangshu-next/*.ndjson.
    const tip = execFileSync(
      'git',
      ['-C', gitDirectory, 'rev-parse', '--verify', '--quiet', 'refs/wanxiang/store'],
      { encoding: 'utf8' },
    ).trim()
    assert.match(tip, /^[0-9a-f]{40}$/, 'EventStore canonical ref must be published')
    assert.equal(existsSync(join(gitDirectory, 'wanxiangshu-next', 'runtimes', `${runtime.runtimeId}.ndjson`)), false)
  })
})

test('GLORY_034_suicide_tool_executes_synchronously', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-suicide-sync', 'fast-manager')
    const context = { sessionID: 'manager-suicide-sync', agent: 'fast-manager' }

    // Pre-activation refusal is instruction-only (comment wire); parseToml strips comments.
    const preText = await hooks.tool.suicide.execute({ last_words: 'Task completed.' }, context)
    assert.match(preText, /# Continue working\./)
    assert.doesNotMatch(preText, /^error\s*=/m)
    assert.equal(parseToml(preText).error, undefined)
  })
})

test('GLORY_038_suicide_with_outstanding_child_prompts_to_join', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-suicide-outstanding', 'fast-manager')
    activateLife(runtime, 'manager-suicide-outstanding')
    acceptFirstTodoWrite(runtime, 'manager-suicide-outstanding')
    const context = { sessionID: 'manager-suicide-outstanding', agent: 'fast-manager', callID: 'call_suicide_1', messageID: 'msg_1' }

    // Fork a child agent so there is an active child handle.
    await hooks.tool.fork.execute({ calling: 'coder', name: 'Ada', charge: 'Do work' }, context)

    const resultText = await hooks.tool.suicide.execute({ last_words: 'Finished.' }, context)
    assert.match(resultText, /# Call join before seeking your end\./)
    assert.doesNotMatch(resultText, /^error\s*=/m)
    assert.equal(parseToml(resultText).error, undefined)
  })
})

test('GLORY_057_suicide_returns_undecided_when_hidden_reviewer_times_out', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    // The fixture git-inits but never commits (process-host-utils.js:111-112
    // commits `git add -A` + `git commit --allow-empty -m init`); a missing HEAD
    // makes GitTree.dirtyPayload throw on `git diff HEAD`, so FinalityTool's
    // treeOf returns None and the suicide is rejected by the pre-condition gate
    // before the hidden Reviewer ever forks. An initial commit routes this
    // scenario past that gate into FinalityController.start, where the injected
    // 1ms reviewerTimeoutMs (finalityReviewerTimeoutMs: 1) fires the timeout path.
    execFileSync('git', ['config', 'user.email', 'test@example.com'], { cwd: directory })
    execFileSync('git', ['config', 'user.name', 'test'], { cwd: directory })
    execFileSync('git', ['add', '-A'], { cwd: directory })
    execFileSync('git', ['commit', '--allow-empty', '-m', 'init'], { cwd: directory })

    acceptAuthorityRoot(runtime, 'manager-finality-no-terminal', 'fast-manager')
    activateLife(runtime, 'manager-finality-no-terminal')
    acceptFirstTodoWrite(runtime, 'manager-finality-no-terminal')
    const context = {
      sessionID: 'manager-finality-no-terminal',
      agent: 'fast-manager',
      callID: 'call-finality-no-terminal',
      messageID: 'msg-finality-no-terminal',
    }

    const outcome = await hooks.tool.suicide.execute({ last_words: 'Finished.' }, context)

    assert.equal(outcome, '# Your ending could not be decided.\n# You still have time. Continue, and seek your end again when you are ready.\n')
    // GLORY-055/057: infrastructure Undecided does not dispose an ungraduated
    // Reviewer session — the physical session stays available for the next request.
    assert.equal(runtime.abortedIds.includes('host-child-1'), false, 'undecided finality must not dispose the ungraduated hidden reviewer')
  }, { finalityReviewerTimeoutMs: 1 })
})
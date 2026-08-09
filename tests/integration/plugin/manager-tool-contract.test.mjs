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
import { readdirSync, readFileSync } from 'node:fs'
import { isAbsolute, join, resolve } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { roles, enforcer } from '../../unit/support/domain.mjs'
import {
  withPlugin,
  withExecutablePlugin,
  acceptAuthorityRoot,
  acceptChildAgentOwnerRoot,
  notifyCompleted,
  awaitPrompted,
  activateLife,
} from '../../unit/plugin/plugin-fixture.mjs'

/** AGENT-002: the twenty-four managed agents, exactly as the Host-final config names them. */
const ROLE_NAMES = [
  'orchestrator',
  'manager',
  'coder',
  'inspector',
  'devops',
  'browser',
  'meditator',
  'reviewer',
  'student',
  'teacher',
  'blogger',
  'executor',
]

const hostFinalConfig = () => {
  const agent = {}
  for (const role of ROLE_NAMES) {
    for (const tier of ['fast', 'deep']) {
      agent[`${tier}-${role}`] = { model: `provider/${tier}-${role}-model` }
    }
  }
  return { agent }
}

// ── the model-visible tool surface ───────────────────────────────────────────

/** Every argument of every tool, so a new or renamed argument fails here first. */
const EXPECTED_ARGUMENTS = {
  // ENFORCER-020 tip v2: required text + tip enum; optional evidence.
  // No 120 numeric score properties.
  'bash-honeypot': {},
  blog: {
    text: 'required',
    tip: 'required',
    evidence: 'optional',
  },
  coder: { agent: 'required', tdd: 'required', prompt: 'optional', prompts: 'optional' },
  executor: {
    command: 'required',
    estimated_mem_usage: 'required',
    estimated_output_bytes: 'required',
    estimated_running_secs: 'required',
  },
  fork: { agent: 'required', prompt: 'optional', tdd: 'optional' },
  'fork-manager': { agent: 'required', prompt: 'required' },
  'fork-pty': { agent: 'required', prompt: 'optional', signal: 'optional' },
  inspector: { agent: 'required', prompt: 'optional', prompts: 'optional' },
  join: {},
  list: {},
  mv: { source: 'required', destination: 'required' },
  rm: { path: 'required' },
  return: { message: 'required' },
  suicide: { last_words: 'required' },
  teacher: { message: 'required' },
  verdict: { verdict: 'required' },
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
    'deep-browser',
    'deep-coder',
    'deep-devops',
    'deep-inspector',
    'deep-meditator',
    'fast-browser',
    'fast-coder',
    'fast-devops',
    'fast-inspector',
    'fast-meditator',
  ],
  'fork-manager': ['deep-manager', 'fast-manager'],
  inspector: ['deep-inspector', 'fast-inspector'],
  coder: ['deep-coder', 'fast-coder'],
}

/**
 * The enum arm of an agent argument, whether or not it is wrapped in a union.
 *
 * `fork.agent` is `union([enum(...), string()])` while the other three are bare
 * enums (`ToolHostCodec.fs:90` vs `:78`). Measured consequence worth stating: the
 * string arm makes `fork.agent.safeParse('garbage')` SUCCEED, so this enum is a
 * provider-visible offer, not a validator. Rejecting an unknown agent happens inside
 * `execute` — which is the part of the original file that has never passed and is
 * recorded as a pending defect rather than migrated.
 */
const agentEnumEntries = (schema) => {
  const def = schema.def ?? schema._def
  const arms = def.type === 'union' ? def.options : [schema]
  return arms
    .map((arm) => arm.def ?? arm._def)
    .filter((armDef) => armDef.type === 'enum')
    .flatMap((armDef) => Object.keys(armDef.entries))
    .sort()
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
  'fork-manager',
  'fork-pty',
  'join',
  'list',
  'read',
  'write',
  'edit',
  'glob',
  'grep',
  'mv',
  'rm',
  'bash-honeypot',
  'inspector',
  'coder',
  'executor',
  'network',
  'verdict',
  'blog',
  'teacher',
  'return',
  'suicide',
]

/** AGENT-006/011/013/014/015: the allowed tools per role. Everything else denies. */
const ALLOWED_TOOLS = {
  orchestrator: ['fork-manager', 'join'],
  manager: ['fork', 'join', 'list', 'suicide'],
  coder: ['read', 'write', 'edit', 'glob', 'grep', 'inspector', 'mv', 'rm', 'bash-honeypot'],
  inspector: ['read', 'glob', 'grep', 'executor'],
  devops: ['fork-pty', 'join', 'list', 'read', 'glob', 'grep', 'inspector', 'coder', 'executor'],
  browser: ['read', 'glob', 'grep', 'network'],
  meditator: ['read', 'glob', 'grep', 'inspector'],
  reviewer: ['read', 'glob', 'grep', 'verdict'],
  student: ['teacher'],
  teacher: ['read', 'write', 'edit', 'glob', 'grep', 'mv', 'rm', 'inspector', 'coder', 'executor', 'network', 'return'],
  // ENFORCER-010: Blogger's tool set is exactly { blog }.
  blogger: ['blog'],
  executor: [],
}

/**
 * Whole-object expectation, chosen over cross-checking `roles.permissions(role)`.
 *
 * The facade returns `ToolPermission` case names (`Exec`, `Pty`, `Fork`), not tool
 * keys. Turning one into the other means re-implementing `StaticTools.permissionObj`'s
 * rename table — `Exec`→`executor`, `Pty`→`fork-pty`, Orchestrator's `Fork`→
 * `fork-manager`, DevOps' write/edit override. A test that mirrors the table it is
 * checking stays green when the table is wrong, which is the false green
 * `design-script-forest.md:630` calls more dangerous than no verification at all.
 *
 * So the matrix is pinned literally against docs/what/agent.md AGENT-006, and the facade is used
 * below only for what it can say independently: how many tools a role may hold.
 *
 * `external_directory` is Host meta-permission, not a role tool: Host defaults it to
 * ask; every managed agent overrides to allow so project-external paths do not prompt.
 */
const expectedPermission = (role) =>
  Object.fromEntries(
    KNOWN_TOOL_KEYS.map((key) => [
      key,
      key === 'external_directory' || ALLOWED_TOOLS[role].includes(key) ? 'allow' : 'deny',
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
  meditator: 'Meditator',
  reviewer: 'Reviewer',
  student: 'Student',
  teacher: 'Teacher',
  blogger: 'Blogger',
  executor: 'Executor',
}

// ── the prompt clauses ───────────────────────────────────────────────────────

/**
 * A prompt is prose, so the assertion is a required-clause list rather than a
 * whole-text comparison: pinning multi-kilobyte prompt bodies byte for byte would
 * fail on every wording edit while proving nothing about the clause that matters.
 * The listed patterns are the load-bearing sentences of AGENT-011/012/013/014,
 * plus the two `forbidden` patterns that keep a capability out of a prompt.
 */
const PROMPT_CLAUSES = {
  'fast-manager': {
    required: [
      /Manager thinks, delegates, and integrates/,
      /Your tools are `fork`, `join`, `list`, and `suicide`/,
      /Call `join` only when no useful unassigned work remains/,
      /A returned child record is evidence, not automatic completion/,
      /Coder edits\./,
      /Do not ask an agent to act outside its role/,
      /Do not ask Coder to run commands/,
      /Do not ask DevOps to edit files directly/,
      /bounded mechanical repair|autonomous mechanical repair|operational closure|execution\/repair objective/i,
      /agent_id/,
      /\blist\b/,
      /compatible context/,
      /Do not reuse when old context would make the new assignment ambiguous/,
      /Reuse must not reduce parallelism/,
      /tdd="red"/,
      /tdd="green"/,
      /suicide\(last_words\)/,
      /When no useful action remains, call/,
      // PROMPT-INSP-001: the Manager must forbid demanding full text, long
      // source, or query dumps from Inspector, and may only ask for locatable
      // summaries — the "repeater" prohibition must be unmistakable.
      /query dump|query dumps/i,
      /only locatable summaries|locatable summaries|locatable pointers/i,
    ],
    forbidden: [],
  },

  // AGENT-012: Coder sees `inspector` but never learns it can execute.
  'fast-coder': {
    required: [
      /Coder edits/,
      /Surgical Precision/,
      /Use Inspector only for a genuinely necessary static investigation/,
      /inspector\(agent: "fast-inspector", prompts\)/,
      /Editing Is the Completion Boundary/,
      /Do not check whether the code compiles or works/,
      /DO NOT use `inspector` to bypass that boundary/,
      /Never ask Inspector to run, reproduce, check, or diagnose compilation, builds, typechecks, linters, tests, programs, or runtime behavior/,
      /After the final required file edit, stop working/,
      // Coder TDD phase discipline (red → green → refactor).
      /red → green → refactor|red → green/,
      /tdd/,
      /Do not delete, skip, loosen, or rewrite/,
      /schema-required|schema-optional.*prompt-required|Manager `fork` of a Coder role/,
    ],
    forbidden: [/executor/i],
  },

  'fast-devops': {
    required: [
      /DevOps executes/,
      /fork-pty/,
      /No Direct File Modification/,
      /Mechanical Repair Autonomy/,
      /Do not ask Manager for permission to make an obvious mechanical repair/,
      /operational closure/,
      /tdd="red"/,
      /tdd="green"/,
      /Confirm true red\/green|confirm.*red.*green|true red\/green/i,
      /named `coder` tool|synchronous `coder` tool/,
      /schema optional `tdd`|prompt-required for `fast-coder`|Manager `fork` of a Coder role/,
    ],
    forbidden: [],
  },

  'fast-inspector': {
    required: [
      /Investigative Inspector/,
      /four investigative instruments: `read`, `glob`, `grep`, and `executor`/i,
      /Absolute Codebase Read-Only Invariant/,
      /Direct File Tools First; `executor` Only for Read-Only Queries/,
      /No Project Workloads or Verification/,
      /Never invoke a compiler, build system, typechecker, linter, formatter, test runner/,
      /a request from Coder to compile, test, validate, reproduce, or modify remains forbidden/,
      /DO NOT compile, build, typecheck, lint, format, test, benchmark, run repository programs/,
      // PROMPT-INSP-002: even when the Parent demands full text, Inspector must
      // refuse that part, explicitly correct the overreach, and return only a
      // structured summary — it must never become a full-text repeater.
      /parent.*(asks|demands|requests).*full|refuse.*full-text|reject.*full-text/i,
      /correct.*overreach|rebuke/i,
      /structured summary only|only a structured summary/i,
    ],
    forbidden: [],
  },

  // REVIEW-003 is Host-owned: the Reviewer must not be told about the double-PERFECT
  // rule, or it would try to run the confirmation itself.
  'fast-reviewer': {
    required: [
      /Uncompromising Reviewer/,
      /Quality Gatekeeper/,
      /verdict\("PERFECT"\)/,
      /verdict\("REVISE"\)/,
    ],
    forbidden: [/Double-PERFECT|two consecutive `PERFECT`|Nope, let's re-evaluate/i],
  },

  'fast-browser': {
    required: [
      /Information Navigator/,
      /`network`/,
      /do \*\*not\*\* have/i,
      /Browser-only web access/i,
      /MUST NOT use [`']read[`'], [`']glob[`'], or [`']grep[`'] to read or search local workspace or repository files/i,
    ],
    forbidden: [],
  },

  'fast-meditator': {
    required: [
      /Architectural Strategist/,
      /Transparent Trade-Off Evaluation/,
      /inspector\(agent: "fast-inspector", prompts\)/,
    ],
    forbidden: [],
  },

  'fast-orchestrator': {
    required: [
      /Multi-Worktree Director/,
      /fork-manager/,
      /Host-owned Dual PERFECT/,
      /fast-manager|deep-manager/,
      // PR B: continue same Manager job; no invented reuse API.
      /originating Manager|existing Manager job|Continue the existing Manager/i,
      /truly independent|真正并行|parallel independent/i,
    ],
    forbidden: [],
  },

  'fast-student': {
    required: [
      /你是 Student/,
      /学习阶段你只有 teacher 工具/,
      /最终苏格拉底反证/,
      /主动结束当前 turn 并进入 idle/,
    ],
    forbidden: [],
  },

  'fast-teacher': {
    required: [
      /你是 Teacher/,
      /同一个 Student 会在持续 Session 中反复向你学习/,
      /调查真实情况/,
      /必须通过 return 工具返回/,
    ],
    forbidden: [],
  },

  // AGENT-008: Executor holds no tools; Blogger/Teacher are also internal but
  // have their dedicated private tool surfaces.
  'fast-executor': {
    required: [/Command Output Summarizer/, /AgentRole\.Executor/, /Tool Capability: \[\] \(NONE\)/],
    forbidden: [],
  },

  'fast-blogger': {
    required: [
      /Work Log Blogger/,
      /only tool is `blog`/,
      /exactly once/,
      /Self-Compression/,
    ],
    forbidden: [/Tools: \[\]/, /no tools/, /Do not call tools/, /DO NOT attempt/],
  },
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
        agentEnumEntries(hooks.tool[toolName].args.agent),
      ]),
    )

    assert.deepEqual(observed, EXPECTED_AGENT_ENUMS)

    // EXEC-003: the PTY signal set, the only other enum on the model-visible surface.
    assert.deepEqual(agentEnumEntries(hooks.tool['fork-pty'].args.signal.def.innerType).sort(), [
      'HUP',
      'INT',
      'KILL',
      'QUIT',
      'TERM',
      'USR1',
      'USR2',
    ])

    // REVIEW-002: a verdict is a tool argument with exactly two values.
    assert.deepEqual(agentEnumEntries(hooks.tool.verdict.args.verdict), ['PERFECT', 'REVISE'])

    // AGENT-005: omitting the agent is not a defaultable choice.
    const omitted = Object.fromEntries(
      Object.keys(EXPECTED_AGENT_ENUMS).map((toolName) => [
        toolName,
        hooks.tool[toolName].args.agent.safeParse(undefined).success,
      ]),
    )
    assert.deepEqual(omitted, { fork: false, 'fork-manager': false, inspector: false, coder: false })
  })
})

test('AGENT_004_006_010_config_gains_a_prompt_and_the_whole_permission_matrix', async () => {
  await withPlugin(async (hooks) => {
    const config = hostFinalConfig()
    hooks.config(config)

    // AGENT-006: one whole-object comparison per agent. Twenty-four of them, because
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
        (key) => key !== '*' && key !== 'external_directory' && permissions[`fast-${role}`][key] === 'allow',
      ).length,
    ])
    const facadeCount = ROLE_NAMES.map((role) => [
      role,
      roles.permissions(roles.of(FACADE_ROLE_CASES[role])).length,
    ])
    assert.deepEqual(allowedCount, facadeCount, 'an allow appearing without a ToolPermission behind it')

    // AGENT-004/005: every managed agent receives a prompt, and the clauses that
    // define its role are present. `mode` is asserted alongside because
    // `applyOwnedFields` writes both and a lost `mode` would strand the agent.
    const shape = {}
    const clauseFailures = []
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
    }

    assert.deepEqual(
      shape,
      Object.fromEntries(
        ROLE_NAMES.flatMap((role) =>
          ['fast', 'deep'].map((tier) => [`${tier}-${role}`, { mode: 'primary', prompt: 'string' }]),
        ),
      ),
    )

    for (const [agent, clauses] of Object.entries(PROMPT_CLAUSES)) {
      const prompt = config.agent[agent].prompt
      for (const pattern of clauses.required) {
        if (!pattern.test(prompt)) clauseFailures.push(`${agent} is missing ${pattern}`)
      }
      for (const pattern of clauses.forbidden) {
        if (pattern.test(prompt)) clauseFailures.push(`${agent} must not mention ${pattern}`)
      }
    }

    assert.deepEqual(clauseFailures, [], 'a missing clause is a capability the agent will misuse')
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
    // HOST-013: every transform inserts one tool-call + tool-result pair before trailing user.
    const transformed = { messages: withSession([{ role: 'user', text: 'hello' }]) }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(transformed.messages.length, 3)
    assert.equal(markerCount(transformed.messages), 2)

    const call = transformed.messages[0]
    const result = transformed.messages[1]
    const user = transformed.messages[2]
    assert.equal(call.info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
    assert.equal(call.parts[0].tool, 'auto-injected')
    assert.equal(call.parts[0].state.status, 'pending')
    assert.equal(result.parts[0].state.status, 'completed')
    assert.equal(result.parts[0].state.output, PAIR_PROGRAMMING_THOUGHT_TEXT)
    assert.equal(call.parts[0].callID, result.parts[0].callID)
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

    assert.equal(transformed.messages.length, 4)
    assert.equal(markerCount(transformed.messages), 2)
    assert.equal(transformed.messages[0].role ?? transformed.messages[0].info?.role, 'user')
    assert.equal(transformed.messages[1].role ?? transformed.messages[1].info?.role, 'assistant')
    assert.equal(transformed.messages[2].info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
    assert.equal(transformed.messages[3].info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
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

    assert.equal(markerCount(transformed.messages) >= 2, true)
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

    assert.equal(markerCount(transformed.messages), 2)
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

    assert.equal(markerCount(transformed.messages), 2)
    assert.equal(transformed.messages.at(-1).role ?? transformed.messages.at(-1).info?.role, 'user')
    assert.equal(transformed.messages.at(-2).info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
    assert.equal(transformed.messages.at(-3).info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
  })
})

test('HOST_013_repeated_transform_of_same_placement_replays_only', async () => {
  await withPlugin(async (hooks) => {
    // HOST-013: a placement occasion that already has a bracket only replays —
    // repeated transform of the same real transcript must not append a pair.
    // Use non-synthetic base so history is re-hydrated from durable/memory ledger.
    const first = { messages: withSession([{ role: 'user', text: 'hello' }], 'ses-repeat') }
    await hooks['experimental.chat.messages.transform']({}, first)
    assert.equal(markerCount(first.messages), 2)
    assert.equal(first.messages.at(-1).role ?? first.messages.at(-1).info?.role, 'user')

    const second = { messages: withSession([{ role: 'user', text: 'hello' }], 'ses-repeat') }
    await hooks['experimental.chat.messages.transform']({}, second)
    assert.equal(markerCount(second.messages), 2, 'same placement must replay, not append a second pair')
    assert.equal(second.messages.at(-1).role ?? second.messages.at(-1).info?.role, 'user')
  })
})

test('HOST_013_new_user_turn_keeps_history_and_appends_new_pair', async () => {
  await withPlugin(async (hooks) => {
    const first = {
      messages: withSession([{ role: 'user', text: 'hello' }], 'ses-turn'),
    }
    await hooks['experimental.chat.messages.transform']({}, first)
    // first: [call, result, user]
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

    // second: [user hello, hist-call, hist-result, next-call, next-result, user second]
    assert.equal(markerCount(second.messages), 4)
    assert.equal(second.messages[1].parts[0].callID, firstCallId)
    assert.notEqual(second.messages[3].parts[0].callID, firstCallId)
    assert.equal(second.messages.at(-1).role ?? second.messages.at(-1).info?.role, 'user')
  })
})

test('HOST_013_companion_blogger_skips_guideline_injection', async () => {
  // HOST-013 scope: durable Companion (Blogger) transcripts must not receive
  // pair-programming auto-injected pairs — they pollute the blog tool contract.
  const { agentFact, sessionId, caseOf } = await import('../../unit/support/domain.mjs')
  const { AgentJournalModule_appendAgent } = await import('../../../dist/Journal/AgentJournal.js')
  const { StreamId } = await import('../../../dist/Journal/Envelope.js')

  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const main = sessionId('ses-main-no-auto-injected')
    const blogger = sessionId('ses-blogger-no-auto-injected')
    const linked = AgentJournalModule_appendAgent(
      new StreamId(1, main),
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
      ['list', {}],
      ['inspector', { agent: 'fast-inspector', prompts: ['git status'] }],
      ['fork', { agent: 'fast-coder', prompt: 'work' }],
    ]) {
      const result = parseToml(await hooks.tool[toolName].execute(args, context))
      assert.deepEqual(Object.keys(result), ['error'], `${toolName} must reject, not run`)
      assert.match(result.error, /no Authority Root fixes this session's role/)
    }
  })
})

test('EXEC_002_one_shot_tools_return_the_managed_agent_and_the_turn_formal_text', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    // Meditator may hold inspector; DevOps (AGENT-015) may hold coder.
    acceptAuthorityRoot(runtime, 'meditator-contract', 'fast-meditator')
    acceptAuthorityRoot(runtime, 'devops-contract', 'fast-devops')

    const inspectorResultP = hooks.tool.inspector.execute(
      { agent: 'fast-inspector', prompts: ['git status'] },
      { sessionID: 'meditator-contract', agent: 'fast-meditator' },
    )
    // 订阅在 prompt 之前安装（OneShotAgentTool.fs:115 → send）：promptAsync 被调用即
    // terminal 订阅就绪。此前直接 notify 与 execute 内部安装竞态——通知被丢弃则 execute
    // 永远等不到结局（实测 1000ms 判据线）。
    await awaitPrompted(createdIds[0])
    notifyCompleted(runtime, createdIds[0], 'inspector session-wide A', 'inspector turn formal report')
    const inspectorText = await inspectorResultP
    const inspectorResult = parseToml(inspectorText)

    // Data-only fields of the TOML result. The natural-language output is carried
    // as the leading instruction comment (docs/how/synthetic-toml.md), so it is asserted on the raw
    // text rather than as a parsed field.
    assert.deepEqual(inspectorResult, {
      inspector_id: createdIds[0],
      agent: 'fast-inspector',
      tier: 'fast',
      fallback_peer: 'deep-inspector',
      parent_b_digest: '',
    })
    // EXEC-028: entry-local LWR (includeOpening=false) + TurnFormalText; no work_record field.
    assert.match(inspectorText, /# (Work log|Final output|Uncompressed tail)/)
    assert.ok(!inspectorText.includes('# Opening task'))
    assert.doesNotMatch(inspectorText, /# # /)
    assert.equal(inspectorResult.work_record, undefined)
    assert.ok(!/(^|\n)\s*work_record\s*=/.test(inspectorText))
    assert.ok(inspectorText.includes('inspector turn formal report'))
    assert.equal(inspectorResult.error, undefined)

    const coderResultP = hooks.tool.coder.execute(
      { agent: 'fast-coder', tdd: 'green', prompts: ['apply the requested edit'] },
      { sessionID: 'devops-contract', agent: 'fast-devops' },
    )
    await awaitPrompted(createdIds[1])
    notifyCompleted(runtime, createdIds[1], 'coder session-wide A', 'coder turn formal report')
    const coderText = await coderResultP
    const coderResult = parseToml(coderText)

    // CoderTool: data-only fields, natural-language output as leading comment (docs/how/synthetic-toml.md).
    // tdd is the normalized wire name of the required phase.
    assert.deepEqual(coderResult, {
      coder_id: createdIds[1],
      agent: 'fast-coder',
      tier: 'fast',
      fallback_peer: 'deep-coder',
      tdd: 'green',
      parent_b_digest: '',
    })
    assert.match(coderText, /# (Work log|Final output|Uncompressed tail)/)
    assert.ok(!coderText.includes('# Opening task'))
    assert.doesNotMatch(coderText, /# # /)
    assert.equal(coderResult.work_record, undefined)
    assert.ok(!/(^|\n)\s*work_record\s*=/.test(coderText))
    assert.ok(coderText.includes('coder turn formal report'))
    assert.equal(coderResult.error, undefined)

    // Child assignment must carry the GREEN phase constraint (not metadata-only).
    // OpenCodePort promptAsync shape: { path: { id }, body: { parts, … } }.
    const promptTextFor = (sessionId) => {
      const entry = runtime.prompts.find((p) => (p?.path?.id ?? p?.sessionID) === sessionId)
      assert.ok(entry, `coder child ${sessionId} must receive a prompt`)
      return entry.body.parts[0].text
    }
    const greenBody = promptTextFor(createdIds[1])
    assert.match(greenBody, /TDD phase: GREEN/)
    assert.match(greenBody, /Do not delete, skip, loosen, or rewrite the test/)
    assert.match(greenBody, /apply the requested edit/)

    // RED path: success + child prompt forbids production fix.
    const redResultP = hooks.tool.coder.execute(
      { agent: 'fast-coder', tdd: 'red', prompt: 'failing test for missing behavior' },
      { sessionID: 'devops-contract', agent: 'fast-devops' },
    )
    await awaitPrompted(createdIds[2])
    notifyCompleted(runtime, createdIds[2], 'red session-wide A', 'red turn formal report')
    const redText = await redResultP
    const redResult = parseToml(redText)
    assert.equal(redResult.tdd, 'red')
    assert.equal(redResult.error, undefined)
    assert.match(redText, /# (Work log|Final output|Uncompressed tail)/)
    assert.ok(!redText.includes('# Opening task'))
    assert.doesNotMatch(redText, /# # /)
    assert.equal(redResult.work_record, undefined)
    assert.ok(!/(^|\n)\s*work_record\s*=/.test(redText))
    assert.ok(redText.includes('red turn formal report'))
    const redBody = promptTextFor(createdIds[2])
    assert.match(redBody, /TDD phase: RED/)
    assert.match(redBody, /Do not implement the production fix/)
    assert.match(redBody, /failing test for missing behavior/)

    // Missing / illegal tdd fail closed (no default green).
    const missing = parseToml(
      await hooks.tool.coder.execute(
        { agent: 'fast-coder', prompts: ['no tdd'] },
        { sessionID: 'devops-contract', agent: 'fast-devops' },
      ),
    )
    assert.match(missing.error, /missing required argument: tdd/)

    for (const bad of ['RED', 'test', 'refactor', 'blue', '']) {
      const illegal = parseToml(
        await hooks.tool.coder.execute(
          { agent: 'fast-coder', tdd: bad, prompt: 'x' },
          { sessionID: 'devops-contract', agent: 'fast-devops' },
        ),
      )
      assert.ok(illegal.error, `tdd=${JSON.stringify(bad)} must fail`)
      assert.match(illegal.error, /missing required argument: tdd|UnknownTddPhase/)
    }
  })
})

test('GLORY_031_manager_fork_of_a_reviewer_is_denied_role_based', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-reverted-root', 'fast-manager')

    // GLORY-002/031: a Manager must never create, reuse or nudge a Reviewer;
    // the Reviewer is Host-owned. Denied by durable role, before any prompt.
    const result = parseToml(
      await hooks.tool.fork.execute(
        { agent: 'fast-reviewer', prompt: 'Review the current tree.' },
        { sessionID: 'manager-reverted-root', agent: 'fast-manager' },
      ),
    )

    assert.equal(result.error, 'Unknown or unavailable managed agent.')
    assert.equal(runtime.prompts.length, 0)

    const deepResult = parseToml(
      await hooks.tool.fork.execute(
        { agent: 'deep-reviewer', prompt: 'Review the same current tree.' },
        { sessionID: 'manager-reverted-root', agent: 'fast-manager' },
      ),
    )
    assert.equal(deepResult.error, 'Unknown or unavailable managed agent.')
    assert.equal(runtime.prompts.length, 0)
  })
})

test('EXEC_002_EXEC_004_fork_join_and_list_carry_the_same_mailbox_identity', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    // AGENT-013: fork/join/list belong to the Manager alone.
    acceptAuthorityRoot(runtime, 'manager-contract', 'fast-manager')
    const context = { sessionID: 'manager-contract', agent: 'fast-manager' }

    // An unknown agent is rejected inside execute, not by the schema — the
    // fork.agent schema is a union with string() (AGENT-009 note above). The
    // near-miss suggestion is matched, not pinned whole (ManagedAgent.fs:140-143).
    const unknown = parseToml(await hooks.tool.fork.execute({ agent: 'deep-inspecter', prompt: 'work' }, context))
    assert.deepEqual(Object.keys(unknown), ['error'])
    assert.match(unknown.error, /Legacy agent name|Unknown managed agent 'deep-inspecter'/)
    assert.match(unknown.error, /fast-inspector|deep-inspector/)

    const fork = parseToml(await hooks.tool.fork.execute({ agent: 'fast-coder', prompt: 'work' }, context))
    // ForkTool.fs:22-37: the whole fork payload.
    assert.deepEqual(Object.keys(fork).sort(), ['agent', 'agent_id', 'fallback_peer', 'role', 'tier'])
    assert.match(fork.agent_id, /^[a-z0-9]{6}$/)
    assert.equal(fork.agent, 'fast-coder')
    assert.equal(fork.role, 'coder')
    assert.equal(fork.tier, 'fast')
    assert.equal(fork.fallback_peer, 'deep-coder')

    // EXEC-004: register the forked handle so the terminal delivery below also
    // claims the durable completion cell before join retires it (fixture docs).
    runtime.recordFork('manager-contract', fork.agent_id, createdIds[0])

    const joinResultP = hooks.tool.join.execute({}, context)
    notifyCompleted(runtime, createdIds[0], 'forked coder session-wide A', 'forked coder turn formal report')
    const joinText = await joinResultP
    const join = parseToml(joinText)

    // EXEC-004 rev.2 / docs/how/synthetic-toml.md ### Join / fork: batch wire — status + count + [[result]].
    // Single completion still uses [[result]] (count=1, ordinal=1, kind=agent).
    // work_record is entry-local comment, never a TOML field.
    // Fixture has no Opening capture → LWR empty → no # comment block before [[result]].
    assert.equal(join.status, 'completed')
    assert.equal(join.count, 1)
    assert.ok(Array.isArray(join.result), '[[result]] must parse as array')
    assert.equal(join.result.length, 1)
    assert.deepEqual(join.result[0], {
      ordinal: 1,
      kind: 'agent',
      status: 'completed',
      agent: 'fast-coder',
    })
    assert.equal(join.work_record, undefined, 'work_record must not be a TOML field')
    assert.ok(!joinText.includes('work_record ='), 'wire must not contain work_record = field line')
    assert.ok(joinText.includes('[[result]]'), 'single result still uses [[result]]')
    assert.ok(!joinText.includes('# Opening task'), 'join LWR must not echo the child opening')
    assert.ok(!joinText.includes('forked coder turn formal report'))
    assert.ok(!joinText.includes('run-'), 'no run id on the LLM-visible wire')
    assert.ok(!joinText.includes('child_session_id'), 'no child session id on the wire')

    const list = parseToml(await hooks.tool.list.execute({}, context))
    // EXEC-005 明文「不包含 Retired」：join 已退休该 handle，派生视图为空。
    // 此断言曾按「runtime record 保留完成记录」的假设期望一条 idle 条目——实测生产
    // 返回 []，该假设是测试想象而非契约。
    assert.deepEqual(list, {})
  })
})

// Phase 4 / corrective §7.1: real chat.message (keyless external human) wakes a
// blocked JoinTool via JoinInterruptRegistry → reason=user_message. Must not use
// OperatorAbort or tool abort controller as the primary stimulus.
test('EXEC_017_blocked_join_wakes_on_user_message_from_chat_message', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-user-wake', 'fast-manager')
    const context = { sessionID: 'manager-user-wake', agent: 'fast-manager' }

    const fork = parseToml(await hooks.tool.fork.execute({ agent: 'fast-coder', prompt: 'work' }, context))
    assert.equal(fork.error, undefined, `fork failed: ${fork.error}`)
    // Join blocks waiting only when an active handle is recorded.
    runtime.recordFork('manager-user-wake', fork.agent_id, createdIds[0])

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
    const wire = parseToml(text)
    assert.equal(wire.status, 'interrupted')
    assert.equal(wire.reason, 'user_message')
    assert.notEqual(wire.reason, 'operator_abort')
    assert.ok(!text.includes('operator_abort'), 'user_message path must not emit operator_abort')
    assert.equal(wire.message, undefined, 'user_message wire omits operator join-interrupted message')

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
    const join2 = parseToml(join2Text)
    assert.equal(join2.status, 'completed', `late join after user_message must harvest child: ${join2Text}`)
    assert.equal(join2.count, 1)
    assert.equal(join2.result?.[0]?.status, 'completed')
    assert.equal(join2.result?.[0]?.agent, 'fast-coder')
    assert.equal(runtime.abortedIds.includes(createdIds[0]), false, 'user_message must not abort the child session')
  })
})

test('EXEC_002_fork_existing_agent_id_reuses_child_without_new_session', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-reuse', 'fast-manager')
    const context = { sessionID: 'manager-reuse', agent: 'fast-manager' }

    const created = parseToml(
      await hooks.tool.fork.execute({ agent: 'fast-coder', prompt: 'first assignment' }, context),
    )
    assert.equal(created.error, undefined, `create fork failed: ${created.error}`)
    assert.equal(created.agent, 'fast-coder')
    assert.equal(createdIds.length, 1, 'managed name creates exactly one child session')
    const agentId = created.agent_id
    assert.match(agentId, /^[a-z0-9]{6}$/)
    const promptsAfterCreate = runtime.prompts.length
    assert.ok(promptsAfterCreate >= 1, 'create path must send a child prompt')

    // PROMPT-005: the create fork is AwaitMode.Detached with a receipt-only stub —
    // Claimed → Submitted, no PhysicalAccepted, so the child has NO ActiveLogicalRun.
    // BusyAgentNudge requires one (HostForkBusyNudge.fs:37). Accept the pending
    // AgentOwnerRoot claim on the child before the busy reuse below.
    const childSessionId = createdIds[0]
    // PromptKey is on the last SendPrompt metadata for that child (PROMPT-011).
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

    // Busy reuse: child still active (no terminal yet). ForkTool TryFindAgent → Reuse →
    // sendToExistingChild active-run branch → BusyAgentNudge. No session.create.
    const nudged = parseToml(
      await hooks.tool.fork.execute({ agent: agentId, prompt: 'nudge: add one constraint' }, context),
    )
    assert.equal(nudged.error, undefined, `reuse/nudge failed: ${nudged.error}`)
    assert.equal(nudged.agent_id, agentId, 'reuse returns the same agent_id')
    assert.equal(nudged.agent, 'fast-coder')
    assert.equal(createdIds.length, 1, 'reuse must not create a second child session')
    assert.ok(
      runtime.prompts.length > promptsAfterCreate,
      'busy reuse must deliver a nudge prompt to the existing child',
    )

    // Managed name again is always create — not silent reuse of the first child.
    const twin = parseToml(
      await hooks.tool.fork.execute({ agent: 'fast-coder', prompt: 'parallel twin work' }, context),
    )
    assert.equal(twin.error, undefined, `second create failed: ${twin.error}`)
    assert.notEqual(twin.agent_id, agentId, 'managed name creates a distinct handle')
    assert.equal(createdIds.length, 2, 'managed name create adds a second child record')
  })
})

test('EXEC_002_fork_optional_tdd_injects_phase_or_fail_closed', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-fork-tdd', 'fast-manager')
    const context = { sessionID: 'manager-fork-tdd', agent: 'fast-manager' }

    const promptTextFor = (sessionId) => {
      const entry = [...runtime.prompts].reverse().find((p) => (p?.path?.id ?? p?.sessionID) === sessionId)
      assert.ok(entry, `fork child ${sessionId} must receive a prompt`)
      return entry.body.parts[0].text
    }

    // tdd=red → RED constraint composed into child assignment.
    const red = parseToml(
      await hooks.tool.fork.execute(
        { agent: 'fast-coder', tdd: 'red', prompt: 'failing test for missing index' },
        context,
      ),
    )
    assert.equal(red.error, undefined, `fork tdd=red failed: ${red.error}`)
    const redBody = promptTextFor(createdIds[0])
    assert.match(redBody, /TDD phase: RED/)
    assert.match(redBody, /Do not implement the production fix/)
    assert.match(redBody, /failing test for missing index/)

    // tdd=green → GREEN constraint.
    const green = parseToml(
      await hooks.tool.fork.execute(
        { agent: 'deep-coder', tdd: 'green', prompt: 'minimal production fix only' },
        context,
      ),
    )
    assert.equal(green.error, undefined, `fork tdd=green failed: ${green.error}`)
    const greenBody = promptTextFor(createdIds[1])
    assert.match(greenBody, /TDD phase: GREEN/)
    assert.match(greenBody, /Do not delete, skip, loosen, or rewrite the test/)
    assert.match(greenBody, /minimal production fix only/)

    // No tdd → behavior unchanged (no phase injection).
    const plain = parseToml(
      await hooks.tool.fork.execute({ agent: 'fast-inspector', prompt: 'static fact only' }, context),
    )
    assert.equal(plain.error, undefined, `fork without tdd failed: ${plain.error}`)
    const plainBody = promptTextFor(createdIds[2])
    assert.doesNotMatch(plainBody, /TDD phase:/)
    assert.match(plainBody, /static fact only/)

    // Illegal tdd → fail-closed (same wire parse as coder tool).
    // Empty / omitted is optional-absent (OptionalText), not illegal.
    for (const bad of ['RED', 'test', 'refactor', 'blue']) {
      const illegal = parseToml(
        await hooks.tool.fork.execute({ agent: 'fast-coder', tdd: bad, prompt: 'x' }, context),
      )
      assert.ok(illegal.error, `fork tdd=${JSON.stringify(bad)} must fail`)
      assert.match(illegal.error, /UnknownTddPhase/)
    }

    // Busy reuse + tdd: compose into nudge prompt text.
    const reuseCreate = parseToml(
      await hooks.tool.fork.execute(
        { agent: 'fast-coder', tdd: 'red', prompt: 'open red assignment' },
        context,
      ),
    )
    assert.equal(reuseCreate.error, undefined)
    const reuseChildSessionId = createdIds[createdIds.length - 1]
    const reusePrompt = [...runtime.prompts].reverse().find((p) => {
      const id = p?.path?.id ?? p?.sessionID ?? p?.sessionId
      return id === reuseChildSessionId
    })
    const reuseKey =
      reusePrompt?.body?.metadata?.wanxiangshu_prompt_key ??
      reusePrompt?.body?.parts?.find((part) => part?.type === 'text')?.metadata?.wanxiangshu_prompt_key
    acceptChildAgentOwnerRoot(runtime, reuseChildSessionId, reuseKey)
    const promptsBeforeNudge = runtime.prompts.length
    const reuseNudge = parseToml(
      await hooks.tool.fork.execute(
        { agent: reuseCreate.agent_id, tdd: 'green', prompt: 'switch to green constraint' },
        context,
      ),
    )
    assert.equal(reuseNudge.error, undefined, `reuse/nudge with tdd failed: ${reuseNudge.error}`)
    assert.equal(reuseNudge.agent_id, reuseCreate.agent_id)
    assert.ok(runtime.prompts.length > promptsBeforeNudge, 'nudge must deliver a prompt')
    const nudgeBody = promptTextFor(reuseChildSessionId)
    assert.match(nudgeBody, /TDD phase: GREEN/)
    assert.match(nudgeBody, /switch to green constraint/)
  })
})

test('EXEC_002_fork_tool_description_states_create_or_reuse_by_agent_id', async () => {
  await withPlugin(async (hooks) => {
    const description = hooks.tool.fork?.description
    assert.equal(typeof description, 'string', 'fork tool must expose description')
    assert.match(description, /reuse|agent_id/i)
    assert.match(description, /Create a managed agent|reuse\/nudge/i)
    // orchestratorSpec stays create-only wording.
    const managerJob = hooks.tool['fork-manager']?.description
    assert.equal(typeof managerJob, 'string')
    assert.match(managerJob, /Fork a manager job/)
    assert.match(managerJob, /reuse|existing manager job|job id/i)
  })
})

test('EXEC_002_the_fixture_delivers_the_real_journal_and_terminal_port', async () => {
  await withExecutablePlugin(async (_hooks, directory, _createdIds, runtime) => {
    // The runtime the fixture hands over must BE the production instances:
    // - the journal's RuntimeId is the id stamped into every NDJSON envelope on
    //   disk (same runtime stream), and
    // - NotifyTerminal through the handed-over port is what join observed above
    //   (proven there by a join that only returns after the notification).
    acceptAuthorityRoot(runtime, 'manager-fixture-probe', 'fast-manager')

    const commonDirectory = execFileSync('git', ['-C', directory, 'rev-parse', '--git-common-dir'], {
      encoding: 'utf8',
    }).trim()
    const gitDirectory = isAbsolute(commonDirectory) ? commonDirectory : resolve(directory, commonDirectory)
    const runtimeDirectory = join(gitDirectory, 'wanxiangshu-next', 'runtimes')
    const streams = readdirSync(runtimeDirectory).filter((name) => name.endsWith('.ndjson'))
    assert.deepEqual(streams, [`${runtime.runtimeId}.ndjson`])
    const envelope = JSON.parse(readFileSync(join(runtimeDirectory, streams[0]), 'utf8').split('\n')[0])
    // Fable 线格式：PascalCase 键，单 case union 序列化为 [caseName, value] 对。
    assert.deepEqual(envelope.RuntimeId, ['RuntimeId', runtime.runtimeId])
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
    const context = { sessionID: 'manager-suicide-outstanding', agent: 'fast-manager', callID: 'call_suicide_1', messageID: 'msg_1' }

    // Fork a child agent so there is an active child handle
    await hooks.tool.fork.execute(
      { agent: 'fast-coder', prompt: 'Do work', tdd: 'green' },
      context,
    )

    // Outstanding-child refusal is instruction-only (comment wire); parseToml strips comments.
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

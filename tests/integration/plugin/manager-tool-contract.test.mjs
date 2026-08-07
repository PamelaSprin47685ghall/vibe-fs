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
} from '../../unit/plugin/plugin-fixture.mjs'

/** AGENT-002: the twenty managed agents, exactly as the Host-final config names them. */
const ROLE_NAMES = [
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
  suicide: { last_words: 'required' },
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
    'deep-reviewer',
    'fast-browser',
    'fast-coder',
    'fast-devops',
    'fast-inspector',
    'fast-meditator',
    'fast-reviewer',
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
  'inspector',
  'coder',
  'executor',
  'network',
  'verdict',
  'blog',
  'suicide',
]

/** AGENT-006/011/013/014/015: the allowed tools per role. Everything else denies. */
const ALLOWED_TOOLS = {
  orchestrator: ['fork-manager', 'join'],
  manager: ['fork', 'join', 'list', 'suicide'],
  coder: ['read', 'write', 'edit', 'glob', 'grep', 'inspector', 'mv', 'rm'],
  inspector: ['read', 'glob', 'grep', 'executor'],
  devops: ['fork-pty', 'join', 'list', 'read', 'glob', 'grep', 'inspector', 'coder', 'executor'],
  browser: ['read', 'glob', 'grep', 'network'],
  meditator: ['read', 'glob', 'grep', 'inspector'],
  reviewer: ['read', 'glob', 'grep', 'inspector', 'verdict'],
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
      /Do not ask DevOps to edit files/,
      /agent_id/,
      /\blist\b/,
      /compatible context/,
      /Do not reuse an agent/,
      /tdd="red"/,
      /tdd="green"/,
      /suicide\(last_words\)/,
      /When no useful action remains, call/,
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
    forbidden: [/Double-PERFECT|two consecutive `PERFECT`|confirmation|Nope, let's re-evaluate/i],
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

  // AGENT-008: internal agents hold no tools, and their prompts say so.
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

/** HOST-013: count markers by their source identity, never by text (spec forbids text-only filtering). */
const markerCount = (messages) =>
  messages.filter((message) => message?.info?.source === PAIR_PROGRAMMING_THOUGHT_SOURCE).length

test('CTX_002_transform_injects_exactly_one_pair_programming_thought', async () => {
  await withPlugin(async (hooks) => {
    // HOST-013: after the latest user message the transform must insert exactly
    // one provider-visible synthetic assistant thought. The first user message
    // survives byte-identical; the marker carries the frozen source/synthetic
    // identity and the exact reasoning part.
    const transformed = { messages: [{ role: 'user', text: 'hello' }] }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(transformed.messages.length, 2)
    assert.deepEqual(transformed.messages[0], { role: 'user', text: 'hello' })

    const marker = transformed.messages[1]
    assert.equal(marker.info.role, 'assistant')
    assert.equal(marker.info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
    assert.equal(marker.info.synthetic, true)
    assert.equal(marker.parts.length, 1)
    assert.equal(marker.parts[0].type, 'reasoning')
    assert.equal(marker.parts[0].text, PAIR_PROGRAMMING_THOUGHT_TEXT)

    // VERIFY-003: the injected message must not smuggle any test-only
    // `[CAPS]`/`[REVIEW]`/`[HINT]:` marker into the production prompt.
    const markerRe = /\[(CAPS|REVIEW|HINT):/
    const marked = transformed.messages
      .flatMap((message) => [message.text ?? '', ...(message.parts ?? []).map((part) => part.text ?? '')])
      .filter((text) => markerRe.test(text))
    assert.deepEqual(marked, [])
  })
})

test('HOST_013_marker_inserts_after_anchor_not_after_later_assistant', async () => {
  await withPlugin(async (hooks) => {
    // HOST-013: the anchor is the latest user message. An assistant shell that
    // already follows it must be displaced — never blindly appended at the end.
    const transformed = {
      messages: [
        { role: 'user', text: 'hello' },
        { role: 'assistant', text: 'ok' },
      ],
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(transformed.messages.length, 3)
    assert.deepEqual(transformed.messages[0], { role: 'user', text: 'hello' })
    assert.equal(transformed.messages[1].info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
    assert.deepEqual(transformed.messages[2], { role: 'assistant', text: 'ok' })
  })
})

test('HOST_013_empty_messages_insert_nothing', async () => {
  await withPlugin(async (hooks) => {
    // HOST-013: no anchor, no marker — empty history stays empty.
    const transformed = { messages: [] }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.deepEqual(transformed.messages, [])
  })
})

test('HOST_013_system_and_assistant_history_only_inserts_nothing', async () => {
  await withPlugin(async (hooks) => {
    // HOST-013: without a user or completed tool-result anchor the history must
    // pass through untouched.
    const transformed = {
      messages: [
        { role: 'system', text: 'rules' },
        { role: 'assistant', text: 'ok' },
      ],
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.deepEqual(transformed.messages, [
      { role: 'system', text: 'rules' },
      { role: 'assistant', text: 'ok' },
    ])
  })
})

test('HOST_013_marker_appends_after_every_anchor', async () => {
  await withPlugin(async (hooks) => {
    // HOST-013: every anchor — the user message AND the completed tool-result
    // message — gets its own marker, in order. The tool shapes follow the
    // decode path's accepted forms (Projection.decodePart `tool_result` /
    // `tool-call`, HOST-012).
    const toolResultMessage = {
      info: { role: 'tool' },
      parts: [{ type: 'tool_result', callID: 'call_1', result: 'ok' }],
    }
    const transformed = {
      messages: [
        { role: 'user', text: 'hello' },
        {
          info: { role: 'assistant' },
          parts: [{ type: 'tool-call', callID: 'call_1', tool: 'read', args: '{}' }],
        },
        toolResultMessage,
      ],
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(transformed.messages.length, 5)
    assert.deepEqual(transformed.messages[0], { role: 'user', text: 'hello' })
    assert.equal(transformed.messages[1].info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
    assert.deepEqual(transformed.messages[3], toolResultMessage)
    assert.equal(transformed.messages[4].info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
    assert.equal(markerCount(transformed.messages), 2)
  })
})

test('HOST_013_repeated_transform_injects_no_duplicate_marker', async () => {
  await withPlugin(async (hooks) => {
    // HOST-013 idempotency: re-running the transform over the same output must
    // not add a second marker for the same anchor.
    const transformed = { messages: [{ role: 'user', text: 'hello' }] }
    await hooks['experimental.chat.messages.transform']({}, transformed)
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(transformed.messages.length, 2)
    assert.equal(markerCount(transformed.messages), 1)
  })
})

test('HOST_013_new_user_turn_adds_marker_and_keeps_previous', async () => {
  await withPlugin(async (hooks) => {
    // HOST-013: a previous turn's marker is not an anchor. The new user message
    // is; this round's marker lands after it while the earlier marker survives.
    const previousMarker = {
      info: {
        id: 'marker-prev',
        role: 'assistant',
        source: PAIR_PROGRAMMING_THOUGHT_SOURCE,
        synthetic: true,
      },
      parts: [{ type: 'reasoning', text: PAIR_PROGRAMMING_THOUGHT_TEXT }],
    }
    const transformed = {
      messages: [
        { role: 'user', text: 'hello' },
        previousMarker,
        { role: 'user', text: 'second turn' },
      ],
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.equal(transformed.messages.length, 4)
    assert.deepEqual(transformed.messages[1], previousMarker)
    assert.equal(transformed.messages[3].info.source, PAIR_PROGRAMMING_THOUGHT_SOURCE)
    assert.equal(markerCount(transformed.messages), 2)
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
    // Reviewer (AGENT-014) may hold inspector; DevOps (AGENT-015) may hold coder.
    acceptAuthorityRoot(runtime, 'reviewer-contract', 'fast-reviewer')
    acceptAuthorityRoot(runtime, 'devops-contract', 'fast-devops')

    const inspectorResultP = hooks.tool.inspector.execute(
      { agent: 'fast-inspector', prompts: ['git status'] },
      { sessionID: 'reviewer-contract', agent: 'fast-reviewer' },
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
    assert.ok(inspectorText.includes('inspector turn formal report'))
    assert.ok(!inspectorText.includes('inspector session-wide A'))

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
    assert.ok(coderText.includes('coder turn formal report'))
    assert.ok(!coderText.includes('coder session-wide A'))

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

    assert.equal(result.error, 'That path is not yours to command. Continue your own work, or call suicide when nothing useful remains.')
    assert.equal(runtime.prompts.length, 0)

    const deepResult = parseToml(
      await hooks.tool.fork.execute(
        { agent: 'deep-reviewer', prompt: 'Review the same current tree.' },
        { sessionID: 'manager-reverted-root', agent: 'fast-manager' },
      ),
    )
    assert.equal(deepResult.error, 'That path is not yours to command. Continue your own work, or call suicide when nothing useful remains.')
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

    // EXEC-004 rev.2 / docs/how/synthetic-toml.md §9.6: batch wire — status + count + [[result]].
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
    assert.doesNotMatch(managerJob, /reuse/i)
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

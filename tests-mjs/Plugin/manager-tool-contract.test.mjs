// tests-mjs/Plugin/manager-tool-contract.test.mjs — AGENT-004/006/009/010, CTX-002.
//
// Layer 2 (resource contract): what the Host sees after `initSpikePlugin` — the tool
// registry, the argument schemas the provider is offered, and the `opencode.json`
// mutation `hooks.config` performs. No mock provider, no HTTP server, no port or
// HOME/XDG isolation; a `git init` into a `mkdtemp` dir is the whole world, because
// the journal is addressed through the Git common directory (PERSIST-006).
//
// `build/next/OpenCode/SpikePlugin.js` is imported directly rather than through
// `tests-mjs/domain.mjs`. That facade deliberately exports zero `OpenCode/*` modules,
// and the schemas here are not F# values at all: `ToolHostCodec.fs:78-96` emits
// `$0.schema.string()` / `$0.schema.union([...])` against the Host's own zod builder,
// so only a real `initSpikePlugin({ client: {} , ... })` produces them. A direct
// import of the plugin entry is the same precedent `host-hooks.test.mjs` sets.
//
// `domain.mjs` is imported for `roles.permissions` alone, as an independent second
// source for the permission matrix — see the cross-check note below.

import assert from 'node:assert/strict'
import test from 'node:test'
import { roles } from '../domain.mjs'
import { withPlugin } from './plugin-fixture.mjs'

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
  coder: { agent: 'required', prompt: 'optional', prompts: 'optional' },
  executor: {
    command: 'required',
    estimated_mem_usage: 'required',
    estimated_output_bytes: 'required',
    estimated_running_secs: 'required',
  },
  fork: { agent: 'required', prompt: 'optional' },
  'fork-manager': { agent: 'required', prompt: 'required' },
  'fork-pty': { agent: 'required', prompt: 'optional', signal: 'optional' },
  inspector: { agent: 'required', prompt: 'optional', prompts: 'optional' },
  join: {},
  list: {},
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
  'inspector',
  'coder',
  'executor',
  'network',
  'verdict',
]

/** AGENT-006/011/013/014/015: the allowed tools per role. Everything else denies. */
const ALLOWED_TOOLS = {
  orchestrator: ['fork-manager', 'join'],
  manager: ['fork', 'join', 'list'],
  coder: ['read', 'write', 'edit', 'glob', 'grep', 'inspector'],
  inspector: ['read', 'glob', 'grep', 'executor'],
  devops: ['fork-pty', 'join', 'list', 'read', 'glob', 'grep', 'inspector', 'coder', 'executor'],
  browser: ['read', 'glob', 'grep', 'network'],
  meditator: ['read', 'glob', 'grep', 'inspector'],
  reviewer: ['read', 'glob', 'grep', 'inspector', 'verdict'],
  blogger: [],
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
 * So the matrix is pinned literally against SSOT/02 AGENT-006, and the facade is used
 * below only for what it can say independently: how many tools a role may hold.
 */
const expectedPermission = (role) =>
  Object.fromEntries(KNOWN_TOOL_KEYS.map((key) => [key, ALLOWED_TOOLS[role].includes(key) ? 'allow' : 'deny']))

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
      /Manager thinks and delegates/,
      /fork\(agent, prompt\)/,
      /Treat every `join\(\)` as a deliberate blocking point/,
      /work already known and work newly exposed by the latest facts/,
      /fast-coder/,
      /Never assign verification to a Coder/,
      /Do not ask a Coder to run, check, diagnose, or interpret compilation, builds, typechecks, linters, tests, or program execution/,
      /Do not ask a Coder to obtain any of those results through Inspector/,
      /Once its edits are complete, the Coder is done/,
      /DO NOT delegate local workspace reading or search to [`']fast-browser[`'] \/ [`']deep-browser[`']/i,
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
    ],
    forbidden: [/executor/i],
  },

  'fast-devops': {
    required: [/DevOps executes/, /fork-pty/, /No Direct File Modification/],
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
      /Render a Verdict Only After Rigorous Review/,
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
      /AgentRole\.Blogger/,
      /Tool Capability: \[\] \(NONE\)/,
      /Self-Compression/,
    ],
    forbidden: [],
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
    const allowedCount = ROLE_NAMES.map((role) => [
      role,
      KNOWN_TOOL_KEYS.filter((key) => key !== '*' && permissions[`fast-${role}`][key] === 'allow').length,
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

test('CTX_002_the_transform_injects_no_synthetic_marker', async () => {
  await withPlugin(async (hooks) => {
    // With no committed prefix snapshot the transform has nothing to restore, so raw
    // history must come back byte-identical. A synthetic `[CAPS]`/`[REVIEW]`/`[HINT]`
    // head here would be a test-only marker in a production prompt (VERIFY-003).
    const transformed = { messages: [{ role: 'user', text: 'hello' }] }
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.deepEqual(transformed.messages, [{ role: 'user', text: 'hello' }])

    const markerRe = /\[(CAPS|REVIEW|HINT):/
    const marked = transformed.messages
      .flatMap((message) => [message.text ?? '', ...(message.parts ?? []).map((part) => part.text ?? '')])
      .filter((text) => markerRe.test(text))
    assert.deepEqual(marked, [])
  })
})

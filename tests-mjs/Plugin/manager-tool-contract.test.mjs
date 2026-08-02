// tests-mjs/Plugin/manager-tool-contract.test.mjs — AGENT-004/006/009/010, CTX-002.
//
// Layer 2 (resource contract): what the Host sees after `initSpikePlugin` — the tool
// registry, the argument schemas the provider is offered, and the `opencode.json`
// mutation `hooks.config` performs. No mock provider, no HTTP server, no port or
// HOME/XDG isolation; a `git init` into a `mkdtemp` dir is the whole world, because
// the journal is addressed through the Git common directory (PERSIST-006).
//
// The plugin entry (`Infrastructure/OpenCode/Plugin/SpikePlugin.js`) is imported directly rather than through
// `tests-mjs/domain.mjs`. That facade deliberately exports zero `Infrastructure/OpenCode/*` modules,
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
import { roles } from '../domain.mjs'
import { withPlugin, withExecutablePlugin, acceptAuthorityRoot, notifyCompleted, awaitPrompted } from './plugin-fixture.mjs'

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

// ── the execute path (EXEC-002, EXEC-004, AGENT-007 layer two) ───────────────
//
// Everything above is layer 2: what the Host is OFFERED. The three tests below
// are what the shock-anneal archive (FINAL-REPORT §8) recorded as never passing in the deleted
// `testkit/opencode/tests/manager-tool-contract.mjs`: actually invoking
// `hooks.tool.*.execute`. Two independent defects kept them red:
//
//   1. No session transport under `client: {}` — production had briefly
//      FABRICATED a completed AgentRunResult carrying "test output"
//      (src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/Sessions.fs:149-153 records its removal), so the old
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
  // the one the clause names as the thing to delete (SSOT/02).
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
    // as the leading instruction comment (SSOT/13), so it is asserted on the raw
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
      { agent: 'fast-coder', prompts: ['apply the requested edit'] },
      { sessionID: 'devops-contract', agent: 'fast-devops' },
    )
    await awaitPrompted(createdIds[1])
    notifyCompleted(runtime, createdIds[1], 'coder session-wide A', 'coder turn formal report')
    const coderText = await coderResultP
    const coderResult = parseToml(coderText)

    // CoderTool.fs:9-21: data-only fields, with the natural-language output as a
    // leading comment (SSOT/13).
    assert.deepEqual(coderResult, {
      coder_id: createdIds[1],
      agent: 'fast-coder',
      tier: 'fast',
      fallback_peer: 'deep-coder',
      parent_b_digest: '',
    })
    assert.ok(coderText.includes('coder turn formal report'))
    assert.ok(!coderText.includes('coder session-wide A'))
  })
})

test('REVIEW_007_reverted_human_root_is_not_required_by_reviewer', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-reverted-root', 'fast-manager')

    const result = parseToml(
      await hooks.tool.fork.execute(
        { agent: 'fast-reviewer', prompt: 'Review the current tree.' },
        { sessionID: 'manager-reverted-root', agent: 'fast-manager' },
      ),
    )

    assert.equal(result.error, undefined)
    assert.equal(runtime.prompts.length, 1)
    assert.doesNotMatch(runtime.prompts[0].body.parts[0].text, /\[\[original_user_requirement\]\]/)

    runtime.messages.push({
      info: { id: 'root-manager-reverted-root', role: 'user' },
      parts: [{ type: 'text', text: 'Requirement that survived compaction.' }],
    })
    const liveResult = parseToml(
      await hooks.tool.fork.execute(
        { agent: 'deep-reviewer', prompt: 'Review the same current tree.' },
        { sessionID: 'manager-reverted-root', agent: 'fast-manager' },
      ),
    )
    assert.equal(liveResult.error, undefined)
    assert.match(runtime.prompts[1].body.parts[0].text, /Requirement that survived compaction\./)

    for (const [resultToJoin, childSessionId] of [
      [result, createdIds[0]],
      [liveResult, createdIds[1]],
    ]) {
      runtime.recordFork('manager-reverted-root', resultToJoin.agent_id, childSessionId)
      const joined = hooks.tool.join.execute({}, { sessionID: 'manager-reverted-root', agent: 'fast-manager' })
      notifyCompleted(runtime, childSessionId, 'review completed', 'review completed', 5)
      await joined
    }
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

    // EXEC-004: the success wire is exactly status + agent + work_record. The
    // work record is the child's LWR text (here: the terminal text, since the
    // fixture delivers a plain text completion); runtime-only identities
    // (agent_id/run_id/child_session_id/...) never reach the LLM.
    assert.deepEqual(join, {
      status: 'completed',
      agent: 'fast-coder',
      work_record: 'forked coder session-wide A',
    })
    assert.ok(joinText.includes('forked coder session-wide A'))
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

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
import { roles, enforcer } from '../../unit/support/domain.mjs'
import {
  withPlugin,
  withExecutablePlugin,
  acceptAuthorityRoot,
  acceptChildAgentOwnerRoot,
  notifyCompleted,
  activateLife,
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
  'bash-honeypot': {},
  blog: {
    text: 'required',
    tip: 'required',
    evidence: 'optional',
  },
  coder: { tdd: 'required', prompt: 'optional', prompts: 'optional' },
  executor: {
    command: 'required',
    estimated_mem_usage: 'required',
    estimated_output_bytes: 'required',
    estimated_running_secs: 'required',
  },
  fork: { agent: 'required', prompt: 'optional', tdd: 'optional' },
  'fork-manager': { agent: 'required', prompt: 'required' },
  'fork-pty': { agent: 'required', prompt: 'optional', signal: 'optional' },
  inspector: { prompt: 'optional', prompts: 'optional' },
  join: {},
  list: {},
  mv: { source: 'required', destination: 'required' },
  rm: { path: 'required' },
  return: { message: 'required' },
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
    'fast-browser',
    'fast-coder',
    'fast-devops',
    'fast-inspector',
    'fast-meditator',
  ],
  'fork-manager': ['deep-manager', 'fast-manager'],
}

/**
 * The enum arm of an agent argument, whether or not it is wrapped in a union.
 *
 * `fork.agent` is `union([enum(...), string()])` while `fork-manager.agent` is a bare
 * enum (`ToolHostCodec.fs:90` vs `:78`). Measured consequence worth stating: the
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
  meditator: ['inspector'],
  reviewer: ['read', 'glob', 'grep', 'verdict'],
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

  // AGENT-008: Executor holds no tools; Blogger is also internal but
  // has its dedicated private tool surface.
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
    assert.deepEqual(omitted, { fork: false, 'fork-manager': false })
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
[…337ln elided…]
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


[Showing lines 1-450 and 788-1237 of 1237; 337 middle lines (15.0KB) elided. Read artifact://1311 for full output]
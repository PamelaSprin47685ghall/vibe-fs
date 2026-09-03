// Provider-visible bounded command execution. The owner surface keeps
// ToolHost arguments, runtime capabilities and recovery unions opaque.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  describeRun,
  run: executeRun,
  runToolName,
} = await import('../../../dist/OpenCode/Tools/ExecutorToolSurface.js')
const { contextDecode, contextView } = await import('../../../dist/OpenCode/Codec/ToolHostSurface.js')

const chain = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => chain(`${kind}-described`, extra),
  optional: () => chain(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => chain('string'),
  number: () => chain('number'),
  enum: (values) => chain('enum', { values }),
  boolean: () => chain('boolean'),
}
const toolModule = { tool: { schema: fakeSchema } }

const context = (sessionID = 'ses-exec') => ({ sessionID })
const run = (args, ctx = context(), recovery = '') =>
  executeRun(toolModule, {}, args, ctx, recovery)

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

test('WHAT[PROC-011] RUN_surface_names_the_provider_execution_verb', () => {
  assert.equal(runToolName, 'run')
  const tool = describeRun(toolModule)
  assert.equal(tool.name, 'run')
  assert.match(tool.description, /deadline_seconds (?:and|与) output_budget_bytes/)
  assert.deepEqual(tool.arguments, ['command', 'deadline_seconds', 'output_budget_bytes', 'world_lock'])
})

test('WHAT[PROC-011] RUN_host_context_codec_exposes_plain_snapshot', () => {
  const decoded = contextDecode({ sessionID: 'ses-exec', agent: 'devops' })
  assert.deepEqual(contextView(decoded), {
    sessionId: 'ses-exec',
    agent: 'devops',
    toolCallId: null,
    providerRunId: null,
    promptText: null,
  })
})

test('WHAT[PROC-011] RUN_missing_command_is_rejected_before_spawn', async () => {
  const result = await run({})
  assert.doesNotMatch(result, /\berror\s*=/)
  assert.match(result, /# (?:Missing command|缺少 command)/)
})

test('WHAT[PROC-011] RUN_blank_command_is_rejected_before_spawn', async () => {
  const result = await run({ command: '   ' })
  assert.doesNotMatch(result, /\berror\s*=/)
  assert.match(result, /# (?:Missing command|缺少 command)/)
})

test('WHAT[PROC-005] RUN_non_positive_deadline_is_rejected', async () => {
  for (const value of [0, -5, Number.NaN, Number.POSITIVE_INFINITY]) {
    const result = await run({ command: 'true', deadline_seconds: value })
    assert.match(result, /(?:deadline_seconds must be a finite positive number|deadline_seconds 必须是有限正数)/, `value=${value}`)
  }
})

test('WHAT[PROC-005] RUN_invalid_output_budget_is_rejected', async () => {
  const negative = await run({ command: 'true', output_budget_bytes: -1 })
  assert.match(negative, /(?:output_budget_bytes must be a finite non-negative integer|output_budget_bytes 必须是有限非负整数)/)

  const fractional = await run({ command: 'true', output_budget_bytes: 1.5 })
  assert.match(fractional, /(?:output_budget_bytes must be an integer|output_budget_bytes 必须是整数)/)

  const nan = await run({ command: 'true', output_budget_bytes: Number.NaN })
  assert.match(nan, /(?:output_budget_bytes must be a finite non-negative integer|output_budget_bytes 必须是有限非负整数)/)
})

test('WHAT[PROC-011] RUN_blank_session_surfaces_natural_execution_consequence_before_spawn', async () => {
  const result = await run({ command: 'true' }, context(''))
  assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
  assert.match(result, /(?:cannot run from this execution context|无法在此执行上下文中运行)/i)
})

test('WHAT[PROC-010] RUN_completed_command_reports_exit_code_and_streams', async () => {
  const result = parseToml(
    await run({ command: "printf 'hello-stdout'; printf 'hello-stderr' >&2" }),
  )
  assert.equal(result.exit_code, '0')
  assert.equal(result.stdout, 'hello-stdout')
  assert.equal(result.stderr, 'hello-stderr')
})

test('WHAT[PROC-010] RUN_nonzero_exit_is_reported_not_thrown', async () => {
  const result = parseToml(await run({ command: 'exit 3' }))
  assert.equal(result.exit_code, '3')
})

test('WHAT[PROC-011] RUN_deadline_overrun_returns_the_fixed_timeout_consequence', async () => {
  const result = await run({ command: 'sleep 5', deadline_seconds: 0.01, output_budget_bytes: 16 })
  assert.doesNotMatch(result, /TimeoutExceeded|\berror\s*=/)
  assert.match(result, /(?:The command was still running when its allowed time ended, so it was stopped\.|command 在允许时间结束时仍在运行，因此已被停止。)/)
})

test('WHAT[PROC-011] RUN_world_lock_is_accepted', async () => {
  const result = parseToml(await run({ command: "printf 'ok'", world_lock: true }))
  assert.equal(result.exit_code, '0')
  assert.equal(result.stdout, 'ok')
})

// ── Spooled path: output beyond output_budget_bytes spills to a spool file ───

const SPOOL_COMMAND = "printf 'abcdefghijklmnopqrstuvwxyz0123456789'"
const SPOOL_BUDGET = { command: SPOOL_COMMAND, output_budget_bytes: 4 }

test('WHAT[PROC-011] RUN_spooled_request_without_authority_fails_before_execution_without_identity_leak', async () => {
  const result = await run(SPOOL_BUDGET, context(''))
  assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
  assert.match(result, /(?:cannot run from this execution context|无法在此执行上下文中运行)/i)
})

test('WHAT[PROC-011] RUN_spooled_output_family_blocked_surfaces_recovery_consequence', async () => {
  const result = await run(SPOOL_BUDGET, context(), 'blocked')
  assert.doesNotMatch(result, /RECOVERY_BLOCKED|\berror\s*=/)
  assert.match(result, /(?:large output cannot be reconciled while recovery is blocked|恢复受阻期间无法调和其大额输出)/i)
})

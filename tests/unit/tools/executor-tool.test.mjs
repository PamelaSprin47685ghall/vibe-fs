// tests/unit/tools/executor-tool.test.mjs — VERIFY-009 coverage: executor tool.
//
// Decode errors are refused before any spawn. The Completed path runs a real
// `sh -lc` through the real ProcessRunner (process-runner.test.mjs precedent).
// The Spooled path (>3x estimated bytes) drives the permit gate with an
// attached family-recovery stub; the map/reduce itself runs the real
// ExecutorSummarize against the scope's real executor runtime (no journal →
// chunk fork fails fast → partial summary, no hang).

import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import test from 'node:test'

import { listItems, sessionId } from '../support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/ExecutorTool.js')
const {
  ToolRuntimeScope,
  ToolRuntimeScope__AttachFamilyRecovery_3A336721: attachFamilyRecovery,
} = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const {
  FamilyRecovery,
  NonEmpty_one: nonEmptyOne,
  RecoveryBlock,
} = await import('../../../dist/Domain/SessionRecovery.js')

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
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const context = (session = 'ses-exec') =>
  new HostToolContext(session, undefined, undefined, undefined, undefined, () => () => {})

const scope = ({ sessions } = {}) =>
  new ToolRuntimeScope(
    sessions ?? {},
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

const run = (runtimeScope, args, ctx = context()) => spec(factory, runtimeScope).Execute(makeArgs(args), ctx)

test('EXECUTOR_spec_exposes_command_and_estimate_arguments', () => {
  const tool = spec(factory, scope())
  assert.equal(tool.Name, 'executor')
  assert.match(tool.Description, /explicit output, time, and memory estimates/)
  const args = listItems(tool.Arguments).map(([name]) => name)
  assert.deepEqual(args, ['command', 'estimated_output_bytes', 'estimated_running_secs', 'estimated_mem_usage'])
})

test('EXECUTOR_missing_command_is_rejected_before_spawn', async () => {
  const result = parseToml(await run(scope(), {}))
  assert.equal(result.error, 'Missing command')
})

test('EXECUTOR_blank_command_is_rejected_before_spawn', async () => {
  const result = parseToml(await run(scope(), { command: '   ' }))
  assert.equal(result.error, 'Missing command')
})

test('EXECUTOR_non_positive_running_secs_is_rejected', async () => {
  for (const value of [0, -5, Number.NaN, Number.POSITIVE_INFINITY]) {
    const result = parseToml(await run(scope(), { command: 'true', estimated_running_secs: value }))
    assert.equal(result.error, 'estimated_running_secs must be a finite positive number', `value=${value}`)
  }
})

test('EXECUTOR_invalid_output_bytes_is_rejected', async () => {
  const negative = parseToml(await run(scope(), { command: 'true', estimated_output_bytes: -1 }))
  assert.equal(negative.error, 'estimated_output_bytes must be a finite non-negative integer')

  const fractional = parseToml(await run(scope(), { command: 'true', estimated_output_bytes: 1.5 }))
  assert.equal(fractional.error, 'estimated_output_bytes must be an integer')

  const nan = parseToml(await run(scope(), { command: 'true', estimated_output_bytes: Number.NaN }))
  assert.equal(nan.error, 'estimated_output_bytes must be a finite non-negative integer')
})

test('EXECUTOR_unknown_memory_tier_is_rejected', async () => {
  const result = parseToml(await run(scope(), { command: 'true', estimated_mem_usage: 'huge' }))
  assert.equal(result.error, 'estimated_mem_usage must be medium or large')
})

test('EXECUTOR_blank_session_surfaces_runtime_error_before_spawn', async () => {
  const result = parseToml(await run(scope(), { command: 'true' }, context('')))
  assert.equal(result.error, 'Missing sessionID')
})

test('EXECUTOR_completed_command_reports_exit_code_and_streams', async () => {
  const result = parseToml(
    await run(scope(), { command: "printf 'hello-stdout'; printf 'hello-stderr' >&2" }),
  )
  assert.equal(result.exit_code, '0')
  assert.equal(result.stdout, 'hello-stdout')
  assert.equal(result.stderr, 'hello-stderr')
})

test('EXECUTOR_nonzero_exit_is_reported_not_thrown', async () => {
  const result = parseToml(await run(scope(), { command: 'exit 3' }))
  assert.equal(result.exit_code, '3')
})

test('EXECUTOR_deadline_overrun_surfaces_process_error', async () => {
  // 3x the 10ms estimate is the sole deadline — `sleep 5` must be killed.
  const result = parseToml(
    await run(scope(), { command: 'sleep 5', estimated_running_secs: 0.01, estimated_output_bytes: 16 }),
  )
  assert.ok(result.error, `a killed process must surface an error, got: ${JSON.stringify(result)}`)
  assert.match(result.error, /timed out|deadline|TimedOut|exceeded/i)
})

test('EXECUTOR_large_memory_estimate_is_accepted', async () => {
  const result = parseToml(await run(scope(), { command: "printf 'ok'", estimated_mem_usage: 'large' }))
  assert.equal(result.exit_code, '0')
  assert.equal(result.stdout, 'ok')
})

// ── Spooled path: output beyond 3x the estimate spills to a spool file ──────

const SPOOL_COMMAND = "printf 'abcdefghijklmnopqrstuvwxyz0123456789'"
const SPOOL_ESTIMATE = { command: SPOOL_COMMAND, estimated_output_bytes: 4 }

test('EXECUTOR_spooled_output_without_session_fails_closed', async () => {
  const result = parseToml(await run(scope(), SPOOL_ESTIMATE, context('')))
  assert.equal(result.error, 'Missing sessionID')
})

test('EXECUTOR_spooled_output_family_blocked_surfaces_recovery_error', async () => {
  const runtimeScope = scope()
  attachFamilyRecovery(
    runtimeScope,
    async () => new FamilyRecovery(2, [nonEmptyOne(new RecoveryBlock(6, [sessionId('ses-exec')]))]),
  )

  const result = parseToml(await run(runtimeScope, SPOOL_ESTIMATE))
  assert.equal(result.error, 'RECOVERY_BLOCKED: family recovery blocked before executor join')
})

test('EXECUTOR_spooled_output_runs_map_reduce_and_always_deletes_the_spool', async () => {
  const runtimeScope = scope()
  // First permit request (the tool's own gate) waits; every later request from
  // the map/reduce runtime hard-blocks so chunk forks/awaits fail fast into a
  // partial summary instead of retrying until the await budget expires.
  let calls = 0
  attachFamilyRecovery(runtimeScope, async () => {
    calls += 1
    return calls === 1
      ? new FamilyRecovery(1, [nonEmptyOne(new RecoveryBlock(6, [sessionId('ses-exec')]))])
      : new FamilyRecovery(2, [nonEmptyOne(new RecoveryBlock(6, [sessionId('ses-exec')]))])
  })

  const text = await run(runtimeScope, SPOOL_ESTIMATE)
  const result = parseToml(text)

  assert.equal(result.exit_code, '0')
  assert.equal(result.total_bytes, '36')
  assert.equal(result.chunk_count, '1')
  assert.ok(result.spool_path, 'spool path must be reported')
  assert.equal(existsSync(result.spool_path), false, 'the spool file must be deleted even on the summary path')
  // No journal → the executor chunk fork fails fast; the summary degrades to a
  // partial report carried as instructions, never a thrown exception.
  assert.match(text, /partial|raw tail|unavailable/i)
})

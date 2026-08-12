// tests/unit/tools/executor-tool.test.mjs — VERIFY-009 coverage: run tool.
//
// Decode errors are refused before any spawn. The Completed path runs a real
// `sh -lc` through the real ProcessRunner (process-runner.test.mjs precedent).
// The Spooled path (output beyond output_budget_bytes) drives the permit gate with
// an attached family-recovery stub; map/reduce runs the real Distillation against
// the scope's executor runtime (no journal → chunk fork fails fast → partial account).

import assert from 'node:assert/strict'
import test from 'node:test'

import { listItems, sessionId } from '../support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { runSpec } = await import('../../../dist/Infrastructure/OpenCode/Tools/ExecutorTool.js')
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
  boolean: () => chain('boolean'),
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

const run = (runtimeScope, args, ctx = context()) => runSpec(factory, runtimeScope).Execute(makeArgs(args), ctx)

test('RUN_spec_exposes_command_and_budget_arguments', () => {
  const tool = runSpec(factory, scope())
  assert.equal(tool.Name, 'run')
  assert.match(tool.Description, /deadline_seconds and output_budget_bytes/)
  const args = listItems(tool.Arguments).map(([name]) => name)
  assert.deepEqual(args, ['command', 'deadline_seconds', 'output_budget_bytes', 'world_lock'])
})

test('RUN_missing_command_is_rejected_before_spawn', async () => {
  const result = parseToml(await run(scope(), {}))
  assert.equal(result.error, 'Missing command')
})

test('RUN_blank_command_is_rejected_before_spawn', async () => {
  const result = parseToml(await run(scope(), { command: '   ' }))
  assert.equal(result.error, 'Missing command')
})

test('RUN_non_positive_deadline_is_rejected', async () => {
  for (const value of [0, -5, Number.NaN, Number.POSITIVE_INFINITY]) {
    const result = parseToml(await run(scope(), { command: 'true', deadline_seconds: value }))
    assert.equal(result.error, 'deadline_seconds must be a finite positive number', `value=${value}`)
  }
})

test('RUN_invalid_output_budget_is_rejected', async () => {
  const negative = parseToml(await run(scope(), { command: 'true', output_budget_bytes: -1 }))
  assert.equal(negative.error, 'output_budget_bytes must be a finite non-negative integer')

  const fractional = parseToml(await run(scope(), { command: 'true', output_budget_bytes: 1.5 }))
  assert.equal(fractional.error, 'output_budget_bytes must be an integer')

  const nan = parseToml(await run(scope(), { command: 'true', output_budget_bytes: Number.NaN }))
  assert.equal(nan.error, 'output_budget_bytes must be a finite non-negative integer')
})

test('RUN_blank_session_surfaces_runtime_error_before_spawn', async () => {
  const result = parseToml(await run(scope(), { command: 'true' }, context('')))
  assert.equal(result.error, 'Missing sessionID')
})

test('RUN_completed_command_reports_exit_code_and_streams', async () => {
  const result = parseToml(
    await run(scope(), { command: "printf 'hello-stdout'; printf 'hello-stderr' >&2" }),
  )
  assert.equal(result.exit_code, '0')
  assert.equal(result.stdout, 'hello-stdout')
  assert.equal(result.stderr, 'hello-stderr')
})

test('RUN_nonzero_exit_is_reported_not_thrown', async () => {
  const result = parseToml(await run(scope(), { command: 'exit 3' }))
  assert.equal(result.exit_code, '3')
})

test('RUN_deadline_overrun_surfaces_process_error', async () => {
  const result = parseToml(
    await run(scope(), { command: 'sleep 5', deadline_seconds: 0.01, output_budget_bytes: 16 }),
  )
  assert.ok(result.error, `a killed process must surface an error, got: ${JSON.stringify(result)}`)
  assert.match(result.error, /timed out|deadline|TimedOut|exceeded/i)
})

test('RUN_world_lock_is_accepted', async () => {
  const result = parseToml(await run(scope(), { command: "printf 'ok'", world_lock: true }))
  assert.equal(result.exit_code, '0')
  assert.equal(result.stdout, 'ok')
})

// ── Spooled path: output beyond output_budget_bytes spills to a spool file ───

const SPOOL_COMMAND = "printf 'abcdefghijklmnopqrstuvwxyz0123456789'"
const SPOOL_BUDGET = { command: SPOOL_COMMAND, output_budget_bytes: 4 }

test('RUN_spooled_output_without_session_fails_closed', async () => {
  const result = parseToml(await run(scope(), SPOOL_BUDGET, context('')))
  assert.equal(result.error, 'Missing sessionID')
})

test('RUN_spooled_output_family_blocked_surfaces_recovery_error', async () => {
  const runtimeScope = scope()
  attachFamilyRecovery(
    runtimeScope,
    async () => new FamilyRecovery(2, [nonEmptyOne(new RecoveryBlock(6, [sessionId('ses-exec')]))]),
  )

  const result = parseToml(await run(runtimeScope, SPOOL_BUDGET))
  assert.equal(result.error, 'RECOVERY_BLOCKED: family recovery blocked before run join')
})

test('RUN_spooled_output_runs_distillation_without_chunk_statistics', async () => {
  const runtimeScope = scope()
  // First permit request (the tool's own gate) waits; every later request from
  // the map/reduce runtime hard-blocks so chunk forks/awaits fail fast into a
  // partial account instead of retrying until the await budget expires.
  let calls = 0
  attachFamilyRecovery(runtimeScope, async () => {
    calls += 1
    return calls === 1
      ? new FamilyRecovery(1, [nonEmptyOne(new RecoveryBlock(6, [sessionId('ses-exec')]))])
      : new FamilyRecovery(2, [nonEmptyOne(new RecoveryBlock(6, [sessionId('ses-exec')]))])
  })

  const text = await run(runtimeScope, SPOOL_BUDGET)
  const result = parseToml(text)

  assert.equal(result.exit_code, '0')
  assert.equal(result.chunk_count, undefined, 'provider must not expose chunk_count')
  assert.equal(result.total_bytes, undefined, 'provider must not expose total_bytes')
  assert.equal(result.spool_path, undefined, 'provider must not expose spool_path')
  // No journal → the distiller chunk fork fails fast; the account degrades to a
  // partial report carried as instructions, never a thrown exception.
  assert.match(text, /Condensation|Most recent raw output/i)
})

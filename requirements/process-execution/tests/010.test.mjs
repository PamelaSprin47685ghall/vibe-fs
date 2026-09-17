import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

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
const SPOOL_COMMAND = "printf 'abcdefghijklmnopqrstuvwxyz0123456789'"
const SPOOL_BUDGET = { command: SPOOL_COMMAND, output_budget_bytes: 4 }

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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const { renderPtyCompletion } = await import('../../../dist/Process/Surface.js')

test('WHAT[PROC-010] EXEC_004_pty_completion_is_natural_language_plus_exit_code', () => {
  const wire = renderPtyCompletion('shell', 'pty-9', 'ended', 0)
  assert.match(wire, /# shell has ended\./)
  assert.match(wire, /exit_code = 0/)
  assert.ok(!wire.includes('pty_id'))
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const {
  command,
  context,
  createCancellationToken,
  estimate,
  runWithLauncher,
  runWithHostLauncher,
} = await import('../../../dist/Process/Surface.js')
const CTX = context(undefined, 3_600_000)
const cmd = command('sh', ['-c', 'echo hi'], undefined, undefined)
const makeEstimate = (runtimeSeconds = 10, outputBytes = 1024, memory = 'medium') =>
  estimate(runtimeSeconds, outputBytes, memory)
const live = () => createCancellationToken(false)
const cancelled = () => createCancellationToken(true)
const okLauncher = (exitCode = 0, out = 'hello', err = '') => async (_command, _token) => [
  exitCode,
  new TextEncoder().encode(out),
  new TextEncoder().encode(err),
]

test('WHAT[PROC-010] EXEC_011_successful_run_collects_stdout_and_exit_code', async () => {
  const result = await runWithLauncher(okLauncher(0, 'the output', ''), cmd, makeEstimate(), CTX, live())
  assert.equal(result.ok, true)
  assert.deepEqual(result.value, {
    kind: 'Completed',
    exitCode: 0,
    stdout: 'the output',
    stderr: '',
    spooled: false,
  })
})
test('WHAT[PROC-010] EXEC_011_nonzero_exit_is_still_an_ok_outcome', async () => {
  const result = await runWithLauncher(okLauncher(3, '', 'boom'), cmd, makeEstimate(), CTX, live())
  assert.equal(result.ok, true)
  assert.equal(result.value.kind, 'Completed')
  assert.equal(result.value.exitCode, 3)
})
}

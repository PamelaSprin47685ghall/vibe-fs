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
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { default: test } = await import("node:test");
const { fileURLToPath } = await import("node:url");

const {
  command,
  commandView,
  estimate,
  estimateView,
} = await import('../../../dist/Process/Surface.js')

test('WHAT[PROC-005] EXEC_010_process_request_carries_all_fields', () => {
  const cmd = command('sh', ['-lc', 'echo hi'], '/tmp/wx', 'input')
  const cmdView = commandView(cmd)
  const estView = estimateView(estimate(42, 65536, 'large'))

  assert.equal(cmdView.fileName, 'sh')
  assert.deepEqual(cmdView.arguments, ['-lc', 'echo hi'])
  assert.equal(cmdView.workingDirectory, '/tmp/wx')
  assert.equal(cmdView.stdin, 'input')
  assert.equal(estView.runtimeSeconds, 42)
  assert.equal(estView.outputBytes, 65536)
  assert.equal(estView.memory, 'large')
})
}

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  describeRun,
  run: executeRun,
  runToolName,
} = await import('../../../dist/OpenCode/Tools/ExecutorToolSurface.js')

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

test('WHAT[process-execution-017] RUN_surface_names_the_provider_execution_verb', () => {
  assert.equal(runToolName, 'run')
  const tool = describeRun(toolModule)
  assert.equal(tool.name, 'run')
})

test('WHAT[process-execution-017] RUN_deadline_over_limit_is_rejected_on_the_instruction_plane', async () => {
  for (const deadlineSeconds of [3600, 10.5]) {
    const result = await run({ command: 'true', deadline_seconds: deadlineSeconds })
    assert.doesNotMatch(result, /\berror\s*=/, `deadline_seconds ${deadlineSeconds} must not surface an error field`)
    assert.match(result, /^# /m, `deadline_seconds ${deadlineSeconds} rejection must be an instruction-plane comment line`)
    assert.match(result, /\b10\b/, `deadline_seconds ${deadlineSeconds} rejection must state the 10-second upper bound`)
    assert.match(result, /open-terminal/, `deadline_seconds ${deadlineSeconds} rejection must point to the open-terminal channel`)
  }
})

{
  const { run, nodeCommand } = await import('./support/executor.mjs')
  const { mkdtempSync, existsSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')

  test('WHAT[process-execution-017] RUN_deadline_over_limit_is_rejected_before_spawn', async () => {
    const dir = mkdtempSync(join(tmpdir(), 'wxs-deadline-'))
    const marker = join(dir, 'spawned')
    const command = nodeCommand(`require('node:fs').writeFileSync(${JSON.stringify(marker)}, 'ran')`)
    try {
      for (const deadlineSeconds of [3600, 10.5]) {
        const result = await run({ command, deadline_seconds: deadlineSeconds })
        assert.doesNotMatch(result, /\berror\s*=/)
        assert.match(result, /^# /m)
        assert.equal(existsSync(marker), false, `deadline_seconds ${deadlineSeconds} must be rejected before the process is spawned`)
      }
    } finally {
      rmSync(dir, { recursive: true, force: true })
    }
  })

  test('WHAT[process-execution-017] RUN_deadline_exactly_at_ten_seconds_still_runs', async () => {
    const result = parseToml(await run({ command: "printf 'ok'", deadline_seconds: 10 }))
    assert.equal(result.exit_code, '0')
    assert.equal(result.stdout, 'ok')
  })

  test('WHAT[process-execution-017] RUN_deadline_default_completes_fast_commands', async () => {
    const result = parseToml(await run({ command: "printf 'ok'" }))
    assert.equal(result.exit_code, '0')
    assert.equal(result.stdout, 'ok')
  })
}

{
  const { existsSync } = await import('node:fs')
  const { join, dirname } = await import('node:path')
  const { fileURLToPath } = await import('node:url')

  const resourceRoot = join(dirname(fileURLToPath(import.meta.url)), '../../../resources/provider/tool/run/deadline-over-limit')

  test('WHAT[process-execution-017] RUN_deadline_over_limit_resources_exist_in_both_languages', () => {
    for (const locale of ['en.md', 'zh-CN.md']) {
      assert.equal(existsSync(join(resourceRoot, locale)), true, `deadline-over-limit resource ${locale} must exist`)
    }
  })
}

// Provider-visible spooled output through the execution owner surface.
process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

import assert from 'node:assert/strict'
import test from 'node:test'

const { run: executeRun } = await import('../../../dist/OpenCode/Tools/ExecutorToolSurface.js')

const chain = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => chain(`${kind}-described`, extra),
  optional: () => chain(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => chain('string'),
  number: () => chain('number'),
  boolean: () => chain('boolean'),
}
const toolModule = { tool: { schema: fakeSchema } }

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

const run = (args, context = { sessionID: 'ses-exec' }) => executeRun(toolModule, {}, args, context, 'ready')
const SPOOL_COMMAND = "printf 'abcdefghijklmnopqrstuvwxyz0123456789'"
const SPOOL_BUDGET = { command: SPOOL_COMMAND, output_budget_bytes: 4 }

test('WHAT[DISTILL-013] RUN_spooled_output_runs_distillation_without_chunk_statistics', async () => {
  const text = await run(SPOOL_BUDGET)
  const result = parseToml(text)

  assert.equal(result.exit_code, '0')
  assert.equal(result.chunk_count, undefined, 'provider must not expose chunk_count')
  assert.equal(result.total_bytes, undefined, 'provider must not expose total_bytes')
  assert.equal(result.spool_path, undefined, 'provider must not expose spool_path')
  // No journal → the one bounded-tail Distiller fails closed; the account degrades to
  // a partial report carried as instructions, never a thrown exception.
  assert.match(text, /Condensation|Most recent raw output/i)
})

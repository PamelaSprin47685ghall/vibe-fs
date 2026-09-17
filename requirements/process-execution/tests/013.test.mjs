import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseDocument } from 'smol-toml'
import { bindOnce } from '../../../dist/Participant/Provider/LanguageSurface.js'

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

const { run: executeRun, formatSpooledOutcome } = await import('../../../dist/OpenCode/Tools/ExecutorToolSurface.js')

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

const parseToml = parseDocument

const run = (args, context = { sessionID: 'ses-exec' }) => executeRun(toolModule, {}, args, context, 'ready')

test('WHAT[PROC-013] large_command_output_produces_raw_tail_with_zero_distiller_sessions', async () => {
  // 生成远超预算的大输出，末尾包含明确唯一的错误标记
  const earlyMarker = 'EARLY_NON_TAIL_OUTPUT_9918ab'
  const tailMarker = 'CRITICAL_TAIL_ERROR_7f3a91'
  const command = `python3 -c "print('${earlyMarker}'); print('x' * 500); print('${tailMarker}')"`

  const result = await run({ command, output_budget_bytes: 256 })
  const parsed = parseToml(result)

  assert.equal(parsed.exit_code, 0)
  // PROC-013: 零 Distiller 模型会话，输出直接保留未修改的原始尾部，不出现模型摘要字样
  assert.ok(result.includes(tailMarker), 'the raw tail marker must be preserved in output')
  assert.ok(!result.includes(earlyMarker), 'earlier output beyond budget must be truncated')
  assert.ok(!result.includes('Condensation failed'), 'zero Distiller failure artifacts')
  assert.ok(!result.includes('Distilled output'), 'must not be model summary')
})

test('WHAT[PROC-013] small_command_output_within_budget_is_returned_raw_unmodified', async () => {
  const command = "printf 'hello standard output'"
  const result = await run({ command, output_budget_bytes: 1024 })
  const parsed = parseToml(result)

  assert.equal(parsed.exit_code, 0)
  assert.equal(parsed.stdout, 'hello standard output')
  assert.equal(parsed.stderr, '')
})

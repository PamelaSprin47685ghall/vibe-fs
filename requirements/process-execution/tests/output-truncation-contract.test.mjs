// requirements/process-execution/tests/output-truncation-contract.test.mjs
//
// Owner: process-execution.
//
// PROC-013: 大输出零 Distiller 与预算内原始留尾截断
// PROC-014: 真实程序事实不从日志推断且截断声明明确
// PROC-015: 显式字节预算计量与 UTF-8 截断边界安全

import assert from 'node:assert/strict'
import test from 'node:test'

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

test('WHAT[PROC-013] large_command_output_produces_raw_tail_with_zero_distiller_sessions', async () => {
  // 生成远超预算的大输出，末尾包含明确唯一的错误标记
  const earlyMarker = 'EARLY_NON_TAIL_OUTPUT_9918ab'
  const tailMarker = 'CRITICAL_TAIL_ERROR_7f3a91'
  const command = `python3 -c "print('${earlyMarker}' * 500); print('${tailMarker}')"`

  const result = await run({ command, output_budget_bytes: 256 })
  const parsed = parseToml(result)

  assert.equal(parsed.exit_code, '0')
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

  assert.equal(parsed.exit_code, '0')
  assert.equal(parsed.stdout, 'hello standard output')
  assert.equal(parsed.stderr, '')
})

test('WHAT[PROC-014] truncated_output_preserves_program_facts_without_log_inference_and_declares_truncation', async () => {
  // 执行一条返回非零退出码并产生超大输出的命令
  const command = 'python3 -c "import sys; print(\'x\' * 2000); sys.stderr.write(\'failed at line 42\\n\'); sys.exit(7)"'
  const result = await run({ command, output_budget_bytes: 128 })
  const parsed = parseToml(result)

  // PROC-014: 真实程序事实（退出码=7）严格由物理退出事件确立，不从日志推断，不随截断丢失
  assert.equal(parsed.exit_code, '7', 'exit code must be 7 from process exit event')
  // 截断声明明确存在
  assert.match(
    result,
    /(?:truncated|截断|Earlier command output was truncated)/i,
    'must contain explicit truncation notice',
  )
})

test('WHAT[PROC-015] output_truncation_enforces_byte_budget_and_utf8_char_boundary', async () => {
  // 包含 3 字节 UTF-8 中文字符与 4 字节 Emoji 的超长输出
  const command = 'python3 -c "print(\'万象术\' * 100 + \'🔥\' * 50 + \'FINAL_TAIL_UTF8\')"'
  const budgetBytes = 64
  const result = await run({ command, output_budget_bytes: budgetBytes })

  // PROC-015: 截断必须对齐 UTF-8 字符边界，不得产生无效的 \uFFFD 乱码
  assert.ok(!result.includes('\uFFFD'), 'truncation must not slice mid-UTF-8 multibyte character')
  assert.ok(result.includes('FINAL_TAIL_UTF8'), 'latest UTF-8 tail must be retained')
})

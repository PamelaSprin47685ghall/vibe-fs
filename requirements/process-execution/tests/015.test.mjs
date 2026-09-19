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

test('WHAT[process-execution-015] output_truncation_enforces_byte_budget_and_utf8_char_boundary', async () => {
  // 包含 3 字节 UTF-8 中文字符与 4 字节 Emoji 的超长输出
  const command = 'python3 -c "print(\'万象术\' * 100 + \'🔥\' * 50 + \'FINAL_TAIL_UTF8\')"'
  const budgetBytes = 64
  const result = await run({ command, output_budget_bytes: budgetBytes })

  // process-execution-015: 截断必须对齐 UTF-8 字符边界，不得产生无效的 \uFFFD 乱码
  assert.ok(!result.includes('\uFFFD'), 'truncation must not slice mid-UTF-8 multibyte character')
  assert.ok(result.includes('FINAL_TAIL_UTF8'), 'latest UTF-8 tail must be retained')
})

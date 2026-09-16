import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const { distillSpool } = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')
const { formatSpooledOutcome } = await import('../../../dist/OpenCode/Tools/ExecutorToolSurface.js')
const SPOOL_CHUNK_BYTES = 204_800

test('WHAT[DISTILL-005] unseen_reader_gets_locator_plus_visible_truncation_boundary', async () => {
  const earlyMarker = 'EARLY_CONTEXT_NOT_OBSERVED_91aa'
  const tailMarker = 'LATEST_PTY_CRASH_7f3a'
  const dir = mkdtempSync(join(tmpdir(), 'wxs-distill-005-'))
  const spoolPath = join(dir, 'spool.bin')
  const first = Buffer.alloc(SPOOL_CHUNK_BYTES, 0x61)
  first.write(earlyMarker, 0, 'utf8')
  const last = Buffer.alloc(256, 0x62)
  last.write(tailMarker, 0, 'utf8')
  writeFileSync(spoolPath, Buffer.concat([first, last]))

  let payload = null
  const runtime = {
    fork: (agentId, _prompt, body) => {
      payload = body
      return { ok: true, agentId }
    },
    awaitAgent: (agentId) => ({
      ok: true,
      runId: `run-${agentId}`,
      workRecord: payload.includes(tailMarker) ? `Observed exact failure marker ${tailMarker}` : 'missing',
    }),
    awaitRecoveryReadiness: () => undefined,
    cancel: () => undefined,
  }

  try {
    const summary = await distillSpool(runtime, spoolPath, 'en')
    assert.ok(summary.includes(tailMarker))
    assert.ok(!summary.includes(earlyMarker))
    assert.match(summary, /truncated before distillation/)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DISTILL-005] EXEC_distill_spool_extracts_llm_output_without_thinking_and_without_recent_work_header', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-distill-005-extract-'))
  const spoolPath = join(dir, 'spool.bin')
  writeFileSync(spoolPath, Buffer.from('small tail'))

  const rawWorkRecord = [
    'Recent work',
    '<think>',
    'internal chain of thought to be discarded',
    '</think>',
    'assistant: ### 提炼证据',
    '',
    '* 修改文件: a.txt',
    '',
    '### 收尾报告',
    '完成。',
  ].join('\n')

  const runtime = {
    fork: (agentId) => ({ ok: true, agentId }),
    awaitAgent: (agentId) => ({ ok: true, runId: `run-${agentId}`, workRecord: rawWorkRecord }),
    awaitRecoveryReadiness: () => undefined,
    cancel: () => undefined,
  }

  try {
    const summary = await distillSpool(runtime, spoolPath, 'zh-CN')
    assert.equal(summary.includes('Recent work'), false)
    assert.equal(summary.includes('internal chain of thought'), false)
    assert.equal(summary.includes('assistant:'), false)
    assert.match(summary, /### 提炼证据/)
    assert.match(summary, /### 收尾报告/)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DISTILL-005] RUN_spooled_outcome_formats_summary_as_toml_comment', () => {
  const summary = '输出内容太长，以下是蒸馏后的内容\n\n### 提炼证据\n\n* 修改文件: a.txt'
  const text = formatSpooledOutcome(0, summary)
  assert.ok(text.startsWith('# 输出内容太长，以下是蒸馏后的内容\n#\n# ### 提炼证据'))
  assert.match(text, /exit_code = 0/)
  assert.equal(text.includes('Recent work'), false)
  assert.equal(text.includes('assistant:'), false)
})

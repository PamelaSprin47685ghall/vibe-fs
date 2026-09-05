// requirements/output-distillation/tests/executor-summarize.test.mjs
// Owner: output-distillation. Oversized spool input is a bounded tail consumed by
// exactly one Distiller; no map/reduce fan-out exists.

import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const {
  distillFragmentPrompt,
  distillSpool,
} = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')

const SPOOL_CHUNK_BYTES = 204_800

function writeSpool(chunks) {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-sum-tail-'))
  const spoolPath = join(dir, 'spool.bin')
  writeFileSync(spoolPath, Buffer.concat(chunks))
  return { dir, spoolPath }
}

function fakeRuntime({ fork, awaitAgent, awaitRecoveryReadiness, cancel } = {}) {
  const forked = []
  const awaitCalls = []
  const cancelled = []
  const payloads = new Map()
  const runtime = {
    fork: (agentId, prompt, payload) => {
      forked.push(agentId)
      payloads.set(agentId, payload)
      return typeof fork === 'function' ? fork(agentId, prompt, payload) : { ok: true, agentId }
    },
    awaitAgent: (agentId, timeoutMs) => {
      awaitCalls.push({ agentId, timeoutMs })
      return typeof awaitAgent === 'function'
        ? awaitAgent(agentId, timeoutMs, payloads.get(agentId))
        : { ok: true, runId: `run-${agentId}`, workRecord: `summary-for-${agentId}` }
    },
    awaitRecoveryReadiness: (agentId) =>
      typeof awaitRecoveryReadiness === 'function' ? awaitRecoveryReadiness(agentId) : undefined,
    cancel: (agentId) => {
      cancelled.push(agentId)
      if (typeof cancel === 'function') cancel(agentId)
    },
  }
  return { runtime, forked, awaitCalls, cancelled, payloads }
}

const completed = (agentId, workRecord = `summary-for-${agentId}`) => ({
  ok: true,
  runId: `run-${agentId}`,
  workRecord,
})

test('WHAT[DISTILL-001] DISTILLATION_prompt_declares_bounded_tail_and_forbids_whole_run_inference', () => {
  assert.equal(
    distillFragmentPrompt('en'),
    'Distill this bounded tail of command output. Earlier output may be absent. Preserve errors, decisions, paths, and exact numbers; never infer whole-run success from this tail; omit raw code.',
  )
})

test('WHAT[DISTILL-004] EXEC_distill_spool_never_fans_out_or_reduces_when_spool_grows', async () => {
  const chunks = Array.from({ length: 12 }, (_, index) => Buffer.alloc(SPOOL_CHUNK_BYTES, 0x61 + (index % 20)))
  const { dir, spoolPath } = writeSpool(chunks)
  const { runtime, forked, awaitCalls } = fakeRuntime()

  try {
    const summary = await distillSpool(runtime, spoolPath, 'en')
    assert.ok(summary.length > 0)
    assert.equal(forked.length, 1, 'spool size must not create map/reduce Distiller fan-out')
    assert.equal(awaitCalls.length, 1, 'the single Distiller is the only awaited child')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DISTILL-007] EXEC_distill_spool_keeps_only_latest_200kib_payload', async () => {
  const earlyMarker = 'EARLY_BYTES_MUST_NOT_REACH_DISTILLER'
  const retainedSuffixMarker = 'PREVIOUS_CHUNK_SUFFIX_MUST_SURVIVE_28bc'
  const tailMarker = 'LATEST_FAILURE_MARKER_7f3a'
  const first = Buffer.alloc(SPOOL_CHUNK_BYTES, 0x61)
  first.write(earlyMarker, 0, 'utf8')
  first.write(retainedSuffixMarker, SPOOL_CHUNK_BYTES - 96, 'utf8')
  const last = Buffer.alloc(128, 0x62)
  last.write(tailMarker, 0, 'utf8')
  const { dir, spoolPath } = writeSpool([first, last])
  const { runtime, forked, payloads } = fakeRuntime()

  try {
    await distillSpool(runtime, spoolPath, 'en')
    assert.equal(forked.length, 1)
    const payload = payloads.get(forked[0])
    assert.equal(typeof payload, 'string')
    assert.equal(Buffer.byteLength(payload, 'utf8'), SPOOL_CHUNK_BYTES)
    assert.ok(payload.includes(retainedSuffixMarker), 'the rolling tail keeps the suffix of the previous stream chunk')
    assert.ok(payload.includes(tailMarker), 'latest bytes are preserved')
    assert.ok(!payload.includes(earlyMarker), 'earlier windows are discarded before distillation')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DISTILL-008] EXEC_distill_spool_waiting_rechecks_same_exact_agent_after_readiness', async () => {
  const { dir, spoolPath } = writeSpool([Buffer.from('tail')])
  const order = []
  let attempts = 0
  const { runtime, forked } = fakeRuntime({
    awaitAgent: (agentId) => {
      order.push(`permit:${agentId}`)
      attempts += 1
      return attempts === 1 ? { ok: false, kind: 'waiting' } : completed(agentId)
    },
    awaitRecoveryReadiness: (agentId) => order.push(`readiness:${agentId}`),
  })

  try {
    const summary = await distillSpool(runtime, spoolPath, 'en')
    const id = forked[0]
    assert.ok(summary.includes('summary-for-'))
    assert.deepEqual(order, [`permit:${id}`, `readiness:${id}`, `permit:${id}`])
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DISTILL-006] EXEC_distill_spool_failure_returns_bounded_raw_tail_and_cancels_once', async () => {
  const marker = 'LATEST_RAW_FAILURE_4d1c'
  const first = Buffer.alloc(SPOOL_CHUNK_BYTES, 0x61)
  const last = Buffer.from(marker)
  const { dir, spoolPath } = writeSpool([first, last])
  const { runtime, forked, cancelled } = fakeRuntime({
    awaitAgent: (agentId) => ({ ok: false, kind: 'not-found', error: `blocked:${agentId}` }),
  })

  try {
    const summary = await distillSpool(runtime, spoolPath, 'en')
    assert.match(summary, /Condensation failed:/)
    assert.match(summary, /Earlier command output was truncated before distillation/)
    assert.ok(summary.includes(marker), 'failure exposes the same bounded raw tail')
    assert.equal(forked.length, 1)
    assert.deepEqual(cancelled, [forked[0]], 'owned Distiller is physically cancelled at most once')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DISTILL-013] DISTILLATION_prompt_has_no_chunk_or_reduce_instrumentation', () => {
  const prompt = distillFragmentPrompt('en')
  assert.ok(!/chunk index|level-\d|reduce fan-in|success percentage/i.test(prompt))
})

test('WHAT[DISTILL-005] EXEC_distill_spool_extracts_llm_output_without_thinking_and_without_recent_work_header', async () => {
  const { dir, spoolPath } = writeSpool([Buffer.from('small tail')])
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

  const { runtime } = fakeRuntime({
    awaitAgent: (agentId) => ({ ok: true, runId: `run-${agentId}`, workRecord: rawWorkRecord }),
  })

  try {
    const summary = await distillSpool(runtime, spoolPath, 'zh-CN')
    assert.equal(summary.includes('Recent work'), false, 'must not leak Recent work header')
    assert.equal(summary.includes('internal chain of thought'), false, 'must strip thinking/monologue')
    assert.equal(summary.includes('assistant:'), false, 'must strip assistant role prefix')
    assert.match(summary, /### 提炼证据/)
    assert.match(summary, /### 收尾报告/)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const { distillFragmentPrompt, distillSpool } = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')
const SPOOL_CHUNK_BYTES = 204_800

test('WHAT[DISTILL-001] DISTILLATION_prompt_declares_bounded_tail_and_forbids_whole_run_inference', () => {
  assert.equal(
    distillFragmentPrompt('en'),
    'Distill this bounded tail of command output. Earlier output may be absent. Preserve errors, decisions, paths, and exact numbers; never infer whole-run success from this tail; omit raw code.',
  )
})

test('WHAT[DISTILL-001] truncation_produces_nonempty_bounded_observation', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-distill-001-'))
  const spoolPath = join(dir, 'spool.bin')
  writeFileSync(spoolPath, Buffer.alloc(SPOOL_CHUNK_BYTES + 128, 0x61))

  let payload = null
  const runtime = {
    fork: (agentId, _prompt, body) => {
      payload = body
      return { ok: true, agentId }
    },
    awaitAgent: (agentId) => ({ ok: true, runId: `run-${agentId}`, workRecord: 'summary' }),
    awaitRecoveryReadiness: () => undefined,
    cancel: () => undefined,
  }

  try {
    const summary = await distillSpool(runtime, spoolPath, 'en')
    assert.ok(summary.length > 0)
    assert.ok(Buffer.byteLength(payload, 'utf8') <= SPOOL_CHUNK_BYTES)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const { distillSpool } = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')
const SPOOL_CHUNK_BYTES = 204_800

test('WHAT[DISTILL-007] EXEC_distill_spool_keeps_only_latest_200kib_payload', async () => {
  const earlyMarker = 'EARLY_BYTES_MUST_NOT_REACH_DISTILLER'
  const retainedSuffixMarker = 'PREVIOUS_CHUNK_SUFFIX_MUST_SURVIVE_28bc'
  const tailMarker = 'LATEST_FAILURE_MARKER_7f3a'
  const first = Buffer.alloc(SPOOL_CHUNK_BYTES, 0x61)
  first.write(earlyMarker, 0, 'utf8')
  first.write(retainedSuffixMarker, SPOOL_CHUNK_BYTES - 96, 'utf8')
  const last = Buffer.alloc(128, 0x62)
  last.write(tailMarker, 0, 'utf8')

  const dir = mkdtempSync(join(tmpdir(), 'wxs-distill-007-'))
  const spoolPath = join(dir, 'spool.bin')
  writeFileSync(spoolPath, Buffer.concat([first, last]))

  const forked = []
  const payloads = new Map()
  const runtime = {
    fork: (agentId, prompt, payload) => {
      forked.push(agentId)
      payloads.set(agentId, payload)
      return { ok: true, agentId }
    },
    awaitAgent: (agentId) => ({ ok: true, runId: `run-${agentId}`, workRecord: 'summary' }),
    awaitRecoveryReadiness: () => undefined,
    cancel: () => undefined,
  }

  try {
    await distillSpool(runtime, spoolPath, 'en')
    assert.equal(forked.length, 1)
    const payload = payloads.get(forked[0])
    assert.equal(Buffer.byteLength(payload, 'utf8'), SPOOL_CHUNK_BYTES)
    assert.ok(payload.includes(retainedSuffixMarker))
    assert.ok(payload.includes(tailMarker))
    assert.ok(!payload.includes(earlyMarker))
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

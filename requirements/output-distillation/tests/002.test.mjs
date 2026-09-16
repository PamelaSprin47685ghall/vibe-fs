import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const { distillSpool } = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')
const SPOOL_CHUNK_BYTES = 204_800

test('WHAT[DISTILL-002] bounded_tail_keeps_recent_judgment_changing_marker', async () => {
  const tailMarker = 'LATEST_PTY_CRASH_7f3a'
  const dir = mkdtempSync(join(tmpdir(), 'wxs-distill-002-'))
  const spoolPath = join(dir, 'spool.bin')
  const first = Buffer.alloc(SPOOL_CHUNK_BYTES, 0x61)
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
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

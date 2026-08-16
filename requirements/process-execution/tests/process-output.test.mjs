// Process owner output surface: bounded collection and streaming spool.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  estimate,
  outputCreate,
  outputAddStdout,
  outputAddStderr,
  outputBuildResult,
  outputView,
  spoolChunkCount,
  spoolChunkBytes,
  spoolStart,
  spoolAppend,
  spoolPath,
  spoolBytesWritten,
  spoolRead,
  spoolReadPath,
  spoolDelete,
} = await import('../../../dist/Process/Surface.js')

const text = (value) => new TextEncoder().encode(value)
const decode = (bytes) => new TextDecoder().decode(Uint8Array.from(bytes))
const create = (seconds, bytes) => outputCreate(estimate(seconds, bytes, 'medium'))

const tempSpool = (value) => {
  const spool = spoolStart()
  spoolAppend(spool, text(value))
  return spool
}

const readSpool = async (spool) => {
  const chunks = spool.path ? await spoolReadPath(spool.path) : await spoolRead(spool)
  return chunks.map(decode).join('')
}

// ── OutputCollector ──────────────────────────────────────────────────────────

test('WHAT[PROC-009] EXEC_011_collector_concatenates_stdout_stderr_utf8', () => {
  const collector = create(10, 1000)
  outputAddStdout(collector, text('hello '))
  outputAddStderr(collector, text('warn\n'))
  outputAddStdout(collector, text('世界'))
  assert.deepEqual(outputBuildResult(collector, 7), {
    kind: 'Completed',
    exitCode: 7,
    stdout: 'hello 世界',
    stderr: 'warn\n',
    spooled: false,
  })
})

test('WHAT[PROC-009] EXEC_011_collector_ignores_empty_chunks', () => {
  const collector = create(10, 1000)
  outputAddStdout(collector, new Uint8Array(0))
  outputAddStdout(collector, undefined)
  assert.equal(outputView(collector).bytesObserved, 0)
  assert.equal(outputBuildResult(collector, 0).stdout, '')
})

test('WHAT[PROC-009] EXEC_011_collector_spools_when_byte_count_crosses_threshold', async () => {
  const collector = create(10, 2)
  outputAddStdout(collector, text('abcdefg'))
  assert.equal(outputView(collector).spooled, true, 'crossing the threshold must start a spool')

  const outcome = outputBuildResult(collector, 3)
  assert.equal(outcome.kind, 'Spooled')
  assert.equal(outcome.exitCode, 3)
  assert.equal(outcome.totalBytes, 7)
  assert.equal(outcome.chunkCount, 1)
  assert.equal(await readSpool({ path: outcome.spoolPath }), 'abcdefg')
  spoolDelete(outcome.spoolPath)
})

test('WHAT[PROC-009] EXEC_011_collector_spool_accumulates_later_chunks', async () => {
  const collector = create(10, 2)
  outputAddStdout(collector, text('abcdefg'))
  outputAddStderr(collector, text('x'))

  const outcome = outputBuildResult(collector, 4)
  assert.equal(outcome.kind, 'Spooled')
  assert.equal(outcome.totalBytes, 8)
  assert.equal(outcome.chunkCount, 1)
  assert.equal(await readSpool({ path: outcome.spoolPath }), 'abcdefgx')
  spoolDelete(outcome.spoolPath)
})

test('WHAT[PROC-009] EXEC_011_collector_spooled_buffers_are_cleared', () => {
  const collector = create(10, 2)
  outputAddStdout(collector, text('abcdefg'))
  const view = outputView(collector)
  assert.equal(view.stdoutChunks, 0)
  assert.equal(view.stderrChunks, 0)
})

// ── Spool primitives ─────────────────────────────────────────────────────────

test('WHAT[PROC-009] EXEC_011_spool_chunk_count_rounds_up', () => {
  assert.equal(spoolChunkCount(0), 0)
  assert.equal(spoolChunkCount(1), 1)
  assert.equal(spoolChunkCount(204800), 1)
  assert.equal(spoolChunkCount(204801), 2)
})

test('WHAT[PROC-009] EXEC_011_spool_chunk_bytes_splits_at_chunk_size', () => {
  assert.deepEqual(spoolChunkBytes(3, new Uint8Array(0)), [])
  assert.deepEqual(spoolChunkBytes(3, undefined), [])
  assert.deepEqual(spoolChunkBytes(3, new Uint8Array([1, 2, 3, 4, 5])), [
    [1, 2, 3],
    [4, 5],
  ])
})

test('WHAT[PROC-009] EXEC_011_spool_round_trips_bytes_through_temp_file', async () => {
  const spool = tempSpool('0123456789')
  assert.equal(spoolBytesWritten(spool), 10)
  assert.equal(spoolChunkCount(spoolBytesWritten(spool)), 1)
  assert.equal(await readSpool(spool), '0123456789')
  spoolDelete(spoolPath(spool))
})

test('WHAT[PROC-009] EXEC_011_spool_append_tracks_bytes_written', async () => {
  const spool = spoolStart()
  spoolAppend(spool, text('ab'))
  spoolAppend(spool, new Uint8Array(0))
  assert.equal(spoolBytesWritten(spool), 2)
  assert.equal(await readSpool(spool), 'ab')
  spoolDelete(spoolPath(spool))
})

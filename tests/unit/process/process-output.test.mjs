// tests/unit/process/process-output.test.mjs — VERIFY-009 coverage targets.
//
// EXEC-011 process estimates, the OutputCollector (stdout/stderr aggregation, byte
// counting, spool-threshold handoff) and the Spool chunking primitives. Pure byte
// and TimeSpan math; the only side effect is a temp spool file, deleted per test.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf } from '../support/domain.mjs'

const {
  EstimatedRuntime,
  EstimatedOutput,
  EstimatedMemory,
  ProcessEstimateModule_DefaultHardLimit,
  ProcessEstimateModule_effectiveDeadline,
  ProcessEstimateModule_outputThreshold,
} = await import('../../../dist/Process/ProcessRequest.js')

const { OutputCollector, create, addStdout, addStderr, buildResult } = await import(
  '../../../dist/Process/ProcessOutput.js'
)

const {
  chunkBytes,
  chunkCount,
  appendStreamingSpool,
  delete$: spoolDelete,
  readChunksSync,
  spoolBytesToTempFile,
  startStreamingSpool,
} = await import('../../../dist/Process/Spool.js')

// Spool.ChunkSizeBytes is a [<Literal>] — Fable inlines it and exports nothing.
const CHUNK_SIZE_BYTES = 204800

const { fromSeconds, fromHours, compare } = await import(
  '../../../dist/fable_modules/fable-library-js.5.13.0/TimeSpan.js'
)

const runtime = (seconds) => new EstimatedRuntime(seconds)
const output = (bytes) => new EstimatedOutput(bytes)
const estimate = (seconds, bytes, memory = EstimatedMemory.Medium) => ({
  EstimatedRuntime: runtime(seconds),
  EstimatedOutput: output(bytes),
  EstimatedMemory: memory,
})

const text = (value) => new TextEncoder().encode(value)

// ── EXEC-011: estimate math ──────────────────────────────────────────────────

test('EXEC_011_output_threshold_uses_provider_willingness_at_face_value', () => {
  assert.equal(ProcessEstimateModule_outputThreshold(output(0n)), 0n)
  assert.equal(ProcessEstimateModule_outputThreshold(output(-5n)), 0n)
  assert.equal(ProcessEstimateModule_outputThreshold(output(10n)), 10n)
})

test('EXEC_011_effective_deadline_is_min_of_estimate_and_hard_limit', () => {
  const oneHour = fromHours(1)
  assert.equal(compare(ProcessEstimateModule_effectiveDeadline(runtime(10), oneHour), fromSeconds(10)), 0)
  assert.equal(compare(ProcessEstimateModule_effectiveDeadline(runtime(100), oneHour), fromSeconds(100)), 0)
  assert.equal(compare(ProcessEstimateModule_effectiveDeadline(runtime(2000), oneHour), fromSeconds(2000)), 0)
  assert.equal(compare(ProcessEstimateModule_effectiveDeadline(runtime(5000), oneHour), oneHour), 0)
})

test('EXEC_011_nonfinite_or_nonpositive_estimate_collapses_to_hard_limit', () => {
  const hard = fromSeconds(60)
  for (const bad of [NaN, Infinity, -Infinity, 0, -10]) {
    assert.equal(compare(ProcessEstimateModule_effectiveDeadline(runtime(bad), hard), hard), 0, String(bad))
  }
})

test('EXEC_011_default_hard_limit_is_one_hour', () => {
  assert.equal(compare(ProcessEstimateModule_DefaultHardLimit, fromHours(1)), 0)
})

// ── OutputCollector ──────────────────────────────────────────────────────────

test('EXEC_011_collector_concatenates_stdout_stderr_utf8', () => {
  const collector = create(estimate(10, 1000n))
  addStdout(collector, text('hello '))
  addStderr(collector, text('warn\n'))
  addStdout(collector, text('世界'))
  const outcome = buildResult(collector, 7)
  assert.equal(caseOf(outcome), 'Completed')
  const [exitCode, stdout, stderr, spooled] = outcome.fields
  assert.equal(exitCode, 7)
  assert.equal(stdout, 'hello 世界')
  assert.equal(stderr, 'warn\n')
  assert.equal(spooled, false)
})

test('EXEC_011_collector_ignores_empty_chunks', () => {
  const collector = create(estimate(10, 1000n))
  addStdout(collector, new Uint8Array(0))
  addStdout(collector, undefined)
  assert.equal(collector.BytesObserved, 0n)
  const outcome = buildResult(collector, 0)
  assert.equal(caseOf(outcome), 'Completed')
  assert.equal(outcome.fields[1], '')
})

test('EXEC_011_collector_spools_when_byte_count_crosses_threshold', () => {
  // Threshold = 2 × 3 = 6 bytes. Seven bytes cross it.
  const collector = create(estimate(10, 2n))
  addStdout(collector, text('abcdefg'))
  assert.notEqual(collector.Spool, undefined, 'crossing the threshold must start a spool')

  const outcome = buildResult(collector, 3)
  assert.equal(caseOf(outcome), 'Spooled')
  const [exitCode, spoolPath, totalBytes, chunks] = outcome.fields
  assert.equal(exitCode, 3)
  assert.equal(totalBytes, 7n)
  assert.equal(chunks, 1)

  const seen = []
  readChunksSync(spoolPath, (bytes) => seen.push(new TextDecoder().decode(bytes)))
  assert.equal(seen.join(''), 'abcdefg')

  spoolDelete(spoolPath)
})

test('EXEC_011_collector_spool_accumulates_later_chunks', () => {
  const collector = create(estimate(10, 2n))
  addStdout(collector, text('abcdefg')) // 7 bytes → spool started
  addStderr(collector, text('x')) // appended to the same spool file

  const outcome = buildResult(collector, 4)
  assert.equal(caseOf(outcome), 'Spooled')
  const [, spoolPath, totalBytes, chunks] = outcome.fields
  assert.equal(totalBytes, 8n)
  assert.equal(chunks, 1)

  const seen = []
  readChunksSync(spoolPath, (bytes) => seen.push(new TextDecoder().decode(bytes)))
  assert.equal(seen.join(''), 'abcdefgx')

  spoolDelete(spoolPath)
})

test('EXEC_011_collector_spooled_buffers_are_cleared', () => {
  const collector = create(estimate(10, 2n))
  addStdout(collector, text('abcdefg'))
  assert.equal(collector.Stdout.length, 0)
  assert.equal(collector.Combined.length, 0)
})

// ── Spool primitives ─────────────────────────────────────────────────────────

test('EXEC_011_spool_chunk_count_rounds_up', () => {
  assert.equal(chunkCount(0n), 0)
  assert.equal(chunkCount(1n), 1)
  assert.equal(chunkCount(BigInt(CHUNK_SIZE_BYTES)), 1)
  assert.equal(chunkCount(BigInt(CHUNK_SIZE_BYTES) + 1n), 2)
})

test('EXEC_011_spool_chunk_bytes_splits_at_chunk_size', () => {
  assert.deepEqual(chunkBytes(3, new Uint8Array(0)), [])
  assert.deepEqual(chunkBytes(3, undefined), [])
  const parts = chunkBytes(3, new Uint8Array([1, 2, 3, 4, 5]))
  assert.equal(parts.length, 2)
  assert.deepEqual(parts[0], new Uint8Array([1, 2, 3]))
  assert.deepEqual(parts[1], new Uint8Array([4, 5]))
})

test('EXEC_011_spool_round_trips_bytes_through_temp_file', () => {
  const [path, totalBytes, chunks] = spoolBytesToTempFile(text('0123456789'))
  assert.equal(totalBytes, 10n)
  assert.equal(chunks, 1)

  const seen = []
  readChunksSync(path, (bytes) => seen.push(new TextDecoder().decode(bytes)))
  assert.equal(seen.join(''), '0123456789')

  spoolDelete(path)
})

test('EXEC_011_spool_append_tracks_bytes_written', () => {
  const spool = startStreamingSpool()
  appendStreamingSpool(spool, text('ab'))
  appendStreamingSpool(spool, new Uint8Array(0)) // empty → no-op
  assert.equal(spool.BytesWritten, 2n)

  const seen = []
  readChunksSync(spool.Path, (bytes) => seen.push(new TextDecoder().decode(bytes)))
  assert.equal(seen.join(''), 'ab')

  spoolDelete(spool.Path)
})

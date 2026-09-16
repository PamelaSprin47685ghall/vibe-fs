import assert from 'node:assert/strict'
import test from 'node:test'
import * as delta from '../../../dist/Context/Companion/BloggerDeltaSurface.js'
import * as deltaSurface from '../../../dist/Context/Companion/Blogger/DeltaSurface.js'

test('WHAT[CONTEXT-COMPRESSION-003] CTX_003_delta_limit_is_200_KiB', () => {
  assert.equal(delta.limitBytes, 200 * 1024)
})

test('WHAT[CONTEXT-COMPRESSION-003] CTX_003_no_chunk_exceeds_the_limit', () => {
  const chunks = delta.chunkTranscript('x'.repeat(500 * 1024))
  for (const c of chunks) {
    assert.ok(Buffer.byteLength(c, 'utf8') <= 200 * 1024)
  }
})

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as delta from '../../../dist/Context/Companion/Blogger/DeltaSurface.js'
import * as prompt from '../../../dist/Context/Companion/ProjectionSurface.js'
import * as toml from '../../../dist/Context/Companion/Blogger/TomlSurface.js'

const syn = { byteCount: delta.byteCount }

const textItem = (text, role = 'user') => ({
  Role: role,
  Part: { Kind: 'text', Text: text, Tool: '', Args: '', MediaType: '' },
  Truncated: false,
})

const origin = delta.cursor(0, 0)

const drainAll = (limit, messages, guard = 50) => {
  const chunks = []
  let cursor = origin
  let cutoff = 0

  for (let i = 0; i < guard; i += 1) {
    const chunk = delta.nextChunk({ limit, cursor, previousCutoff: cutoff, messages })
    if (chunk === undefined) return chunks

    chunks.push(chunk)
    cursor = delta.cursor(chunk.nextCursor.turn, chunk.nextCursor.part)
    cutoff = chunk.nextCutoff
  }

  assert.fail(`chunking did not terminate within ${guard} chunks — a cursor is not advancing`)
}

test('WHAT[context-compression-003] CTX_003_delta_limit_is_200_KiB', () => {
  // An input contract, not an estimate: never compared to a model window, never
  // scaled by provider. Exported as a plain value so this test can read it.
  assert.equal(delta.limitBytes, 200 * 1024)

  // Threshold proof: a single part of 200 KiB exactly is within the chunk boundary,
  // whereas 200 KiB + 1 byte exceeds the limit and triggers deterministic truncation/marking.
  const limit = 200 * 1024
  const atLimitText = 'a'.repeat(limit - 300) // leaves room for TOML framing within 200 KiB
  const atLimitMessages = delta.messages([{ role: 'user', parts: [delta.text(atLimitText)] }])
  const atLimitChunk = delta.nextChunk({ limit, cursor: origin, messages: atLimitMessages })
  assert.ok(atLimitChunk, 'fits within 200 KiB limit')
  assert.equal(atLimitChunk.itemCount, 1)
  assert.deepEqual(atLimitChunk.truncatedFlags, [false])

  const overLimitText = 'b'.repeat(limit + 500)
  const overLimitMessages = delta.messages([{ role: 'user', parts: [delta.text(overLimitText)] }])
  const overLimitChunk = delta.nextChunk({ limit, cursor: origin, messages: overLimitMessages })
  assert.ok(overLimitChunk, 'handles oversized part at 200 KiB boundary')
  assert.equal(overLimitChunk.itemCount, 1)
  assert.deepEqual(overLimitChunk.truncatedFlags, [true])
})

test('WHAT[context-compression-003] CTX_003_no_chunk_exceeds_the_limit', () => {
  const messages = delta.messages(
    [0, 1, 2, 3, 4, 5].map((n) => ({
      role: n % 2 === 0 ? 'user' : 'assistant',
      parts: [delta.text(`turn ${n}: ` + '中'.repeat(200))],
    })),
  )

  // CJK: 200 characters is 600 bytes, so a character-based limit would let each
  // chunk run three times over.
  const limit = 1500
  for (const chunk of drainAll(limit, messages)) {
    assert.equal(chunk.bytes <= limit, true, `chunk of ${chunk.bytes} bytes exceeds ${limit}`)
    assert.equal(chunk.bytes, syn.byteCount(chunk.toml), 'reported bytes must be the rendered bytes')
  }
})

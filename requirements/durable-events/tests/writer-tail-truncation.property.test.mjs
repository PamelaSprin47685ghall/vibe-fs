import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import fc from 'fast-check'

import * as eventCodec from '../../../dist/Persistence/EventStore/CodecSurface.js'
import * as retention from '../../../dist/Persistence/EventStore/RetentionSurface.js'

const propertyOptions = { seed: 0x44555241, numRuns: 300 }
const payloads = fc.array(fc.string({ maxLength: 128 }), { minLength: 1, maxLength: 12 })
const cutSeed = fc.nat()

const event = (index, text) => ({
  id: (index + 1).toString(16).padStart(40, '0'),
  stream: 'property/writer-tail',
  type: 'JobRequested',
  parents: index === 0 ? [] : [index.toString(16).padStart(40, '0')],
  payload: { text },
  payloadRefs: [],
})

const truncatedWriter = (values, seed) => {
  const lines = values.map((text, index) => Buffer.from(eventCodec.encode(event(index, text))))
  const lastLine = lines.at(-1)
  const removedBytes = 1 + (seed % (lastLine.length - 1))
  return Buffer.concat(lines).subarray(0, -removedBytes)
}

const assertIncompleteTailRejected = (read) => {
  assert.throws(read, (error) => /incomplete trailing line/i.test(String(error)))
}

const withWriter = (run) => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiang-writer-tail-property-'))
  const commonDir = join(root, '.git')
  const eventsDir = join(commonDir, 'wanxiang', 'events')
  const writerPath = join(eventsDir, 'property-writer.ndjson')
  mkdirSync(eventsDir, { recursive: true })
  try {
    return run({ commonDir, writerPath })
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

test('WHAT[DURABLE-EVENTS-004] every incomplete canonical writer tail fails closed', () => {
  withWriter(({ commonDir, writerPath }) => {
    fc.assert(
      fc.property(payloads, cutSeed, (values, seed) => {
        writeFileSync(writerPath, truncatedWriter(values, seed))
        assertIncompleteTailRejected(() => retention.retainedWriterIdsAt(commonDir, 0))
      }),
      propertyOptions,
    )
  })
})

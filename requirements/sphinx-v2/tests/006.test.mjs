import assert from 'node:assert/strict'
import test from 'node:test'
import * as Wire from '../../../dist/Sphinx/V2/Wire/Surface.js'
import { body, envelope, batch, append, current, mustOk, withStore } from './persistence-support.mjs'
import {
  interpretationInquiry, interpretationObservation, interpretationApplied, interpretationFailed,
  interpretationSeedBodies, interpretationRecord, pendingInterpretationRecord,
} from './interpretation-support.mjs'

test('WHAT[sphinx-v2-006] pending interpretation exposes only its original logical provenance without an invented semantic outcome', async () => {
  await withStore(async ({ open, commonDir }) => {
    const writer = open()
    await append(writer, batch(interpretationInquiry, 'accepted-raw', interpretationSeedBodies()))
    const reader = Wire.create(commonDir, 'pending-provenance-reader', null)
    try {
      const full = Wire.exportInquiry(reader, { inquiryId: interpretationInquiry, mode: 'full' })
      assert.equal(full.outcome, 'exported', JSON.stringify(full))
      assert.deepEqual(full.state.interpretations[0].value, pendingInterpretationRecord)
      assert.deepEqual(full.inquiry.interpretations, [{
        key: interpretationObservation, status: 'pending', interpretationId: null, plugin: null,
      }])
      assert.equal(Object.keys(full.state.interpretations[0].value).length, 7)
      assert.equal(Object.keys(full.inquiry.interpretations[0]).length, 4)
    } finally {
      Wire.dispose(reader)
    }
  })
})

test('WHAT[sphinx-v2-006] an Applied delta remains an opaque recorded envelope rather than a Core epistemic or graph judgment', async () => {
  await withStore(async ({ open, close }) => {
    const writer = open()
    const previous = await append(writer, batch(interpretationInquiry, 'accepted-raw', interpretationSeedBodies()))
    const opaque = envelope('{"HypothesisTrue":"opaque plugin material","nodes":["node-never-created"]}')
    const applied = body('InterpretationApplied', { ...interpretationApplied.payload, delta: opaque })
    await append(writer, batch(interpretationInquiry, 'opaque-applied', [applied], previous))
    const live = current(writer, interpretationInquiry)
    assert.deepEqual(interpretationRecord(live), {
      ...pendingInterpretationRecord,
      interpretationId: applied.payload.interpretationId,
      pluginRef: applied.payload.pluginRef, status: 'applied', delta: opaque,
    })
    assert.deepEqual(mustOk(live).graph, [])
    assert.deepEqual(mustOk(live).edges, [])
    assert.deepEqual(mustOk(live).certificates, [])
    assert.deepEqual(mustOk(live).status, { case: 'Active' })
    close(writer)
    assert.deepEqual(current(open(), interpretationInquiry), live)
  })
})

test('WHAT[sphinx-v2-006] different opaque outcome contents remain distinguishable in complete and semantic state without Core interpretation', async t => {
  for (const kind of ['Applied', 'Failed']) {
    await t.test('WHAT[sphinx-v2-006] retains distinct ' + kind + ' contents by identity', async () => {
      const exports = []
      for (const text of ['原文甲', '原文乙']) {
        await withStore(async ({ open, commonDir }) => {
          const writer = open()
          const previous = await append(writer, batch(interpretationInquiry, 'accepted-raw', interpretationSeedBodies()))
          const outcome = kind === 'Applied'
            ? body('InterpretationApplied', { ...interpretationApplied.payload, delta: envelope(JSON.stringify({ opaque: text })) })
            : body('InterpretationFailed', { ...interpretationFailed.payload, reason: text })
          await append(writer, batch(interpretationInquiry, 'same-outcome-command', [outcome], previous))
          const reader = Wire.create(commonDir, 'opaque-outcome-reader', null)
          try {
            const full = Wire.exportInquiry(reader, { inquiryId: interpretationInquiry, mode: 'full' })
            assert.equal(full.outcome, 'exported', JSON.stringify(full))
            assert.equal(full.state.interpretations[0].value.status, kind.toLowerCase())
            assert.deepEqual(full.state.graph, [])
            assert.deepEqual(full.state.certificates, [])
            exports.push(full)
          } finally {
            Wire.dispose(reader)
          }
        })
      }
      assert.equal(exports[0].events.length, exports[1].events.length)
      assert.notEqual(exports[0].stateHash, exports[1].stateHash)
      assert.notEqual(exports[0].semanticHash, exports[1].semanticHash)
      assert.notEqual(exports[0].traceHash, exports[1].traceHash)
    })
  }
})

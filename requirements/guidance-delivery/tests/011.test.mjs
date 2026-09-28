import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as guideline from '../../../dist/OpenCode/Host/PairProgramming/GuidelineSurface.js'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'

const payload = (overrides = {}) => ({
  ordinal: 1n, callId: 'call-1', markerText: '原文字节\r\n  preserved',
  callGap: 'before:m1', resultGap: 'after:m1', ...overrides,
})

test('WHAT[guidance-delivery-011] actual fold preserves order bytes and placement and rejects conflicting identities', () => {
  assert.equal(guideline.nextOrdinal(guideline.empty), 1n)
  const first = guideline.apply(payload(), guideline.empty)
  assert.equal(first.ok, true)
  assert.deepEqual(guideline.pairs(first.value), [payload()])
  for (const [input, rejection] of [
    [payload({ ordinal: 3n, callId: 'call-2' }), 'NonSequentialOrdinal'],
    [payload({ ordinal: 2n, callGap: 'before:m2', resultGap: 'after:m2' }), 'DuplicateCallId'],
    [payload({ ordinal: 2n, callId: 'call-2' }), 'DuplicatePlacement'],
  ]) {
    const result = guideline.apply(input, first.value)
    assert.equal(result.ok, false)
    assert.equal(result.error.name, rejection)
  }
  const reanchored = guideline.applyReanchor(first.value)
  assert.deepEqual(guideline.visiblePairs(reanchored), [])
  assert.deepEqual(guideline.pairs(reanchored), [payload()])
  assert.equal(guideline.nextOrdinal(reanchored), 2n)
  const second = guideline.apply(payload({ ordinal: 2n, callId: 'call-2' }), reanchored)
  assert.equal(second.ok, true)
  assert.deepEqual(guideline.pairs(second.value).map((item) => item.callId), ['call-1', 'call-2'])
})

const resultMessage = (id) => ({
  info: { id, role: 'assistant' },
  parts: [{ type: 'tool', tool: 'read', callID: `call-${id}`,
    state: { status: 'completed', input: {}, output: `body-${id}\r\n`, time: { start: 0, end: 1 } } }],
})

test('WHAT[guidance-delivery-011] actual durable replay freezes prior bytes while a new placement gets new material', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'guideline-replay-'))
  let journal
  const open = async () => {
    const result = await pair.createJournal(directory)
    assert.equal(result.ok, true, result.error)
    journal = result.journal
  }
  const inject = async (marker, messages) => {
    const result = await pair.tryInjectWithJournal(journal, 'main', marker, messages)
    assert.equal(result.ok, true, result.error)
    return result.value
  }
  try {
    await open()
    const original = '旧规则\r\n 原样  '
    const updated = 'NEW RULE AND CHANGED CALIBRATION'
    const firstInput = [resultMessage('first')]
    const first = await inject(original, firstInput)
    assert.equal(first[0].parts[0].state.output, `body-first\r\n\0\uFEFF${original}`)
    pair.disposeJournal(journal)
    journal = undefined
    await open()
    assert.deepEqual(await inject(updated, firstInput), first)
    assert.equal(pair.pairCount(journal, 'main'), 1)
    const next = await inject(updated, [...firstInput, resultMessage('second')])
    assert.equal(next[0].parts[0].state.output, first[0].parts[0].state.output)
    assert.equal(next[1].parts[0].state.output, `body-second\r\n\0\uFEFF${updated}`)
    assert.equal(pair.pairCount(journal, 'main'), 2)
  } finally {
    if (journal) pair.disposeJournal(journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

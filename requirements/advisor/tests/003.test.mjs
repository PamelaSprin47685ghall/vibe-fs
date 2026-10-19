import assert from 'node:assert/strict'
import test from 'node:test'

import { clear, clearAll, delivered, plan, record } from '../../../dist/Execution/Delegation/Advisor/Surface.js'

// advisor-[003]: a follow-up appends the view delta behind the delivered cursor, never the whole
// view again. The cursor is process-live and keyed by caller session.
const message = (anchor, text) => ({ anchor, text })

const request = (messages, { prefixUntil, thenRemove = [] } = {}) => ({
  messages,
  declaration: { prefixUntil, thenRemove },
})

// The authored header prose itself names `[[new_work_to_record]]`, so every table count below is
// scoped to the data body — the part after the header's blank line.
const viewBody = (prompt) => {
  const boundary = prompt.indexOf('\n\n')
  return boundary === -1 ? '' : prompt.slice(boundary + 2)
}

const tableCount = (prompt) => (viewBody(prompt).match(/\[\[new_work_to_record\]\]/g) ?? []).length

const material = (anchor) => `anchor-${anchor}-material`

const messages = (anchors) => anchors.map((anchor) => message(anchor, material(anchor)))

test('WHAT[advisor-003] a follow-up plan with the previous DeliveredAnchors emits the delta alone', () => {
  const first = plan('en', [], request(messages([1, 2, 3]), { prefixUntil: 4 }))
  assert.deepEqual(first.deliveredAnchors, [1, 2, 3])
  assert.equal(tableCount(first.viewPrompt), 3)

  const second = plan('en', first.deliveredAnchors, request(messages([1, 2, 3, 4, 5]), { prefixUntil: 6 }))
  assert.equal(second.hasNewMaterial, true)
  assert.deepEqual(second.deliveredAnchors, [1, 2, 3, 4, 5])
  assert.equal(tableCount(second.viewPrompt), 2, 'only the two new anchors are handed over')
  assert.equal(second.viewPrompt.includes(material(4)), true)
  assert.equal(second.viewPrompt.includes(material(5)), true)
  for (const anchor of [1, 2, 3]) {
    assert.equal(
      second.viewPrompt.includes(material(anchor)),
      false,
      `anchor ${anchor} was already delivered and must not be re-sent`,
    )
  }

  const third = plan('en', second.deliveredAnchors, request(messages([1, 2, 3, 4, 5]), { prefixUntil: 6 }))
  assert.equal(third.hasNewMaterial, false)
  assert.equal(tableCount(third.viewPrompt), 0, 'an unchanged view re-sends no table')
  assert.deepEqual(third.deliveredAnchors, [1, 2, 3, 4, 5])
})

test('WHAT[advisor-003] a ThenRemove pair does not re-open an already delivered anchor', () => {
  const planned = plan(
    'en',
    [1, 2],
    request(messages([1, 2, 3]), { prefixUntil: 4, thenRemove: [[1, 'anchor-one-summary']] }),
  )

  assert.equal(tableCount(planned.viewPrompt), 1)
  assert.equal(planned.viewPrompt.includes(material(3)), true)
  assert.equal(
    planned.viewPrompt.includes('anchor-one-summary'),
    false,
    'a delivered anchor stays delivered even when its removal pair is declared',
  )
  assert.equal(planned.viewPrompt.includes(material(1)), false)
  assert.deepEqual(planned.deliveredAnchors, [1, 2, 3])
})

test('WHAT[advisor-003] the delivered cursor accumulates per caller session and only grows', () => {
  const suffix = Math.random().toString(36).slice(2, 8)
  const caller = `sess-advisor-cursor-${suffix}`
  const other = `sess-advisor-cursor-${suffix}-other`

  try {
    assert.deepEqual(delivered(caller), [], 'a fresh session has delivered nothing')

    record(caller, [5, 3])
    assert.deepEqual(delivered(caller), [3, 5])

    record(caller, [1])
    assert.deepEqual(delivered(caller), [1, 3, 5], 'recording accumulates')

    // Monotone: an earlier (here: subset) anchor set cannot shrink the counts.
    record(caller, [1])
    record(caller, [])
    assert.deepEqual(delivered(caller), [1, 3, 5], 'the cursor never loses an anchor')

    record(other, [2])
    assert.deepEqual(delivered(other), [2], 'a second caller session has its own cursor')
    assert.deepEqual(delivered(caller), [1, 3, 5], 'sessions do not share a cursor')

    clear(caller)
    assert.deepEqual(delivered(caller), [])
    assert.deepEqual(delivered(other), [2], 'clear resets one session only')
  } finally {
    clearAll()
  }

  assert.deepEqual(delivered(caller), [])
  assert.deepEqual(delivered(other), [])
})

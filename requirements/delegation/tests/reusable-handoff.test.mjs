import assert from 'node:assert/strict'
import test from 'node:test'
import * as handoff from '../../../dist/Execution/Delegation/HandoffSurface.js'

test('WHAT[DELEG-024] reusable handoff advances one durable parent delta window at a time', () => {
  const first = handoff.handoffWindow(null, 10)
  assert.deepEqual(first, { start: 0, end: 10, isInitial: true })

  const second = handoff.handoffWindow(10, 17)
  assert.deepEqual(second, { start: 10, end: 17, isInitial: false })

  assert.deepEqual(handoff.handoffWindow(17, 17), { start: 17, end: 17, isInitial: false })
})

test('WHAT[DELEG-024] reusable prompt carries the new charge and parent delta as data', () => {
  const prompt = handoff.render('fix the second defect', 'parent delta only')
  assert.match(prompt, /# fix the second defect/)
  assert.match(prompt, /parent_delta_work_record\s*=/)
  assert.match(prompt, /parent delta only/)
})

test('WHAT[DELEG-024] bounded child result never widens to an earlier invocation', () => {
  assert.deepEqual(handoff.childRange(31, 44), { start: 31, end: 44 })
})

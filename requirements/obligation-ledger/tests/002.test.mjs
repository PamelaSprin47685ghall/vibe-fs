import assert from 'node:assert/strict'
import test from 'node:test'
import * as sink from '../../../dist/Participant/Cognition/TodoSinkSurface.js'

const rows = [
  {content: '重复内容', status: 'pending', priority: 'low'},
  {content: '重复内容', status: 'completed', priority: 'high'},
  {content: '  前后空格\r\n第二行  ', status: 'cancelled', priority: 'medium'},
]

test('WHAT[obligation-ledger-002] actual sink preserves duplicate rows exact text and declared order', () => {
  assert.deepEqual(sink.projectArgs({update: '.', todos: rows}), {ok: true, todos: rows})
  const reversed = [...rows].reverse()
  assert.deepEqual(sink.projectArgs({update: '.', todos: reversed}), {ok: true, todos: reversed})
})

test('WHAT[obligation-ledger-002] explicit empty todos produces an empty Host replacement', () => {
  assert.deepEqual(sink.projectArgs({update: '.', todos: []}), {ok: true, todos: []})
})

test.todo('WHAT[obligation-ledger-002] the installed Host replaces and clears only the current owner session UI')

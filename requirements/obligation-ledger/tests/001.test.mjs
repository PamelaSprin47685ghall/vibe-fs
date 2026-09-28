import assert from 'node:assert/strict'
import test from 'node:test'
import * as admission from '../../../dist/Participant/Cognition/AdmissionSurface.js'
import * as sink from '../../../dist/Participant/Cognition/TodoSinkSurface.js'

const row = {content: '检查实际结果\n保留原文', status: 'in_progress'}

test('WHAT[obligation-ledger-001] actual assume admission normalizes only the declared optional priority', () => {
  assert.deepEqual(admission.tryDecode({update: '.', todos: [row]}), {
    ok: true, value: {update: '.', todos: [{...row, priority: 'medium'}]},
  })
  for (const status of ['pending', 'in_progress', 'completed', 'cancelled']) {
    assert.deepEqual(admission.tryDecode({update: '.', todos: [{...row, status, priority: 'high'}]}), {
      ok: true, value: {update: '.', todos: [{...row, status, priority: 'high'}]},
    })
  }
})

test('WHAT[obligation-ledger-001] missing and invalid statuses do not become pending by a test adapter default', () => {
  for (const todos of [[{content: 'task'}], [{...row, status: 'done'}], [{...row, status: 1}], [{status: 'pending'}]]) {
    const input = {update: '.', todos}
    assert.equal(admission.tryDecode(input).ok, false)
    assert.equal(sink.projectArgs(input).ok, false)
  }
})

test('WHAT[obligation-ledger-001] retired planning arguments are refused by the real parser', () => {
  for (const name of ['planComplete', 'workingOn', 'horizon', 'obligations', 'revision']) {
    assert.equal(admission.rejectsBecause({update: '.', todos: [row], [name]: true}), 'UnknownArgument')
  }
})

test('WHAT[obligation-ledger-001] actual sink projects only complete Host rows', () => {
  assert.deepEqual(sink.projectArgs({update: '.', todos: [row]}), {
    ok: true, todos: [{...row, priority: 'medium'}],
  })
})

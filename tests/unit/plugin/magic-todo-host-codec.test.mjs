import assert from 'node:assert/strict'
import test from 'node:test'
import {
  listItems,
  magicTodoHost,
} from '../support/domain.mjs'

test('TODO-002 decodes only tagged V2 rows and preserves omitted new ids', () => {
  const decoded = magicTodoHost.decodeV2({
    todos: [
      { kind: 'existing', id: 'todo-1', content: 'Review bridge', status: 'reviewing', priority: 'high' },
      { kind: 'new', content: 'Close proof', status: 'pending', priority: 'medium' },
    ],
  })

  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  const rows = listItems(decoded.value)
  assert.equal(rows[0].Kind, 'existing')
  assert.equal(rows[0].Id, 'todo-1')
  assert.equal(rows[1].Kind, 'new')
  assert.equal(rows[1].Id, undefined)

  const malformed = magicTodoHost.decodeV2({ todos: [{ kind: 'existing', id: 1 }] })
  assert.equal(malformed.ok, false)
  assert.equal(malformed.error, 'todowrite.id must be a string')
})

test('TODO-004 strips V2 fields before invoking the V1 builtin executor', () => {
  const output = { args: { todos: [{ kind: 'new' }] } }
  magicTodoHost.replaceCompatibilityArgs(output, [
    { Content: 'Review bridge', Status: 'reviewing', Priority: 'high' },
  ])

  assert.deepEqual(output.args, {
    todos: [{ content: 'Review bridge', status: 'reviewing', priority: 'high' }],
  })
})

test('TODO-002 replaces only the todowrite definition contract', () => {
  const output = { description: '', parameters: {} }
  magicTodoHost.applyDefinition(output)

  assert.match(output.description, /tagged Magic Todo V2 payload/)
  assert.deepEqual(output.parameters.required, ['todos'])
  assert.equal(output.parameters.properties.todos.type, 'array')
})

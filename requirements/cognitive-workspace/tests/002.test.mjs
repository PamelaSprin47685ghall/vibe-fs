import assert from 'node:assert/strict'
import test from 'node:test'
import * as workspace from '../../../dist/Participant/Cognition/WorkspaceSurface.js'
import * as admission from '../../../dist/Participant/Cognition/AdmissionSurface.js'

// cognitive-workspace-002/003: `update` must produce exactly one JSON value, and the
// canvas plus the declared todos form one committed envelope. These tests exercise
// the pure admission core, where the failures are decidable before any effect.
test('WHAT[cognitive-workspace-002] update and todos are the only accepted arguments', () => {
  const result = admission.tryDecode({ update: '.', todos: [] })
  assert.equal(result.ok, true, JSON.stringify(result.error ?? null))
  assert.equal(result.value.update, '.')
  assert.deepEqual(result.value.todos, [])
})

test('WHAT[cognitive-workspace-002] a missing update is refused', () => {
  const result = admission.tryDecode({ todos: [] })
  assert.equal(result.ok, false)
  assert.match(result.error, /requires update/)
})

test('WHAT[cognitive-workspace-002] a non-string update is refused', () => {
  const result = admission.tryDecode({ update: 1, todos: [] })
  assert.equal(result.ok, false)
  assert.match(result.error, /update must be a string/)
})

test('WHAT[cognitive-workspace-002] missing todos is refused, including an empty string', () => {
  assert.equal(admission.tryDecode({ update: '.' }).ok, false)
  assert.match(admission.tryDecode({ update: '.' }).error, /requires todos/)
})

test('WHAT[cognitive-workspace-002] a non-array todos is refused', () => {
  const result = admission.tryDecode({ update: '.', todos: 'nope' })
  assert.equal(result.ok, false)
  assert.match(result.error, /todos must be an array/)
})

test('WHAT[cognitive-workspace-002] every retired ledger argument is refused by name', () => {
  // The old protocol must not survive as a silently-ignored extra: a model still
  // carrying the habit has to be told, not have its read dropped on the floor.
  for (const name of ['query', 'planComplete', 'workingOn', 'obligations', 'horizon', 'revision', 'phase', 'commit']) {
    const result = admission.tryDecode({ update: '.', todos: [], [name]: name === 'query' ? '.' : true })
    assert.equal(result.ok, false, `${name} must be refused`)
    assert.match(result.error, new RegExp(`'${name}' is not part of this contract`))
  }
})

test('WHAT[cognitive-workspace-002] a blank content is refused and named by index', () => {
  const result = admission.tryDecode({
    update: '.',
    todos: [{ content: 'ok', status: 'pending' }, { content: '   ', status: 'pending' }],
  })
  assert.equal(result.ok, false)
  assert.match(result.error, /todos\[1\]\.content must not be empty/)
})

test('WHAT[cognitive-workspace-002] the status vocabulary is bounded and named by index', () => {
  const result = admission.tryDecode({
    update: '.',
    todos: [{ content: 'ok', status: 'started' }],
  })
  assert.equal(result.ok, false)
  assert.match(result.error, /todos\[0\]\.status must be one of pending, in_progress, completed, cancelled/)
  assert.match(result.error, /got 'started'/)
})

test('WHAT[cognitive-workspace-002] an omitted priority defaults to medium and is not an error', () => {
  const result = admission.tryDecode({ update: '.', todos: [{ content: 'ok', status: 'pending' }] })
  assert.equal(result.ok, true, JSON.stringify(result.error ?? null))
  assert.equal(result.value.todos[0].priority, 'medium')
})

test('WHAT[cognitive-workspace-002] the declaration order is preserved exactly', () => {
  // The sink contract is a full-list replacement, so the tool may not reorder, dedupe
  // or rename what the model declared — the user must see what was sent.
  const result = admission.tryDecode({
    update: '.',
    todos: [
      { content: 'third written first', status: 'pending' },
      { content: 'duplicate text', status: 'pending' },
      { content: 'duplicate text', status: 'completed' },
    ],
  })
  assert.equal(result.ok, true)
  assert.deepEqual(
    result.value.todos.map((row) => row.content),
    ['third written first', 'duplicate text', 'duplicate text'],
  )
  assert.deepEqual(
    result.value.todos.map((row) => row.status),
    ['pending', 'pending', 'completed'],
  )
})

test('WHAT[cognitive-workspace-002] the canvas and the declaration are one snapshot', () => {
  const snapshot = workspace.AssumeSnapshot_ofJson('{"decision":null}', [
    { content: 'ship it', status: 'pending', priority: 'high' },
  ])
  assert.equal(snapshot.canvasJson, '{"decision":null}')
  assert.equal(snapshot.todos.length, 1)
  assert.equal(snapshot.todos[0].content, 'ship it')
  // The declaration lives beside the canvas, not inside it: the model is free to
  // design the canvas without being forced to mirror the todo list inside it.
  assert.doesNotMatch(snapshot.canvasJson, /ship it/)
})

test('WHAT[cognitive-workspace-002] a root null survives the snapshot serialization', () => {
  const snapshot = workspace.AssumeSnapshot_ofJson('null', [])
  const json = JSON.parse(workspace.AssumeSnapshot_json(snapshot))
  assert.strictEqual(json.canvas, null)
})

test('WHAT[cognitive-workspace-002] a mixed array and a null-valued key survive serialization', () => {
  const snapshot = workspace.AssumeSnapshot_ofJson('{"items":[1,{"a":true},"x"],"note":null}', [])
  const json = JSON.parse(workspace.AssumeSnapshot_json(snapshot))
  assert.deepEqual(json.canvas, { items: [1, { a: true }, 'x'], note: null })
})

test('WHAT[cognitive-workspace-002] a fresh canvas is an empty object with no declaration', () => {
  const snapshot = workspace.AssumeSnapshot_empty()
  assert.equal(snapshot.canvasJson, '{}')
  assert.deepEqual(snapshot.todos, [])
})

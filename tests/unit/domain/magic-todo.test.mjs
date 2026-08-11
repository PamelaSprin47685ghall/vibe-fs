import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, errorResult, magicTodo, magicTodoAdmission, managerLifeId, toList, toolCallId } from '../support/domain.mjs'

const sha256 = (value) => `digest:${value}`
const values = (list) => Array.from(list)
const ok = (result) => {
  assert.equal(result.tag, 0, `expected Ok, got ${result.fields?.[0]?.cases?.()[result.fields?.[0]?.tag]}`)
  return result.fields[0]
}
const error = (result) => {
  assert.equal(result.tag, 1, 'expected Error')
  return result.fields[0]
}

const life = managerLifeId('manager-life')
const firstCall = toolCallId('first-call')
const secondCall = toolCallId('second-call')
const pending = magicTodo.TodoStatus.Pending
const inProgress = magicTodo.TodoStatus.InProgress
const reviewing = magicTodo.TodoStatus.Reviewing
const completed = magicTodo.TodoStatus.Completed
const cancelled = magicTodo.TodoStatus.Cancelled

const normalize = (old, call, input) =>
  magicTodo.normalizeProposed(sha256, life, call, toList(old), toList(input))

test('TODO-002 allocates replay-stable identities only for tagged new items', () => {
  const first = values(ok(normalize([], firstCall, [magicTodo.new('Implement bridge', pending, 'high')])))[0]
  const replay = values(ok(normalize([], firstCall, [magicTodo.new('Implement bridge', pending, 'high')])))[0]
  const next = values(ok(normalize([], secondCall, [magicTodo.new('Implement bridge', pending, 'high')])))[0]

  assert.equal(magicTodo.todoItemIdValue(first.Id), magicTodo.todoItemIdValue(replay.Id))
  assert.notEqual(magicTodo.todoItemIdValue(first.Id), magicTodo.todoItemIdValue(next.Id))
})

test('TODO-012 round-trips canonical settled todo bodies and rejects unknown statuses', () => {
  const item = magicTodo.item(magicTodo.todoItemIdCreate('persisted'), 'Persist checkpoint', reviewing, 'high')
  const encoded = magicTodo.encodeList([item])
  const decoded = magicTodo.decodeList(encoded)

  assert.equal(encoded, '[{"id":"persisted","content":"Persist checkpoint","status":"reviewing","priority":"high"}]')
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(values(decoded.value)[0].Content, 'Persist checkpoint')
  assert.equal(values(decoded.value)[0].Status, reviewing)

  const unknown = magicTodo.decodeList('[{"id":"persisted","content":"Persist checkpoint","status":"invented","priority":"high"}]')
  assert.equal(unknown.ok, false)
})

test('TODO-003 rejects direct completed transitions and completed new items', () => {
  const existingId = magicTodo.todoItemIdCreate('existing')
  const old = [magicTodo.item(existingId, 'Review implementation', inProgress, 'high')]
  const direct = error(normalize(old, firstCall, [magicTodo.existing(existingId, 'Review implementation', completed, 'high')]))
  const fresh = error(normalize([], firstCall, [magicTodo.new('Already done', completed, 'low')]))

  assert.equal(direct.cases()[direct.tag], 'IllegalCompletedTransition')
  assert.equal(fresh.cases()[fresh.tag], 'NewItemCompleted')
  assert.equal(magicTodo.validateCompletedGate(reviewing, completed), true)
  assert.equal(magicTodo.validateCompletedGate(inProgress, completed), false)
})

test('TODO-005 REVISE conservatively merges progress but PERFECT fully replaces', () => {
  const id = magicTodo.todoItemIdCreate('same-task')
  const old = [magicTodo.item(id, 'Old wording', inProgress, 'low')]
  const proposed = [magicTodo.item(id, 'New wording', completed, 'high')]

  const revised = values(magicTodo.settle(toList(old), toList(proposed), magicTodo.revise))[0]
  const perfect = values(magicTodo.settle(toList(old), toList(proposed), magicTodo.perfect))[0]

  assert.equal(revised.Content, 'New wording')
  assert.equal(revised.Priority, 'high')
  assert.equal(revised.Status, inProgress)
  assert.equal(perfect.Status, completed)
})

test('TODO-005 REVISE preserves unilateral cancellation and resurrection', () => {
  const id = magicTodo.todoItemIdCreate('disposition')
  const active = [magicTodo.item(id, 'Task', pending, 'medium')]
  const cancelledProposal = [magicTodo.item(id, 'Task', cancelled, 'medium')]
  const cancelledOld = [magicTodo.item(id, 'Task', cancelled, 'medium')]
  const activeProposal = [magicTodo.item(id, 'Task', reviewing, 'medium')]

  assert.equal(values(magicTodo.semanticMerge(toList(active), toList(cancelledProposal)))[0].Status, pending)
  assert.equal(values(magicTodo.semanticMerge(toList(cancelledOld), toList(activeProposal)))[0].Status, cancelled)
})

test('TODO-004 rejects different todowrite calls in one assistant message', () => {
  const rejected = error(magicTodo.admitTodowriteBatch(toList([firstCall, secondCall])))
  const replay = ok(magicTodo.admitTodowriteBatch(toList([firstCall, firstCall])))

  assert.equal(rejected.cases()[rejected.tag], 'MultipleTodowriteInMessage')
  assert.equal(replay, undefined)
})

test('TODO-004 rejects replay identity corruption', () => {
  const expected = new magicTodo.PreparedIdentity(life, 'provider-a', 'base-a', 3)
  const matching = new magicTodo.PreparedIdentity(life, 'provider-a', 'base-a', 3)
  const changed = new magicTodo.PreparedIdentity(life, 'provider-b', 'base-a', 3)

  assert.equal(ok(magicTodo.checkPreparedReplay(expected, matching)), undefined)
  const rejected = error(magicTodo.checkPreparedReplay(expected, changed))
  assert.equal(rejected.cases()[rejected.tag], 'IdentityCorruption')
  assert.equal(rejected.fields[0], 'ProviderInputDigest')
})

test('TODO-004 replays an identical prepared call even while its review is outstanding', () => {
  const write = magicTodo.todoWriteId(sha256, life, firstCall)
  const existing = new magicTodoAdmission.ExistingPrepared(
    new magicTodo.PreparedIdentity(life, 'provider-input', magicTodo.listDigest(sha256, toList([])), 1),
    write,
  )
  const localized = new magicTodoAdmission.LocalizedToolCall(
    firstCall,
    1,
    toList([firstCall]),
    { Sequence: 4n },
    'provider-input',
  )

  const outcome = magicTodoAdmission.admit(
    sha256,
    life,
    [],
    errorResult(undefined),
    existing,
    localized,
    [],
  )
  assert.equal(caseOf(outcome), 'IdempotentReplay')
})

test('TODO-014 blocks first unblessed suicide without an accepted checkpoint', () => {
  const missing = error(magicTodo.requireCheckpointBeforeFirstSuicide(0))
  assert.equal(missing.cases()[missing.tag], 'FirstSuicideWithoutCheckpoint')
  assert.equal(ok(magicTodo.requireCheckpointBeforeFirstSuicide(1)), undefined)
})

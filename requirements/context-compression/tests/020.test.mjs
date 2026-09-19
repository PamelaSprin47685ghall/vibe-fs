import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as magicTodo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

const floor = ({ hasOpenLife = true, planCommitted = false, xTraceHeadSequence = 0, legacyProtectedPrefixEnd, parts = [] } = {}) =>
  magicTodo.effectiveOpeningFloor(hasOpenLife, planCommitted, 1, null, null, xTraceHeadSequence, parts)

test('WHAT[context-compression-020] todowrite_material_does_not_redefine_the_owned_opening_floor', () => {
  const parts = [
    { sequence: 8, kind: 'tool_call', toolCallId: 'todo-call-1' },
    { sequence: 9, kind: 'tool_result', toolCallId: 'todo-call-1' },
  ]

  assert.equal(Number(floor({ xTraceHeadSequence: 20, parts })), 2)
})

test('WHAT[context-compression-020] todowrite call and matching result are retained across a Y cutoff', () => {
  assert.deepEqual(
    prefix.retainTodoWriteRounds([
      { containsTodoWrite: false, callIds: [] },
      { containsTodoWrite: true, callIds: ['todo-call-1'] },
      { containsTodoWrite: false, callIds: ['todo-call-1'] },
      { containsTodoWrite: false, callIds: ['other-call'] },
    ]),
    [false, true, true, false],
    'only the todowrite round punches through an otherwise replaceable prefix',
  )
})

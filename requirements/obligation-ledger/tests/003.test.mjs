import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const root = new URL('../../../', import.meta.url).pathname

const read = (path) => readFileSync(join(root, path), 'utf8')

const firstCheckpointSurfaces = [
  ['planning-table/en', 'resources/provider/lifecycle/manager/planning-table/en.md'],
  ['planning-table/zh-CN', 'resources/provider/lifecycle/manager/planning-table/zh-CN.md'],
  ['todowrite-description/en', 'resources/provider/lifecycle/magic-todo/todowrite-description/en.md'],
  ['todowrite-description/zh-CN', 'resources/provider/lifecycle/magic-todo/todowrite-description/zh-CN.md'],
]

test('WHAT[OBLIGATION-LEDGER-003] clean break removes the legacy todo ontology from the production graph', () => {
  const algebra = read('src/Wanxiangshu/Mission/Obligation/Todo/Model.fs')
  // W5: the wrapper aggregate is gone; the compile-order manifest lists what
  // production actually ships.
  const order = read('src/Wanxiangshu/compile-order.txt')

  assert.doesNotMatch(
    algebra,
    /TodoStatus|TodoItemId|MagicTodoInputItem|MagicTodoItem|MagicTodoList|semanticMerge|RevisePreview/,
    'MagicTodo algebra must stay obligation-only',
  )
  assert.doesNotMatch(order, /MagicTodoListCodec|MagicTodoLegacySeed|MagicTodoSuicide/)
  assert.match(order, /ObligationCodec\.fs/)
})

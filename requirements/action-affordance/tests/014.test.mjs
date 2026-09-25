import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const LOCALES = ['en', 'zh-CN']

test('WHAT[action-affordance-014] AA_assume_contract_is_single_jq_update_and_complete_todo_declaration', () => {
  for (const locale of LOCALES) {
    const description = read(`resources/provider/tool/assume/description/${locale}.md`)
    const update = read(`resources/provider/tool/assume/arg-update/${locale}.md`)
    const todos = read(`resources/provider/tool/assume/arg-todos/${locale}.md`)

    assert.match(description, /唯一.*画板|one workspace|single canvas/is, 'one workspace')
    assert.match(description, /独享|互不可见|private to|other sessions|invisible/is, 'canvas is private to its own session')
    assert.match(description, /不清空|空画板|重启|not cleared|empty canvas|restart|survives/is, 'canvas survives session end and restart')
    assert.match(description, /update|todos/is, 'both arguments named')
    assert.match(description, /恰好一个|exactly one/i, 'update must produce exactly one value')
    assert.match(description, /完整|complete|whole/i, 'todos is the complete list')
    assert.match(description, /完整|complete|whole|回滚|unchanged|not roll back/is, 'failure changes nothing')
    assert.match(update, /jq/i, 'update identifies jq')
    assert.match(update, /当前画板|当前持久画板|current canvas|persistent canvas/i, 'update identifies the dot input')
    assert.match(todos, /pending|in_progress|completed|cancelled/, 'todos names the status vocabulary')
    assert.doesNotMatch(description, /\bquery\b/i, 'the retired query parameter is gone')
  }
})

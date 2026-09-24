import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const HIGH_RISK_TOOLS = Object.freeze([
  'commission',
  'establish-behavior',
  'fork',
  'inspect',
  'query-shell',
  'repair-behavior',
  'resume',
  'run',
])

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

test('WHAT[action-affordance-001] AA_assume_contract_answers_act_fit_boundary_return_and_argument', () => {
  for (const locale of LOCALES) {
    const description = readTool('assume', locale)
    const update = read(`resources/provider/tool/assume/arg-update/${locale}.md`)
    const todos = read(`resources/provider/tool/assume/arg-todos/${locale}.md`)

    assert.match(description, /持久.*JSON.*画板|persistent.*JSON.*canvas/is, 'act must be explicit')
    assert.match(description, /非线性|non-linear/i, 'fit must be explicit')
    assert.match(description, /没有.*预定义.*schema|no predefined schema/is, 'nearby ontology must not be imposed')
    assert.match(description, /update|todos/is, 'both arguments must be named')
    assert.match(description, /先抽象.*执行.*验证|abstract.*execute.*verify/is, 'old assume commitment contract must survive')
    assert.match(update, /jq/i, 'update must identify jq syntax')
    assert.match(update, /当前画板|当前持久画板|current canvas|persistent canvas/i, 'update must identify dot input semantics')
    assert.match(update, /恰好一个|exactly one/i, 'update must state the single-output rule')
    assert.match(todos, /pending|in_progress|completed|cancelled/, 'todos must name the status vocabulary')
  }
})

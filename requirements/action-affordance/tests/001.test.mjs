import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

test('WHAT[ACTION-AFFORDANCE-001] AA_assume_contract_answers_act_fit_boundary_return_and_argument', () => {
  for (const locale of LOCALES) {
    const description = readTool('assume', locale)
    const update = read(`resources/provider/tool/assume/arg-update/${locale}.md`)
    const query = read(`resources/provider/tool/assume/arg-query/${locale}.md`)

    assert.match(description, /持久.*JSON.*画板|persistent.*JSON.*canvas/is, 'act must be explicit')
    assert.match(description, /非线性|non-linear/i, 'fit must be explicit')
    assert.match(description, /没有.*预定义.*schema|no predefined schema/is, 'nearby ontology must not be imposed')
    assert.match(description, /先.*update.*后.*query|update.*first.*query/is, 'update-before-query order must be explicit')
    assert.match(description, /先抽象.*执行.*验证|abstract.*execute.*verify/is, 'old assume commitment contract must survive')
    assert.match(update, /jq/i, 'update must identify jq syntax')
    assert.match(update, /当前画板|current workspace/i, 'update must identify dot input semantics')
    assert.match(query, /更新后|updated workspace/i, 'query must identify post-update input semantics')
  }
})

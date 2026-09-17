import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'
import { maskFSharpTrivia } from '../../../scripts/lib/fsharp-source.mjs'

const ROOT = join(fileURLToPath(new URL('../../..', import.meta.url)))

test('WHAT[OBLIGATION-LEDGER-020] OBL_020_ledger_does_not_adjudicate_quality', () => {
  // 1. 行为面与参数契约断言：todowrite 工具线仅接收义务与规划完备性标志，不包含任何质量裁决参数
  const obligation = { name: 'task-1', horizon: 'near', work: 'implement feature' }
  const wire = todo.canonicalObligationListWire([obligation])

  // 导出的 wire 格式严格包含名称、粒度、工作内容，绝无质量、评分、及格等字段
  assert.match(wire, /"name":\s*"task-1"/)
  assert.match(wire, /"horizon":\s*"near"/)
  assert.match(wire, /"work":\s*"implement feature"/)
  assert.doesNotMatch(wire, /"quality"|"score"|"verdict"|"pass"|"acceptance"|"grade"/i)

  // 2. 生产源码结构断言：义务账本持久事实与投影模型中无质量终局裁决字段
  const targetSources = [
    'src/Wanxiangshu/Composition/Durable/MagicTodoFacts.fs',
    'src/Wanxiangshu/Composition/Durable/MagicTodoProjection.fs',
    'src/Wanxiangshu/Composition/Durable/MagicTodoIdentity.fs',
  ]

  const forbiddenQualityTerms = [
    /\bqualityScore\b/i,
    /\bfinalQualityReview\b/i,
    /\badjudicateQuality\b/i,
    /\bqualityVerdict\b/i,
    /\bpassOrFail\b/i,
  ]

  for (const relPath of targetSources) {
    const source = readFileSync(join(ROOT, relPath), 'utf8')
    const masked = maskFSharpTrivia(source)

    for (const pattern of forbiddenQualityTerms) {
      assert.doesNotMatch(
        masked,
        pattern,
        `Obligation ledger source ${relPath} must not contain quality adjudication field: ${pattern}`,
      )
    }
  }

  // 3. 架构依赖断言：obligation-ledger 领域分片严禁反向依赖 relay-assessment
  const todoFsproj = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Work/Mission/Obligation/Todo/Wanxiangshu.Owner.obligation-ledger.mission-obligation-todo-model.fsproj'),
    'utf8',
  )
  assert.doesNotMatch(
    todoFsproj,
    /relay-assessment/i,
    'obligation-ledger must not depend on relay-assessment (quality judgement belongs to independent assessment)',
  )
})

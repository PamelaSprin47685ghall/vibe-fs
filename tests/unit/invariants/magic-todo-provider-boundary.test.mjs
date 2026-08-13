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

test('TODO-002/TODO-015 first todowrite is a finished mission account, never a meta-plan placeholder', () => {
  for (const [label, path] of firstCheckpointSurfaces) {
    const text = read(path)
    assert.match(text, /first|第一次|首次/, `${label}: must identify the first todowrite boundary`)
    assert.match(text, /complete|finished|完整|完成/, `${label}: first call must be a completed account`)
    assert.match(
      text,
      /make a plan|plan of the plan|meta-|先做计划|计划的计划|meta-item|meta-obligation/i,
      `${label}: must reject planning-the-plan as an obligation`,
    )
  }
})

test('TODO-002 Manager Role Law rejects meta-work without owning tool timing', () => {
  for (const path of ['resources/provider/role/manager/en.md', 'resources/provider/role/manager/zh-CN.md']) {
    const text = read(path)
    assert.match(text, /make a plan|analyze the request|先做计划|分析请求/i)
    assert.match(text, /completion\s+counterfactual|完成反事实/i, `${path}: must distinguish cognition from mission debt semantically`)
    assert.match(text, /investigation|调查/i, `${path}: investigative verbs must not hide planning`)
    assert.match(text, /obligation|mission|债务/i)
    assert.doesNotMatch(text, /\btodowrite\b/i, `${path}: lifecycle/tool timing must not leak into Role Law`)
  }
})

test('TODO-002 disguised investigative meta-todo is explicitly rejected before first checkpoint', () => {
  const enPlanning = read('resources/provider/lifecycle/manager/planning-table/en.md')
  const zhPlanning = read('resources/provider/lifecycle/manager/planning-table/zh-CN.md')
  const enTool = read('resources/provider/lifecycle/magic-todo/todowrite-description/en.md')
  const zhTool = read('resources/provider/lifecycle/magic-todo/todowrite-description/zh-CN.md')

  for (const [label, text] of [
    ['planning-table/en', enPlanning],
    ['planning-table/zh-CN', zhPlanning],
    ['todowrite-description/en', enTool],
    ['todowrite-description/zh-CN', zhTool],
  ]) {
    assert.match(text, /completion\s+counterfactual|完成反事实/i, `${label}: must use outcome-based classification, not keywords`)
    assert.match(text, /survey-startup-and-complexity/i, `${label}: observed disguised meta-todo must be covered verbatim`)
    assert.match(text, /O\(N\^2\)|hotspot|热点/i, `${label}: repository investigation disguise must be covered`)
    assert.match(text, /deliverable|交付物/i, `${label}: exception must be tied to the user's actual deliverable`)
  }
})

test('TODO-005 provider wording says Accepted becomes Current without reviewer settlement', () => {
  for (const path of [
    'resources/provider/lifecycle/magic-todo/todowrite-description/en.md',
    'resources/provider/lifecycle/magic-todo/todowrite-description/zh-CN.md',
    'resources/provider/lifecycle/magic-todo/manager-guideline/en.md',
    'resources/provider/lifecycle/magic-todo/manager-guideline/zh-CN.md',
  ]) {
    const text = read(path)
    assert.match(text, /accepted|接受/, `${path}: must name the accepted boundary`)
    assert.match(text, /current|当前/, `${path}: accepted account must become current immediately`)
    assert.doesNotMatch(text, /semanticMerge|revise preview|REVISE preview|settled list/i)
  }
})

test('TODO-004 failure triage keeps red for syntax and kills OpenCode on infrastructure faults', () => {
  const membrane = read('src/Wanxiangshu/Application/Reconciliation/MagicTodoMembrane.fs')
  const hostCodec = read('src/Wanxiangshu/Infrastructure/OpenCode/Codec/MagicTodoHostCodec.fs')

  assert.match(membrane, /Diagnostic\.fatal "magic-todo-infrastructure-failed"/)
  assert.match(membrane, /\| Error reason -> invalidOp reason/, 'schema decode is allowed to reject the tool call')
  assert.match(membrane, /\| Error syntaxReason -> invalidOp syntaxReason/, 'deferred Error is syntax-only')
  assert.match(membrane, /await ConsumableReview failed:[\s\S]*fatalInfrastructure/)
  assert.match(membrane, /ensureReview infrastructure failed:/)
  assert.doesNotMatch(membrane, /Magic Todo deferred prepare failed/)
  assert.doesNotMatch(membrane, /ensureReview failed:[^\n]*invalidOp/)
  assert.match(hostCodec, /output\.args is required[\s\S]*Diagnostic\.fatal|Diagnostic\.fatal[\s\S]*output\.args is required/)
})

test('TODO-003 clean break removes the legacy todo ontology from the production graph', () => {
  const algebra = read('src/Wanxiangshu/Domain/MagicTodo.fs')
  const project = read('src/Wanxiangshu/Wanxiangshu.fsproj')

  assert.doesNotMatch(
    algebra,
    /TodoStatus|TodoItemId|MagicTodoInputItem|MagicTodoItem|MagicTodoList|semanticMerge|RevisePreview/,
    'MagicTodo algebra must stay obligation-only',
  )
  assert.doesNotMatch(project, /MagicTodoListCodec|MagicTodoLegacySeed|MagicTodoSuicide/)
  assert.match(project, /MagicTodoObligationCodec\.fs/)
})

test('TODO-005 production checkpoint path has no reviewer settlement owner', () => {
  for (const path of [
    'src/Wanxiangshu/Application/Reconciliation/MagicTodoMembrane.fs',
    'src/Wanxiangshu/Application/Review/TodoProcessReviewProgram.fs',
    'src/Wanxiangshu/Journal/MagicTodoProjection.fs',
  ]) {
    const text = read(path)
    assert.doesNotMatch(text, /semanticMerge|SettledCurrentRef|RevisePreview/, path)
  }

  const projection = read('src/Wanxiangshu/Journal/MagicTodoProjection.fs')
  assert.match(
    projection,
    /CurrentObligationsRef\s*=\s*Some\(cp\.ProposedTodoRef, cp\.ProposedTodoDigest\)/,
    'TodoWriteAccepted fold must be the CurrentObligations writer',
  )
})

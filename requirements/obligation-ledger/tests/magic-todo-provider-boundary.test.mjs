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

test('WHAT[OBLIGATION-LEDGER-016] planning checkpoints are allowed until the first irreversible true commitment', () => {
  for (const [label, path] of firstCheckpointSurfaces) {
    const text = read(path)
    assert.match(text, /planComplete/i, `${label}: must expose the explicit commitment declaration`)
    assert.match(text, /false/i, `${label}: must permit planning checkpoints before commitment`)
    assert.match(text, /true/i, `${label}: must explain the commitment declaration`)
    assert.match(text, /irreversible|不可逆|forever|永久|cannot.*return|不能.*回退/i, `${label}: first accepted true must be one-way`)
    assert.match(text, /planning|计划|规划/i, `${label}: false checkpoints must be allowed to carry planning work`)
  }
})

test('WHAT[OBLIGATION-LEDGER-004] Manager Role Law distinguishes planning relation from entrusted mission without owning tool timing', () => {
  for (const path of ['resources/provider/role/manager/en.md', 'resources/provider/role/manager/zh-CN.md']) {
    const text = read(path)
    assert.match(text, /Planning Table|规划桌/i)
    assert.match(text, /Entrusted Road|受托之路/i)
    assert.match(text, /planning|计划|规划/i)
    assert.match(text, /mission|obligation|使命|义务/i)
    assert.doesNotMatch(text, /\btodowrite\b|planComplete/i, `${path}: lifecycle/tool timing must not leak into Role Law`)
  }
})

test('WHAT[OBLIGATION-LEDGER-005] empty placeholders remain invalid while concrete planning work is legal before commitment', () => {
  const surfaces = [
    ...firstCheckpointSurfaces,
    ['obligation-name/en', 'resources/provider/lifecycle/magic-todo/obligation-name-description/en.md'],
    ['obligation-name/zh-CN', 'resources/provider/lifecycle/magic-todo/obligation-name-description/zh-CN.md'],
    ['obligation-work/en', 'resources/provider/lifecycle/magic-todo/obligation-work-description/en.md'],
    ['obligation-work/zh-CN', 'resources/provider/lifecycle/magic-todo/obligation-work-description/zh-CN.md'],
  ]

  for (const [label, path] of surfaces) {
    const text = read(path)
    assert.match(text, /handoff|可托付|close|闭环/i, `${label}: must require an obligation to carry concrete closable work`)
    assert.match(text, /placeholder|占位/i, `${label}: must reject slot-reserving entries`)
    assert.match(text, /TBD|deferred|延后|推迟/i, `${label}: must reject deferred substance`)
  }

  const host = read('src/Wanxiangshu/Mission/Obligation/Todo/OpenCode/HostCodec.fs')
  assert.doesNotMatch(host, /placeholder:\s*planning|\bTBD\b/, 'Host must not classify natural-language placeholder keywords')
})

test('WHAT[OBLIGATION-LEDGER-004] committed mode rejects planning-only debt by consequence, not keywords', () => {
  for (const [label, path] of firstCheckpointSurfaces) {
    const text = read(path)
    assert.match(text, /planComplete/i)
    assert.match(text, /completion\s+counterfactual|完成反事实/i, `${label}: committed mode must classify by consequence`)
    assert.match(text, /true/i)
    assert.match(text, /mission|用户|deliverable|交付物/i)
  }

  const host = read('src/Wanxiangshu/Mission/Obligation/Todo/OpenCode/HostCodec.fs')
  assert.doesNotMatch(host, /survey-startup-and-complexity|O\(N\^2\)|placeholder:\s*planning/, 'Host must not classify planning language')
})

test('WHAT[OBLIGATION-LEDGER-012] process reviewer is told the effective planning-vs-mission relation', () => {
  for (const path of [
    'resources/provider/lifecycle/magic-todo/process-reviewer-preamble/en.md',
    'resources/provider/lifecycle/magic-todo/process-reviewer-preamble/zh-CN.md',
  ]) {
    const text = read(path)
    assert.match(text, /planComplete|plan complete|计划.*完备|计划.*完整/i, `${path}: reviewer must understand the commitment relation`)
    assert.match(text, /false/i, `${path}: reviewer must allow planning-account review before commitment`)
    assert.match(text, /true/i, `${path}: reviewer must switch to mission-debt review after commitment`)
  }

  const request = read('src/Wanxiangshu/Mission/Obligation/Todo/ProcessReview.fs')
  assert.match(request, /EffectivePlanComplete/, 'typed process-review request must carry the effective relation')
})

test('WHAT[OBLIGATION-LEDGER-010] provider wording says Accepted becomes Current without reviewer settlement', () => {
  for (const path of [
    'resources/provider/lifecycle/magic-todo/todowrite-description/en.md',
    'resources/provider/lifecycle/magic-todo/todowrite-description/zh-CN.md',
  ]) {
    const text = read(path)
    assert.match(text, /accepted|接受/, `${path}: must name the accepted boundary`)
    assert.match(text, /current|当前/, `${path}: accepted account must become current immediately`)
    assert.doesNotMatch(text, /semanticMerge|revise preview|REVISE preview|settled list/i)
  }
})

test('WHAT[OBLIGATION-LEDGER-023] manager guideline freezes ledger discipline as Manager-only content', () => {
  for (const path of [
    'resources/provider/lifecycle/magic-todo/manager-guideline/en.md',
    'resources/provider/lifecycle/magic-todo/manager-guideline/zh-CN.md',
  ]) {
    const text = read(path)
    // keep while owed / remove when earned
    assert.match(text, /keep|retain|保留|继续保留/, `${path}: must keep obligations while owed`)
    assert.match(text, /remove|discharge|earned|移除|解除|earned|真正解除/, `${path}: must remove only when earned`)
    // checkpoint continuity (lag-1) and no forged Activation (conversation relation, not persisted phase)
    assert.match(text, /accepted account becomes Current|accepted account 都立即成为当前|Current/i, `${path}: accepted supersedes without reviewer settlement`)
    assert.doesNotMatch(text, /Activation|WorkActivated/i, `${path}: must not forge Activation as a persisted phase`)
  }
})

test('WHAT[OBLIGATION-LEDGER-009] failure triage keeps red for syntax and kills OpenCode on infrastructure faults', () => {
  const membrane = read('src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembrane.fs')
  const hostCodec = read('src/Wanxiangshu/Mission/Obligation/Todo/OpenCode/HostCodec.fs')

  assert.match(membrane, /Diagnostic\.fatal "magic-todo-infrastructure-failed"/)
  assert.match(membrane, /\| Error reason -> invalidOp reason/, 'schema decode is allowed to reject the tool call')
  assert.match(membrane, /\| Error syntaxReason -> invalidOp syntaxReason/, 'deferred Error is syntax-only')
  assert.match(membrane, /await ConsumableReview failed:[\s\S]*fatalInfrastructure/)
  assert.match(membrane, /ensureReview infrastructure failed:/)
  assert.doesNotMatch(membrane, /Magic Todo deferred prepare failed/)
  assert.doesNotMatch(membrane, /ensureReview failed:[^\n]*invalidOp/)
  assert.match(hostCodec, /output\.args is required[\s\S]*Diagnostic\.fatal|Diagnostic\.fatal[\s\S]*output\.args is required/)
})

test('WHAT[OBLIGATION-LEDGER-003] clean break removes the legacy todo ontology from the production graph', () => {
  const algebra = read('src/Wanxiangshu/Mission/Obligation/Todo/Model.fs')
  const project = read('src/Wanxiangshu/Wanxiangshu.fsproj')

  assert.doesNotMatch(
    algebra,
    /TodoStatus|TodoItemId|MagicTodoInputItem|MagicTodoItem|MagicTodoList|semanticMerge|RevisePreview/,
    'MagicTodo algebra must stay obligation-only',
  )
  assert.doesNotMatch(project, /MagicTodoListCodec|MagicTodoLegacySeed|MagicTodoSuicide/)
  assert.match(project, /ObligationCodec\.fs/)
})

test('WHAT[OBLIGATION-LEDGER-011] production checkpoint path has no reviewer settlement owner', () => {
  for (const path of [
    'src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembrane.fs',
    'src/Wanxiangshu/Mission/Review/TodoProcess.fs',
    'src/Wanxiangshu/Mission/Obligation/Todo/Projection.fs',
  ]) {
    const text = read(path)
    assert.doesNotMatch(text, /semanticMerge|SettledCurrentRef|RevisePreview/, path)
  }

  const projection = read('src/Wanxiangshu/Mission/Obligation/Todo/Projection.fs')
  assert.match(
    projection,
    /CurrentObligationsRef\s*=\s*Some\(cp\.ProposedTodoRef, cp\.ProposedTodoDigest\)/,
    'TodoWriteAccepted fold must be the CurrentObligations writer',
  )
})

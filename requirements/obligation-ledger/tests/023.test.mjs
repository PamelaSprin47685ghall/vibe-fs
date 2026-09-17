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

test('WHAT[OBLIGATION-LEDGER-023] manager guideline freezes ledger discipline as Manager-only content', () => {
  for (const path of [
    'resources/provider/lifecycle/magic-todo/manager-guideline/en.md',
    'resources/provider/lifecycle/magic-todo/manager-guideline/zh-CN.md',
  ]) {
    const text = read(path)
    // keep while owed / remove when earned
    assert.match(text, /keep|retain|保留|继续保留/, `${path}: must keep obligations while owed`)
    assert.match(text, /remove|discharge|earned|移除|解除|earned|真正解除/, `${path}: must remove only when earned`)
    assert.match(text, /workingOn/i, `${path}: must name the active focus pointer`)
    assert.match(text, /focus|焦点|actively advancing|实际正在推进/i, `${path}: must require workingOn to follow actual work`)
    assert.match(text, /near/i, `${path}: current frontier must be near`)
    assert.match(text, /mid/i, `${path}: next outcomes must stay mid-grained`)
    assert.match(text, /far/i, `${path}: distant outcomes must stay coarse`)
    assert.match(text, /coverage|覆盖/i, `${path}: complete plans must preserve full coverage`)
    assert.match(text, /uniform|均匀/i, `${path}: complete plans must not require uniform decomposition`)
    // checkpoint continuity (lag-1) and no forged Activation (conversation relation, not persisted phase)
    assert.match(text, /accepted account becomes Current|accepted account 都立即成为当前|Current/i, `${path}: accepted supersedes without reviewer settlement`)
    assert.doesNotMatch(text, /Activation|WorkActivated/i, `${path}: must not forge Activation as a persisted phase`)
  }
})

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

test('WHAT[obligation-ledger-004] Manager Role Law distinguishes planning relation from entrusted mission without owning tool timing', () => {
  for (const path of ['resources/provider/role/manager/en.md', 'resources/provider/role/manager/zh-CN.md']) {
    const text = read(path)
    assert.match(text, /Planning Table|规划桌/i)
    assert.match(text, /Entrusted Road|受托之路/i)
    assert.match(text, /planning|计划|规划/i)
    assert.match(text, /mission|obligation|使命|义务/i)
    assert.match(text, /counterfactual|反事实/i, `${path}: must distinguish mapping cognition from mission debt by consequence`)
    assert.doesNotMatch(text, /\btodowrite\b|planComplete/i, `${path}: lifecycle/tool timing must not leak into Role Law`)
  }
})

test('WHAT[obligation-ledger-004] committed mode rejects planning-only debt by consequence, not keywords', () => {
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

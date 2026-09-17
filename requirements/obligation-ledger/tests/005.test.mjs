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

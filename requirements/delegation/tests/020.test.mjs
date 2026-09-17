import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('../../..', import.meta.url)))

const providerRoot = join(ROOT, 'resources', 'provider')

const readProviderPair = (relative) => {
  const base = join(providerRoot, relative)
  return { en: readFileSync(join(base, 'en.md'), 'utf8'), zh: readFileSync(join(base, 'zh-CN.md'), 'utf8') }
}

const assertAnchorHit = (pair, anchor, id) => {
  assert.match(pair.en, anchor.en, `${id}: en`)
  assert.match(pair.zh, anchor.zh, `${id}: zh`)
}

const syncModel = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Model.fs'), 'utf8')

const forkTool = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs'), 'utf8')

test('WHAT[DELEG-020] delegation_semantics_do_not_depend_on_current_tool_names', () => {
  const how = readFileSync(new URL('../HOW.md', import.meta.url), 'utf8')
  assert.match(how, /工具名/)
  assert.match(how, /DELEG-020/)
  assert.match(how, /改名不动 WHAT/)
  for (const name of ['fork', 'commission', 'inspect', 'establish-behavior', 'repair-behavior']) assert.ok(how.includes(name))
})

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

test('WHAT[delegation-001] manager_role_law_entrusts_by_consequence_not_persona', () => {
  const pair = readProviderPair('role/manager')
  assert.match(pair.en, /entrust.*consequence/i)
  assert.match(pair.zh, /托付|consequence/i)
})

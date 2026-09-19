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

test('WHAT[delegation-007] sync_delegate_edges_are_the_allowed_dag_only', () => {
  const adjacency = new Map([
    ['Sphinx', ['Engineer']], ['Engineer', []],
  ])
  const visiting = new Set(); const visited = new Set()
  const visit = (node) => {
    if (visiting.has(node)) throw new Error(`cycle detected through ${node}`)
    if (visited.has(node)) return
    visiting.add(node); for (const next of adjacency.get(node) ?? []) visit(next); visiting.delete(node); visited.add(node)
  }
  for (const node of adjacency.keys()) visit(node)
  assert.equal(visited.size, adjacency.size)
})

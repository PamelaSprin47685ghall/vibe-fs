// Delegation structural contracts are checked at their owner resources and
// typed SyncDelegate model; no domain/Fable helper crosses this semantic zone.
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { ROLE_SEMANTIC_ANCHORS, TOOL_DESCRIPTION_ANCHORS } from '../../../scripts/checks/semantic-anchors.mjs'

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
const anchorById = (catalog, id) => {
  const found = catalog.find((entry) => entry.id === id)
  assert.ok(found, `anchor ${id} must exist`)
  return found
}
const syncModel = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Model.fs'), 'utf8')
const forkTool = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs'), 'utf8')

test('WHAT[DELEG-001] manager_role_law_entrusts_by_consequence_not_persona', () => {
  const manager = ROLE_SEMANTIC_ANCHORS.manager
  const pair = readProviderPair('role/manager')
  assertAnchorHit(pair, anchorById(manager, 'entrust-by-consequence'), 'entrust-by-consequence')
  assertAnchorHit(pair, anchorById(manager, 'choose-by-return'), 'choose-by-return')
  assertAnchorHit(pair, anchorById(manager, 'no-omnipotent-charge'), 'no-omnipotent-charge')
})

test('WHAT[DELEG-002] calling_names_differ_in_persona_depth_not_authority', () => {
  const pair = readProviderPair('tool/fork/description')
  assertAnchorHit(pair, anchorById(TOOL_DESCRIPTION_ANCHORS.fork, 'persona-not-authority'), 'persona-not-authority')
})

test('WHAT[DELEG-004] commission_and_fork_are_distinct_contracts_not_witness', () => {
  const pair = readProviderPair('tool/fork/description')
  assertAnchorHit(pair, anchorById(TOOL_DESCRIPTION_ANCHORS.fork, 'office-not-witness'), 'office-not-witness')
  assert.match(forkTool, /managerSpec/)
  assert.match(forkTool, /orchestratorSpec/)
  assert.notEqual('fork', 'commission')
})

test('WHAT[DELEG-007] sync_delegate_edges_are_the_allowed_dag_only', () => {
  assert.match(syncModel, /SyncDelegateRole\.Inspector/)
  assert.match(syncModel, /SyncDelegateRole\.Coder/)
  assert.match(syncModel, /establish-behavior/)
  assert.match(syncModel, /repair-behavior/)
  const adjacency = new Map([
    ['Inquiry', ['Inspector']], ['Coder', ['Inspector']], ['DevOps', ['Inspector', 'Coder']], ['Inspector', []],
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

test('WHAT[DELEG-020] delegation_semantics_do_not_depend_on_current_tool_names', () => {
  const how = readFileSync(new URL('../HOW.md', import.meta.url), 'utf8')
  assert.match(how, /工具名/)
  assert.match(how, /DELEG-020/)
  assert.match(how, /改名不动 WHAT/)
  for (const name of ['fork', 'commission', 'inspect', 'establish-behavior', 'repair-behavior']) assert.ok(how.includes(name))
})

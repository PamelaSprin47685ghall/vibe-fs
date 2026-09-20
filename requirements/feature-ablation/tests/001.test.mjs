import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const nodesDoc = JSON.parse(read('resources/ablation/nodes.json'))
const toolMap = JSON.parse(read('resources/ablation/tool-map.json'))
const factMap = JSON.parse(read('resources/ablation/fact-map.json'))

const staticTools = read('src/Wanxiangshu/OpenCode/Tools/StaticTools.fs')
const knownMatch = staticTools.match(/let knownToolNames =\s*\[([\s\S]*?)\]/)
const knownTools = knownMatch ? [...knownMatch[1].matchAll(/"([^"]+)"/g)].map((match) => match[1]) : []

const packageDirs = () =>
  readdirSync(join(ROOT, 'requirements'), { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .filter((name) => {
      try {
        read(`requirements/${name}/WHAT.md`)
        return true
      } catch {
        return false
      }
    })

test('WHAT[feature-ablation-001] ABL_001_primary_nodes_cover_index_packages', () => {
  const primary = nodesDoc.nodes.filter((node) => node.kind === 'package').map((node) => node.id)
  const packages = packageDirs()
  assert.deepEqual(new Set(primary), new Set(packages))
})

test('WHAT[feature-ablation-001] ABL_001_slice_nodes_declare_valid_parent_package', () => {
  const nodeMap = new Map(nodesDoc.nodes.map((n) => [n.id, n]))
  const slices = nodesDoc.nodes.filter((n) => n.kind === 'slice')
  assert.ok(slices.length > 0, 'must have slice nodes')

  for (const slice of slices) {
    assert.ok(slice.parent, `slice ${slice.id} must declare parent`)
    const parentNode = nodeMap.get(slice.parent)
    assert.ok(parentNode, `slice ${slice.id} parent ${slice.parent} must exist`)
    assert.equal(parentNode.kind, 'package', `slice ${slice.id} parent must be package`)
    assert.equal(slice.package, parentNode.package, `slice ${slice.id} and parent must share package`)
  }

  // Edge list must contain zero parent edges
  const parentEdges = (nodesDoc.edges || []).filter((e) => e.kind === 'parent')
  assert.equal(parentEdges.length, 0, 'parent edges must be deleted from edges list')
})

test('WHAT[feature-ablation-001] ABL_001_every_known_tool_maps_to_a_manifest_node', () => {
  for (const tool of knownTools) {
    assert.ok(toolMap.tools[tool], `missing tool-map entry for ${tool}`)
    const node = toolMap.tools[tool]
    assert.ok(nodesDoc.nodes.some((entry) => entry.id === node), `unknown node ${node} for ${tool}`)
  }
})

test('WHAT[feature-ablation-001] ABL_001_tool_map_has_no_stale_entries', () => {
  for (const tool of Object.keys(toolMap.tools)) {
    assert.ok(knownTools.includes(tool), `stale tool-map entry ${tool}`)
  }
})

test('WHAT[feature-ablation-001] ABL_001_tool_map_and_fact_map_sync_with_active_roles_and_manifest_nodes', () => {
  const tools = toolMap.tools || {}

  // 1. 新 6 角色 js-* 工具完整映射断言
  const activeRoleTools = {
    'js-engineer': 'repository-programming',
    'js-devops': 'process-execution',
    'js-manager': 'relay-incumbency',
    'js-orchestrator': 'change-integration',
    'js-blogger': 'context-compression',
    'js-bookkeeper': 'knowledge-reuse',
  }

  for (const [toolName, expectedNode] of Object.entries(activeRoleTools)) {
    assert.equal(
      tools[toolName],
      expectedNode,
      `active tool ${toolName} must map to node ${expectedNode}`,
    )
  }

  // 2. 废弃角色与工具零残留断言
  const forbiddenTools = [
    'browser',
    'distill',
    'query-shell',
    'js-coder',
    'js-inspector',
    'js-browser',
  ]

  for (const forbidden of forbiddenTools) {
    assert.equal(
      tools[forbidden],
      undefined,
      `deprecated tool ${forbidden} must be excluded from tool-map.json`,
    )
  }

  // 3. Sphinx 工具映射唯一归属断言
  assert.equal(
    tools['sphinx_*'],
    'epistemic-reasoning',
    'sphinx_* must map uniquely to epistemic-reasoning node',
  )

  // 4. Fact Map 完整性与活跃角色架构同步
  const facts = factMap.facts || {}

  assert.equal(facts['AgentFact.Fission'], 'intra-participant-parallelism')
  assert.equal(facts['AgentFact.Orchestrator'], 'change-integration')
  assert.equal(facts['AgentFact.Relay'], 'relay-incumbency')
  assert.equal(facts['AgentFact.Execution'], 'delegation')
  assert.equal(facts['MagicTodo'], 'obligation-ledger')

  // 5. 确保 fact-map 中所有节点在 nodes.json 中合法存在
  const nodeIds = new Set(nodesDoc.nodes.map((n) => n.id))

  for (const [factTag, nodeName] of Object.entries(facts)) {
    assert.ok(
      nodeIds.has(nodeName),
      `fact ${factTag} maps to node ${nodeName} which must exist in nodes.json`,
    )
  }
})

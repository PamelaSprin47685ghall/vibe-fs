import assert from 'node:assert/strict'
import { readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import * as Ablation from '../../../dist/Ablation/Surface.js'

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

test('WHAT[feature-ablation-001] ABL_001_child_nodes_declare_valid_parent_package', () => {
  const nodeMap = new Map(nodesDoc.nodes.map((n) => [n.id, n]))
  const childNodes = nodesDoc.nodes.filter((n) => n.kind === 'slice')
  assert.ok(childNodes.length > 0, 'must have child nodes')

  for (const childNode of childNodes) {
    assert.ok(childNode.parent, `child node ${childNode.id} must declare parent`)
    const parentNode = nodeMap.get(childNode.parent)
    assert.ok(parentNode, `child node ${childNode.id} parent ${childNode.parent} must exist`)
    assert.equal(parentNode.kind, 'package', `child node ${childNode.id} parent must be package`)
    assert.equal(childNode.package, parentNode.package, `child node ${childNode.id} and parent must share package`)
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

test('WHAT[feature-ablation-001] ABL_001_primary_node_uniqueness_and_presence_fail_closed', () => {
  const nodesPath = join(ROOT, 'resources/ablation/nodes.json')
  const original = readFileSync(nodesPath, 'utf8')

  const withNodesDoc = (modifier, run) => {
    try {
      const doc = JSON.parse(original)
      modifier(doc)
      writeFileSync(nodesPath, JSON.stringify(doc, null, 2), 'utf8')
      run()
    } finally {
      writeFileSync(nodesPath, original, 'utf8')
      Ablation.load()
    }
  }

  // 1. 缺失主节点：当包缺失同名主节点时，load 必须 fail-closed 返回 InvalidManifest
  withNodesDoc(
    (doc) => {
      const target = doc.nodes.find((n) => n.id === 'feature-ablation')
      target.id = 'feature-ablation-custom'
      target.kind = 'custom'
    },
    () => {
      const result = Ablation.load()
      assert.equal(result.ok, false, 'manifest loading must fail when primary node is missing')
      assert.equal(result.kind, 'InvalidManifest')
      assert.match(result.error, /missing a main node/i)
      assert.deepEqual(Ablation.manifestNodeIds(), [])
    },
  )

  // 2. 多个主节点：同一包出现多个主节点时，load 必须 fail-closed 返回 InvalidManifest
  withNodesDoc(
    (doc) => {
      doc.nodes.push({
        id: 'feature-ablation-extra',
        package: 'feature-ablation',
        station: 0,
        kind: 'package',
        borrowed_surface: [],
      })
    },
    () => {
      const result = Ablation.load()
      assert.equal(result.ok, false, 'manifest loading must fail when duplicate primary nodes exist')
      assert.equal(result.kind, 'InvalidManifest')
      assert.match(result.error, /multiple main nodes/i)
      assert.deepEqual(Ablation.manifestNodeIds(), [])
    },
  )
})

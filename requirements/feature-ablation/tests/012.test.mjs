import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[feature-ablation-012] ablation tool map syncs with active roles and excludes deprecated browser/distiller tools', () => {
  const toolMap = JSON.parse(read('resources/ablation/tool-map.json'))
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
  const factMap = JSON.parse(read('resources/ablation/fact-map.json'))
  const facts = factMap.facts || {}

  assert.equal(facts['AgentFact.Fission'], 'intra-participant-parallelism')
  assert.equal(facts['AgentFact.Orchestrator'], 'change-integration')
  assert.equal(facts['AgentFact.Relay'], 'relay-incumbency')
  assert.equal(facts['AgentFact.Execution'], 'delegation')
  assert.equal(facts['MagicTodo'], 'obligation-ledger')

  // 5. 确保 fact-map 中所有节点在 nodes.json 中合法存在
  const nodes = JSON.parse(read('resources/ablation/nodes.json'))
  const nodeIds = new Set(nodes.nodes.map((n) => n.id))

  for (const [factTag, nodeName] of Object.entries(facts)) {
    assert.ok(
      nodeIds.has(nodeName),
      `fact ${factTag} maps to node ${nodeName} which must exist in nodes.json`,
    )
  }
})

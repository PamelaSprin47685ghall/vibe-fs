import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[ABL-010] primary agents and Sphinx MCP are correctly gated by ablation state and decoupled from browser removal', async () => {
  const Ablation = await import('../../../dist/Ablation/Surface.js')
  const SphinxMcpConfigSurface = await import('../../../dist/OpenCode/Host/SphinxMcpConfigSurface.js')

  const withEnv = (entries, run) => {
    const previous = Object.fromEntries(entries.map(([name]) => [name, process.env[name]]))
    try {
      for (const [name, value] of entries) {
        if (value === undefined) delete process.env[name]
        else process.env[name] = value
      }
      run()
    } finally {
      for (const [name, value] of Object.entries(previous)) {
        if (value === undefined) delete process.env[name]
        else process.env[name] = value
      }
    }
  }

  // 1. station-05 下 relay-incumbency 与 change-integration 为 ablated:
  // manager 与 orchestrator 必须被拒绝 (allowsPrimaryAgent === false)
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-05']], () => {
    Ablation.load()
    assert.equal(
      Ablation.allowsPrimaryAgent('manager'),
      false,
      'station-05 must reject manager when relay-incumbency is ablated',
    )
    assert.equal(
      Ablation.allowsPrimaryAgent('orchestrator'),
      false,
      'station-05 must reject orchestrator when change-integration is ablated',
    )
    assert.equal(Ablation.allowsPrimaryAgent('browser'), false)
    assert.equal(Ablation.allowsPrimaryAgent('inquiry'), false)
  })

  // 2. production profile 下两者均为 active:
  // manager 与 orchestrator 必须允许 (allowsPrimaryAgent === true)
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'production']], () => {
    Ablation.load()
    assert.equal(
      Ablation.allowsPrimaryAgent('manager'),
      true,
      'production profile must allow manager primary agent',
    )
    assert.equal(
      Ablation.allowsPrimaryAgent('orchestrator'),
      true,
      'production profile must allow orchestrator primary agent',
    )
  })

  // 3. epistemic-reasoning 为 ablated 时，Sphinx MCP 必须处于 Disabled
  withEnv(
    [
      ['WANXIANGSHU_ABLATION_PROFILE', 'station-41'],
      ['WANXIANGSHU_ABLATION_epistemic_reasoning', 'ablated'],
    ],
    () => {
      Ablation.load()
      const envReader = (k) => (process.env[k] !== undefined ? process.env[k] : undefined)
      const decision = SphinxMcpConfigSurface.launchDecision(envReader)
      assert.equal(
        decision.kind,
        'disabled',
        'Sphinx MCP must be Disabled when epistemic-reasoning is ablated',
      )
      assert.equal(
        decision.enabled,
        false,
        'Sphinx MCP must be Disabled when epistemic-reasoning is ablated',
      )
    },
  )

  // 4. epistemic-reasoning 为 active 时，Sphinx 开关独立运作，绝不因 external-investigation/Browser 撤销被连带关闭
  withEnv(
    [
      ['WANXIANGSHU_ABLATION_PROFILE', 'production'],
      ['WANXIANGSHU_ABLATION_external_investigation', 'ablated'],
      ['WANXIANGSHU_ABLATION_epistemic_reasoning', 'active'],
      ['SPHINX_MCP_DISABLED', '0'],
      ['WANXIANGSHU_TEST', '0'],
    ],
    () => {
      Ablation.load()
      const envReader = (k) => (process.env[k] !== undefined ? process.env[k] : undefined)
      const decision = SphinxMcpConfigSurface.launchDecision(envReader)
      assert.notEqual(
        decision.kind,
        'disabled',
        'Sphinx MCP must remain active when epistemic-reasoning is active, independent of external-investigation',
      )
      assert.equal(
        decision.enabled,
        true,
        'Sphinx MCP must remain active when epistemic-reasoning is active, independent of external-investigation',
      )
    },
  )

  // 5. 静态 DAG 校验：epistemic-reasoning 节点不得存在指向已撤销 external-investigation 的依赖边
  const nodes = JSON.parse(read('resources/ablation/nodes.json'))
  const sphinxNode = nodes.nodes.find((n) => n.id === 'epistemic-reasoning')
  assert.ok(sphinxNode, 'epistemic-reasoning node must exist in ablation nodes')

  const edges = nodes.edges || []
  const hasBrowserDep = edges.some(
    (e) => e.to === 'epistemic-reasoning' && e.from === 'external-investigation',
  )
  assert.equal(hasBrowserDep, false, 'epistemic-reasoning must not depend on external-investigation')
})

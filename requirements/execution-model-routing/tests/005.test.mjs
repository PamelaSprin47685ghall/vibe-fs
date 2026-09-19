import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFile } = await import("node:fs/promises");
const { default: test } = await import("node:test");

const templateUrl = new URL('../../../resources/wanxiangshu.mjs', import.meta.url)
const MANAGED = [
  'engineer',
  'manager',
  'orchestrator',
  'devops',
  'blogger',
]

test('WHAT[execution-model-routing-005] EMR_005_recommended_resource_is_only_a_policy_template', async () => {
  const { default: scheduler } = await import(`${templateUrl.href}?policy=${Date.now()}`)
  const { invokeScheduler } = await import('../../../dist/OpenCode/Host/ModelRoutingSurface.js')
  const route = (role, running, previous = null) => invokeScheduler(scheduler, role, running, previous)
  const first = route('engineer', [])
  const occupied = Array.from({ length: 4 }, () => ({ ...first }))
  const next = route('engineer', occupied)

  assert.notDeepEqual(next, first, 'the template itself, not runtime, owns capacity policy')
})
test('WHAT[execution-model-routing-005] EMR_005_recommended_template_counts_capacity_by_provider_across_models', async () => {
  const { default: scheduler } = await import(`${templateUrl.href}?provider=${Date.now()}`)
  const { invokeScheduler } = await import('../../../dist/OpenCode/Host/ModelRoutingSurface.js')
  const route = (role, running, previous = null) => invokeScheduler(scheduler, role, running, previous)
  // Canonical blogger pool spans ollama-cloud (limit 16) + opencode-go (limit 8).
  // Filling only opencode-go leaves the ollama-cloud candidate available.
  const opencodeFull = Array.from({ length: 8 }, () => ({
    model: 'opencode-go/deepseek-v4-flash',
    reasoning: 'low',
  }))

  assert.equal(
    route('blogger', opencodeFull, null)?.model,
    'ollama-cloud/gemma4:31b',
    'opencode-go/minimax-m3 shares provider capacity with opencode-go/deepseek-v4-flash',
  )

  const bothFull = [
    ...opencodeFull,
    ...Array.from({ length: 16 }, () => ({
      model: 'ollama-cloud/gemma4:31b',
      reasoning: 'none',
    })),
  ]
  assert.equal(
    route('blogger', bothFull, null),
    null,
    'canonical blogger shares provider capacity across both of its providers',
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFile } = await import("node:fs/promises");
const { default: test } = await import("node:test");

const source = async (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('WHAT[execution-model-routing-005] EMR_005_runtime_contains_no_product_lane_or_max_sessions_policy', async () => {
  const routing = await source('src/Wanxiangshu/OpenCode/Host/ModelRouting.fs')
  assert.doesNotMatch(routing, /ExecutionLane|ModelLaneConfig|max_sessions|firstFree|first-free/)
})
}

import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const templateUrl = new URL('../../../resources/wanxiangshu.mjs', import.meta.url)

const MANAGED = [
  'fast-orchestrator', 'deep-orchestrator',
  'fast-manager', 'deep-manager',
  'fast-coder', 'deep-coder',
  'fast-inspector', 'deep-inspector',
  'fast-devops', 'deep-devops',
  'fast-browser', 'deep-browser',
  'fast-inquiry', 'deep-inquiry',
  'fast-reviewer', 'deep-reviewer',
  'fast-blogger', 'deep-blogger',
  'fast-distiller', 'deep-distiller',
  'fast-bookkeeper', 'deep-bookkeeper',
]

test('WHAT[EMR-001] EMR_001_recommended_resource_is_directly_executable_and_uses_full_model_selectors', async () => {
  const source = await readFile(templateUrl, 'utf8')
  assert.match(source, /export default function route/)
  const { default: route } = await import(`${templateUrl.href}?test=${Date.now()}`)

  for (const role of MANAGED) {
    const selected = route(role, [])
    assert.ok(selected, `${role} must be schedulable from the empty recommended template`)
    assert.match(selected.model, /^[^/\s]+\/.+$/, `${role} must use provider/model, never a bare model id`)
    assert.ok(selected.reasoning.length > 0)
  }

  assert.match(route('fast-browser', []).model, /minimax-m3/)
  assert.match(route('deep-browser', []).model, /minimax-m3/)
})

test('WHAT[EMR-005] EMR_005_recommended_resource_is_only_a_policy_template', async () => {
  const { default: route } = await import(`${templateUrl.href}?policy=${Date.now()}`)
  const first = route('fast-coder', [])
  const occupied = Array.from({ length: 8 }, () => ({ ...first }))
  const next = route('fast-coder', occupied)

  assert.notDeepEqual(next, first, 'the template itself, not runtime, owns capacity/fallback policy')
})

test('WHAT[EMR-005] EMR_005_recommended_template_counts_capacity_by_provider_across_models', async () => {
  const { default: route } = await import(`${templateUrl.href}?provider=${Date.now()}`)
  const occupied = Array.from({ length: 8 }, () => ({
    model: 'opencode-go/deepseek-v4-flash',
    reasoning: 'low',
  }))

  assert.equal(
    route('deep-browser', occupied, null),
    null,
    'opencode-go/minimax-m3 shares provider capacity with opencode-go/deepseek-v4-flash',
  )
})

test('WHAT[EMR-006] EMR_006_recommended_template_prefers_previous_candidate_when_provider_has_capacity', async () => {
  const { default: route } = await import(`${templateUrl.href}?previous=${Date.now()}`)
  const previous = { model: 'neuralwatt/glm-5.2-flex', reasoning: 'high' }

  assert.deepEqual(route('deep-coder', [], previous), previous)

  const neuralwattFull = Array.from({ length: 4 }, () => ({
    model: 'neuralwatt/another-model',
    reasoning: 'none',
  }))
  assert.equal(
    route('deep-coder', neuralwattFull, previous).model,
    'cursor/cursor-grok-4.6-xhigh',
    'when the previous provider is full, normal candidate fallback still applies',
  )
})

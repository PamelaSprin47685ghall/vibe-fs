import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const BASE = 'base blogger system prompt'

test('WHAT[BD-004] BEHAVIOR_DIAGNOSIS_SYSTEM_001_composed_prompt_contains_every_tip_exactly_once', () => {
  const composed = enforcer.composeBloggerSystemPrompt(BASE, 'en')
  assert.ok(composed.includes(BASE), 'base prompt must be preserved')
  assert.ok(composed.includes('# Enforcer Rulebook'), 'rulebook header must be present')
  assert.equal(composed.split('\n').filter(Boolean).every((line) => line === '#' || line.startsWith('# ')), true)

  const names = enforcer.rules().map((r) => r.name)
  assert.equal(names.length, 120)
  for (const name of names) {
    const occurrences = composed.split('\n').filter((line) => line === `# ${name}`).length
    assert.equal(occurrences, 1, `TipName ${name} must appear exactly once, got ${occurrences}`)
  }
})

test('WHAT[BD-004] BEHAVIOR_DIAGNOSIS_SYSTEM_002_composition_is_deterministic', () => {
  const a = enforcer.composeBloggerSystemPrompt(BASE, 'en')
  const b = enforcer.composeBloggerSystemPrompt(BASE, 'en')
  assert.equal(a, b, 'same rulebook + base must compose to identical bytes')
})

test('WHAT[BD-004] BEHAVIOR_DIAGNOSIS_SYSTEM_004_english_load_matches_packaged_rule_count', () => {
  const en = enforcer.rules()
  assert.equal(en.length, enforcer.ruleCount())
  assert.equal(en.length, 120)
})

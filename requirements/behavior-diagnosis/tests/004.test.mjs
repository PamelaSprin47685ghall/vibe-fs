import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const BASE = 'base blogger system prompt'

test('WHAT[behavior-diagnosis-004] BEHAVIOR_DIAGNOSIS_SYSTEM_001_composed_prompt_contains_every_tip_exactly_once', () => {
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

test('WHAT[behavior-diagnosis-004] BEHAVIOR_DIAGNOSIS_SYSTEM_002_composition_is_deterministic', () => {
  const a = enforcer.composeBloggerSystemPrompt(BASE, 'en')
  const b = enforcer.composeBloggerSystemPrompt(BASE, 'en')
  assert.equal(a, b, 'same rulebook + base must compose to identical bytes')
})

test('WHAT[behavior-diagnosis-004] BEHAVIOR_DIAGNOSIS_SYSTEM_004_english_load_matches_packaged_rule_count', () => {
  const en = enforcer.rules()
  assert.equal(en.length, enforcer.ruleCount())
  assert.equal(en.length, 120)
})

integrationTest('WHAT[behavior-diagnosis-004] ENFORCER_resource_effective_blogger_prompt_includes_all_enforcer_texts', () => {
  const rules = enforcer.rules()
  const composed = enforcer.composeBloggerSystemPrompt('base', 'en')
  assert.match(composed, /# Enforcer Rulebook/)
  for (const rule of rules) {
    assert.match(composed, new RegExp(`# ${rule.name}`))
    const commented = rule.enforcerText.trim().split('\n').map((line) => line === '' ? '#' : `# ${line}`).join('\n')
    assert.ok(composed.includes(commented))
  }
})

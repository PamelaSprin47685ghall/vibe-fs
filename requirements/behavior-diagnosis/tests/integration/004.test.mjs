import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../../dist/Enforcer/Surface.js'

test('WHAT[BD-004] ENFORCER_resource_effective_blogger_prompt_includes_all_enforcer_texts', () => {
  const rules = enforcer.rules()
  const composed = enforcer.composeBloggerSystemPrompt('base', 'en')
  assert.match(composed, /# Enforcer Rulebook/)
  for (const rule of rules) {
    assert.match(composed, new RegExp(`# ${rule.name}`))
    const commented = rule.enforcerText.trim().split('\n').map((line) => line === '' ? '#' : `# ${line}`).join('\n')
    assert.ok(composed.includes(commented))
  }
})

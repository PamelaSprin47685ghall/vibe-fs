import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as enforcer from '../../../../dist/Enforcer/Surface.js'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../../')

const enforcerRoot = path.join(repoRoot, 'resources', 'enforcer')

test('WHAT[behavior-diagnosis-004] ENFORCER_resource_effective_blogger_prompt_includes_all_enforcer_texts', () => {
  const rules = enforcer.rules()
  const composed = enforcer.composeBloggerSystemPrompt('base', 'en')
  assert.match(composed, /# Enforcer Rulebook/)
  for (const rule of rules) {
    assert.match(composed, new RegExp(`# ${rule.name}`))
    const commented = rule.enforcerText.trim().split('\n').map((line) => line === '' ? '#' : `# ${line}`).join('\n')
    assert.ok(composed.includes(commented))
  }
})

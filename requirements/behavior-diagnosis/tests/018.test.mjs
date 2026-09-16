// BD-018: RulebookRevision freeze per Blogger life
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

test('WHAT[BD-018] BEHAVIOR_DIAGNOSIS_SYSTEM_005_rulebook_revision_freezes_system_prompt_and_tool_definitions', () => {
  const base = 'blogger base prompt'
  const promptA = enforcer.composeBloggerSystemPrompt(base, 'en')
  const rulesA = enforcer.rules()
  const fieldsA = enforcer.fieldNames()

  assert.ok(promptA.length > 0, 'system prompt must be non-empty')
  assert.equal(rulesA.length, 120, 'bound rulebook revision contains exact rule count')
  assert.equal(fieldsA.length, 120, 'chronicle.tip enumeration matches rule count')

  for (const field of fieldsA) {
    const found = enforcer.tryFindByField(field)
    assert.ok(found !== null, `rule ${field} must be found in bound revision`)
    assert.equal(found.fieldName, field)
    assert.ok(found.mainText.trim().length > 0, `mainText must be non-empty for ${field}`)
    assert.ok(found.enforcerText.trim().length > 0, `enforcerText must be non-empty for ${field}`)

    const decoded = enforcer.decodeCall({ tip: field, text: 'valid entry' })
    assert.equal(decoded.ok, true, `decodeCall must succeed for ${field}`)
    assert.equal(decoded.value.tip.fieldName, field)
  }

  const promptB = enforcer.composeBloggerSystemPrompt(base, 'en')
  assert.equal(promptA, promptB, 'system prompt bytes must remain frozen across in-flight cycles')
})

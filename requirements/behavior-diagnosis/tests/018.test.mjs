import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const BASE = 'base blogger system prompt'

test('WHAT[BD-018] BEHAVIOR_DIAGNOSIS_SYSTEM_005_rulebook_revision_freezes_system_prompt_and_tool_definitions', () => {
  const base = 'blogger base prompt'
  const promptA = enforcer.composeBloggerSystemPrompt(base, 'en')
  const rulesA = enforcer.rules()
  const fieldsA = enforcer.fieldNames()

  // In-flight Blogger life maintains exact frozen system prompt bytes and tip enum
  assert.ok(promptA.length > 0, 'system prompt must be non-empty')
  assert.equal(rulesA.length, 120, 'bound rulebook revision contains exact rule count')
  assert.equal(fieldsA.length, 120, 'chronicle.tip enumeration matches rule count')

  // Tool decode mapping and main remediation lookup derive from the same frozen revision
  for (const field of fieldsA) {
    const found = enforcer.tryFindByField(field)
    assert.ok(found !== null, `rule ${field} must be found in bound revision`)
    assert.equal(found.fieldName, field)
    assert.ok(found.mainText.trim().length > 0, `mainText must be non-empty for ${field}`)
    assert.ok(found.enforcerText.trim().length > 0, `enforcerText must be non-empty for ${field}`)

    // Chronicle decode mapping is consistent with the frozen enum
    const decoded = enforcer.decodeCall({ tip: field, text: 'valid entry' })
    assert.equal(decoded.ok, true, `decodeCall must succeed for ${field}`)
    assert.equal(decoded.value.tip.fieldName, field)
  }

  // Byte stability: repeated composition within the life remains byte-identical
  const promptB = enforcer.composeBloggerSystemPrompt(base, 'en')
  assert.equal(promptA, promptB, 'system prompt bytes must remain frozen across in-flight cycles')
})

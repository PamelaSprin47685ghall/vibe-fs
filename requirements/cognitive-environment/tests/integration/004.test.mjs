import assert from 'node:assert/strict'
import test from 'node:test'
import * as providerLanguage from '../../../../dist/Participant/Provider/LanguageSurface.js'

const english = 'English'

const ROLE_PATHS = [
  'role/manager', 'role/coder', 'role/devops', 'role/inspector',
  'role/browser', 'role/inquiry', 'role/orchestrator', 'role/distiller', 'role/blogger', 'role/bookkeeper',
]

const forbiddenRoleToolInventory = /\b(?:todowrite|open-terminal|send-terminal|read-terminal|signal-terminal|query-shell|sphinx_start|sphinx_resume|js-[a-z-]+)\b/i

test('WHAT[COGNITIVE-ENVIRONMENT-004] PROMPT_role_laws_are_identity_not_tool_inventory', () => {
  for (const path of ROLE_PATHS) {
    const law = providerLanguage.readText(english, path)
    assert.doesNotMatch(law, forbiddenRoleToolInventory, path)
  }

  const inquiry = providerLanguage.readText(english, 'role/inquiry')
  assert.match(inquiry, /Inspector/)
  assert.doesNotMatch(inquiry, /sphinx_start|sphinx_resume/)
})

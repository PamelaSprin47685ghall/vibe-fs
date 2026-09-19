import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as promptResources from '../../../dist/Resources/PromptSurface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const walkMarkdown = (relDir) => {
  const out = []
  const walkDir = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const p = join(dir, entry.name)
      if (entry.isDirectory()) walkDir(p)
      else if (entry.name.endsWith('.md')) out.push(readFileSync(p, 'utf8'))
    }
  }
  walkDir(join(ROOT, relDir))
  return out
}

const ROLE_LAW_ROLES = Object.freeze([
  'manager',
  'coder',
  'inspector',
  'devops',
  'orchestrator',
  'blogger',
  'distiller',
  'bookkeeper',
])

const MIRRORED_BY_OFFICE_CAPABILITY = new Set(['entrust-by-consequence', 'choose-by-return', 'no-omnipotent-charge'])

const LANGUAGE = 'English'

test('WHAT[cognitive-environment-004] CE_prompt_015_system_prompt_does_not_enumerate_runtime_tool_surface', () => {
  for (const prompt of promptResources.allForLanguage(LANGUAGE)) {
    assert.doesNotMatch(prompt, /\b(fast|deep)-[a-z]+/, 'machine binding names must not appear in system prompts')
    assert.doesNotMatch(prompt, /auto-injected|ToolPermission/, 'runtime tool-surface machinery must not enter Role Law')
  }
})

{
const providerLanguage = await import("../../../dist/Participant/Provider/LanguageSurface.js");

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

const ROLE_PATHS = [
  'role/manager', 'role/engineer', 'role/devops', 'role/orchestrator', 'role/blogger', 'role/bookkeeper',
]

const RETIRED_ROLE_PATHS = [
  'role/coder', 'role/inspector', 'role/browser', 'role/inquiry', 'role/distiller',
]

const forbiddenRoleToolInventory = /\b(?:todowrite|open-terminal|send-terminal|read-terminal|signal-terminal|query-shell|sphinx_start|sphinx_resume|js-[a-z-]+)\b/i

integrationTest('WHAT[cognitive-environment-004] PROMPT_role_laws_are_identity_not_tool_inventory', () => {
  for (const path of ROLE_PATHS) {
    const law = providerLanguage.readText(english, path)
    assert.doesNotMatch(law, forbiddenRoleToolInventory, path)
  }

  for (const path of RETIRED_ROLE_PATHS) {
    assert.equal(providerLanguage.exists(english, path), false, `${path} must not ship a Role Law`)
    assert.equal(providerLanguage.exists(simplifiedChinese, path), false, `${path} must not ship a Role Law`)
  }
})
}

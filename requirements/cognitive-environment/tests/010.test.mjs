import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as promptResources from '../../../dist/Resources/PromptSurface.js'

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

test('WHAT[COGNITIVE-ENVIRONMENT-010] CE_010_lifecycle_texts_orient_without_educating_or_replacing_system_prompt', () => {
  const transientTexts = [...walkMarkdown('resources/provider/lifecycle'), ...walkMarkdown('resources/provider/runtime')]
  assert.ok(transientTexts.length > 0, 'lifecycle and runtime provider texts must exist')
  for (const text of transientTexts) {
    assert.doesNotMatch(text, /educate|teach|教学|培训|lesson/i, 'lifecycle texts orient, they do not educate')
    assert.doesNotMatch(text, /system prompt|system-prompt|系统提示词/i, 'no lifecycle text triggers a system prompt replacement')
    assert.doesNotMatch(text, /envelope|第二套/i, 'no second envelope is stacked onto the canonical prompt')
  }
})

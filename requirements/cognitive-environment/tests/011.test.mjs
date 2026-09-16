import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

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

test('WHAT[COGNITIVE-ENVIRONMENT-011] CE_011_transient_texts_do_not_rewrite_role_self_model', () => {
  const transientTexts = [...walkMarkdown('resources/provider/lifecycle'), ...walkMarkdown('resources/provider/runtime')]
  assert.ok(transientTexts.length > 0, 'lifecycle and runtime provider texts must exist')
  for (const text of transientTexts) {
    assert.doesNotMatch(text, /identity|persona|self-model|你是谁|身份/i, 'the office identity is decided by Role Law, not by the current phase')
    assert.doesNotMatch(text, /\b(fast|deep)-[a-z]+/, 'transient texts never expose fast/deep machine identity')
  }
})

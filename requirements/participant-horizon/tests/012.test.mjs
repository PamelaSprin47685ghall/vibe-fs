import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const LOCALES = ['en', 'zh-CN']

test('WHAT[PARTICIPANT-HORIZON-012] warm_start_keywords_entry_restricted_to_repository_evidence_roles', () => {
  for (const locale of LOCALES) {
    const text = read(`resources/provider/tool/fork/warm-start-unavailable/${locale}.md`)
    assert.match(text, /\bCoder\b/i, `warm-start-unavailable/${locale}.md must name Coder`)
    assert.match(text, /\bInspector\b/i, `warm-start-unavailable/${locale}.md must name Inspector`)
    assert.match(text, /\bDevOps\b/i, `warm-start-unavailable/${locale}.md must name DevOps`)
  }
})

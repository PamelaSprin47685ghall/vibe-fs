import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[COGNITIVE-ENVIRONMENT-007] CE_prompt_016_library_ingress_teaches_craft_within_existing_authority', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/library/ingress/${locale}.md`)
    assert.match(text, /teach the craft|craft|技艺|手艺/i)
    assert.doesNotMatch(text, /grant.{0,40}authority|授予.*authority/i)
  }
})

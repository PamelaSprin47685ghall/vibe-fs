import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[COGNITIVE-ENVIRONMENT-006] CE_prompt_016_library_ingress_books_do_not_enlarge_authority', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/library/ingress/${locale}.md`)
    assert.match(text, /do not enlarge your authority|不扩大|不会扩大|不?扩大你的权/i)
    assert.match(text, /do not override the Common Law|不会覆盖 Common Law|Common Law/i)
  }
})

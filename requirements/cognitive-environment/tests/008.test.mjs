import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[COGNITIVE-ENVIRONMENT-008] CE_prompt_016_office_library_closing_books_older_than_assignment', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/library/closing/${locale}.md`)
    assert.match(text, /older than this assignment|比.*assignment|比.*更老|旧于/i)
  }
})

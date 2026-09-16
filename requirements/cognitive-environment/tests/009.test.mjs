import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[COGNITIVE-ENVIRONMENT-009] CE_prompt_016_office_library_closing_work_not_forced_to_resemble_book', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/library/closing/${locale}.md`)
    assert.match(text, /do not force the work|不?要强|不要.*模仿|Don't force/i)
  }
})

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[COGNITIVE-ENVIRONMENT-016] CE_016_pair_hint_retains_brief_trigger_without_repeating_full_psychological_contract', () => {
  for (const locale of ['en', 'zh-CN']) {
    const hint = read(`resources/provider/host/pair-programming-guideline/${locale}.md`)
    const assumeDesc = read(`resources/provider/tool/assume/description/${locale}.md`)

    assert.match(hint, /`assume`/)
    assert.match(hint, /jq/i)
    assert.doesNotMatch(hint, /map\(|select\(|setpath|delpaths/i, `${locale} pair hint must not repeat jq manual`)

    assert.match(assumeDesc, /update.*query/is)
    assert.match(assumeDesc, /map\(|select\(|setpath|delpaths/i, `${locale} tool description carries the detailed jq guidance`)
    assert.match(assumeDesc, /经验丰富|experienced participant/i, `${locale} tool description preserves the old commitment psychology`)
  }
})

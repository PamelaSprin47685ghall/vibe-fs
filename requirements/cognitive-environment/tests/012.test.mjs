import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[COGNITIVE-ENVIRONMENT-012] CE_012_relay_assessment_prompt_carries_ledger_without_process_mechanics', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/library/relay/quality-ledger/${locale}.md`)
    assert.match(text, /Ledger|judgment|acceptance/i, 'Relay assessment prompt carries Ledger guidance')
    assert.match(text, /independent|独立/, `${locale}: assessment prompt must teach independent judgement`)
    assert.doesNotMatch(
      text,
      /\bbarrier\b|\b2N\b|\bwitness\b|\bcohort\b|confirmation rounds|dedicated session|双 PERFECT|双完美/i,
      `${locale}: hidden PERFECT-process mechanics must not enter the assessment prompt`,
    )
  }
})

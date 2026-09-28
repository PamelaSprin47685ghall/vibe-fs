import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as language from '../../../dist/Participant/Provider/LanguageSurface.js'
import { guidance, link, observe, withJournal, main, tip } from './support/journal.mjs'

for (const [locale, suffix] of [['English', ''], ['SimplifiedChinese', '.zh-CN']]) {
  test(`WHAT[guidance-delivery-002] ${locale} first Full contains the entire authored Main body`, async () => {
    language.clearAllForTests()
    try {
      assert.equal(language.bindOnce(main, locale).ok, true)
      await withJournal(async ({ journal }) => {
        await link(journal)
        await observe(journal)
        const result = await guidance.resolve(journal, main)
        const body = readFileSync(new URL(`../../../resources/enforcer/${tip}/main${suffix}.md`, import.meta.url), 'utf8').trim()
        assert.equal(result.presentation, 'Full')
        assert.equal(result.tipName, tip)
        assert.ok(result.text.includes(body))
      })
    } finally {
      language.clearAllForTests()
    }
  })
}

test.todo('WHAT[guidance-delivery-002] GAP-116 actual rejected or unknown delivery append cannot report successful Full or advance Frontier')

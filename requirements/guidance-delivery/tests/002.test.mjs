import assert from 'node:assert/strict'
import test from 'node:test'
import * as language from '../../../dist/Participant/Provider/LanguageSurface.js'
import { guidance, link, observe, withJournal, main, tip, authoredBody } from './support/journal.mjs'

for (const [locale, suffix] of [['English', ''], ['SimplifiedChinese', '.zh-CN']]) {
  test(`WHAT[guidance-delivery-002] ${locale} first Full contains the entire authored Main body`, async () => {
    const previous = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    process.env.WANXIANGSHU_PROVIDER_LANGUAGE = locale === 'English' ? 'en' : 'zh-CN'
    language.refreshGlobalLanguage()
    try {
      await withJournal(async ({ journal }) => {
        await link(journal)
        await observe(journal)
        const result = await guidance.resolve(journal, main)
        assert.equal(result.presentation, 'Full')
        assert.equal(result.tipName, tip)
        assert.ok(result.text.includes(authoredBody))
      })
    } finally {
      if (previous === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
      else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previous
      language.refreshGlobalLanguage()
    }
  })
}

test.todo('WHAT[guidance-delivery-002] GAP-116 actual rejected or unknown delivery append cannot report successful Full or advance Frontier')

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as prompts from '../../../dist/Resources/PromptSurface.js'

for (const [language, locale] of [['English', 'en'], ['SimplifiedChinese', 'zh-CN']]) {
  test(`WHAT[cognitive-environment-012] ${locale} Manager system includes the complete role and Quality Ledger resources`, () => {
    const system = prompts.systemForRole(language, 'manager').trimEnd().split('\n')
      .map((line) => line === '#' ? '' : line.slice(2)).join('\n')
    for (const semantic of ['role/manager', 'library/relay/quality-ledger']) {
      const resource = readFileSync(new URL(`../../../resources/provider/${semantic}/${locale}.md`, import.meta.url), 'utf8').trim()
      assert.ok(resource.length > 0)
      assert.ok(system.includes(resource), `${language}: ${semantic}`)
    }
  })
}

test.todo('WHAT[cognitive-environment-012] assessment actually judges current evidence independently and honestly without hidden-process influence; resource inclusion does not prove behavior (GAP-076)')

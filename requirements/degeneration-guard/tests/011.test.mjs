import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as language from '../../../dist/Participant/Provider/LanguageSurface.js'

for (const [locale, directory] of [['English', 'en'], ['SimplifiedChinese', 'zh-CN']]) {
  test(`WHAT[degeneration-guard-011] ${locale} projects the two distinct canonical guard resources`, () => {
    const repetitive = language.readText(locale, 'runtime/degeneration-too-repetitive')
    const random = language.readText(locale, 'runtime/degeneration-too-random')
    for (const [key, projected] of [['degeneration-too-repetitive', repetitive], ['degeneration-too-random', random]]) {
      const source = readFileSync(new URL(`../../../resources/provider/runtime/${key}/${directory}.md`, import.meta.url), 'utf8')
      assert.equal(projected, source.trim())
      assert.ok(projected.trim().length > 0)
    }
    assert.notEqual(repetitive, random)
  })
}

test.todo('WHAT[degeneration-guard-011] actual continuation binds DegenerationGuard authority and uses the matching anomaly resource (GAP-145)')

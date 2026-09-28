import assert from 'node:assert/strict'
import test from 'node:test'
import { scanParity } from '../../../scripts/checks/language-parity-gate.mjs'
import { requireLanguagePair, readText } from '../../../dist/Participant/Provider/LanguageSurface.js'
import { resourceFixture } from './support/resource-fixture.mjs'

test('WHAT[provider-language-006] the real parity checker rejects either missing locale and accepts a pair', () => {
  for (const [present, missingCode] of [['en.md', 'missing-zh-cn'], ['zh-CN.md', 'missing-en']]) {
    const fixture = resourceFixture()
    try {
      fixture.write('role/example', present, 'Present resource')
      const missing = scanParity(['role/example'], fixture.directory)
      assert.equal(missing.length, 1)
      assert.equal(missing[0].code, missingCode)
      fixture.write('role/example', present === 'en.md' ? 'zh-CN.md' : 'en.md', 'Counterpart')
      assert.deepEqual(scanParity(['role/example'], fixture.directory), [])
    } finally {
      fixture.dispose()
    }
  }
})

test('WHAT[provider-language-006] the resource loader rejects a missing semantic resource instead of supplying another', () => {
  assert.throws(() => requireLanguagePair('role/does-not-exist'), /missing/)
  requireLanguagePair('role/manager')
  for (const language of ['English', 'SimplifiedChinese']) {
    assert.ok(readText(language, 'role/manager').length > 0)
    assert.throws(() => readText(language, 'role/does-not-exist'))
  }
})

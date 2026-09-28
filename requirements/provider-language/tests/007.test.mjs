import assert from 'node:assert/strict'
import test from 'node:test'
import { extractPlaceholders, scanPlaceholderParity } from '../../../scripts/checks/language-parity-gate.mjs'
import { substitute } from '../../../dist/Participant/Provider/LanguageSurface.js'
import { resourceFixture } from './support/resource-fixture.mjs'

test('WHAT[provider-language-007] placeholder comparison accepts order changes and rejects a missing parameter', () => {
  const fixture = resourceFixture()
  try {
    fixture.write('tool/demo', 'en.md', '{{byname}} carries {{charge}}.')
    fixture.write('tool/demo', 'zh-CN.md', '{{charge}} 交给 {{byname}}。')
    assert.deepEqual(scanPlaceholderParity(['tool/demo'], fixture.directory), [])
    fixture.write('tool/demo', 'zh-CN.md', '{{byname}} 承担托付。')
    const missing = scanPlaceholderParity(['tool/demo'], fixture.directory)
    assert.equal(missing.length, 1)
    assert.equal(missing[0].code, 'placeholder-parity')
    assert.match(missing[0].detail, /only-en: \[charge\]/)
  } finally {
    fixture.dispose()
  }
})

test('WHAT[provider-language-007] parameter values retain their text and missing values fail', () => {
  assert.deepEqual([...extractPlaceholders('{{byname}} / {{charge}} / {{byname}}')].sort(), ['byname', 'charge'])
  assert.equal(substitute('Result {{value}}.', { value: 'exit_code 中文 /tmp/path' }), 'Result exit_code 中文 /tmp/path.')
  assert.throws(() => substitute('Hello {{name}}.', {}), /missing substitution/)
  assert.throws(() => substitute('{{a}} then {{b}}.', { a: 'x' }), /missing substitution/)
})

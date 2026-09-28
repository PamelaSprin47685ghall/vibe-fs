import assert from 'node:assert/strict'
import test from 'node:test'
import { scanForbiddenPromptPhrases } from '../../../scripts/checks/language-parity-gate.mjs'
import { resourceFixture } from './support/resource-fixture.mjs'

test('WHAT[provider-language-012] the existing contradiction scanner distinguishes direct grants from direct prohibitions', () => {
  const fixture = resourceFixture()
  try {
    for (const [locale, prohibited, granted] of [
      ['en.md', 'Manager cannot use Fission. DevOps cannot Fission.', 'Manager may use Fission.'],
      ['zh-CN.md', 'Manager 不能使用 Fission。DevOps 不能 Fission。', 'DevOps 可以使用 Fission。'],
    ]) {
      fixture.write('role/demo', locale, prohibited)
      assert.deepEqual(scanForbiddenPromptPhrases(fixture.directory), [])
      fixture.write('role/demo', locale, granted)
      const violations = scanForbiddenPromptPhrases(fixture.directory)
      assert.equal(violations.length, 1)
      assert.equal(violations[0].code, 'forbidden-fission-claim')
      fixture.write('role/demo', locale, prohibited)
    }
  } finally {
    fixture.dispose()
  }
})

test.todo('WHAT[provider-language-012] bilingual semantic equivalence requires review against current office responsibilities including standard Engineer permissions in Sphinx; phrase matching does not prove this')

import assert from 'node:assert/strict'
import test from 'node:test'
import { clearAllForTests, bindOnce, ensureInherited, tryGet } from '../../../dist/Participant/Provider/LanguageSurface.js'
import { withPreference } from './support/language-fixtures.mjs'

test.beforeEach(clearAllForTests)
test.afterEach(clearAllForTests)

test('WHAT[provider-language-003] a child inherits its bound owner even when the global preference is invalid', async () => {
  assert.equal(bindOnce('owner', 'SimplifiedChinese').ok, true)
  await withPreference('not-a-language', () => {
    assert.equal(ensureInherited('owner', 'child'), 'SimplifiedChinese')
    assert.equal(ensureInherited('child', 'grandchild'), 'SimplifiedChinese')
    assert.equal(tryGet('grandchild'), 'SimplifiedChinese')
  })
})

test('WHAT[provider-language-003] inheritance cannot overwrite a different existing child binding', () => {
  assert.equal(bindOnce('owner', 'SimplifiedChinese').ok, true)
  assert.equal(bindOnce('child', 'English').ok, true)
  assert.throws(() => ensureInherited('owner', 'child'), /already bound/)
  assert.equal(tryGet('child'), 'English')
  assert.equal(tryGet('owner'), 'SimplifiedChinese')
})

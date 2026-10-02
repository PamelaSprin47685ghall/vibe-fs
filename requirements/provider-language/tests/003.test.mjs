import assert from 'node:assert/strict'
import test from 'node:test'
import { clearAllForTests, refreshGlobalLanguage, languageOfSession } from '../../../dist/Participant/Provider/LanguageSurface.js'
import { withPreference } from './support/language-fixtures.mjs'

test.beforeEach(clearAllForTests)
test.afterEach(clearAllForTests)

test('WHAT[provider-language-003] derived sessions follow the live global preference, never a stored owner value', async () => {
  await withPreference('zh-CN', async () => {
    refreshGlobalLanguage()
    assert.equal(languageOfSession('owner'), 'SimplifiedChinese')
    assert.equal(languageOfSession('child'), 'SimplifiedChinese')
    assert.equal(languageOfSession('grandchild'), 'SimplifiedChinese')
  })
  await withPreference('en', async () => {
    refreshGlobalLanguage()
    assert.equal(languageOfSession('owner'), 'English', 'the owner itself follows the changed preference')
    assert.equal(languageOfSession('child'), 'English', 'and so does the derived child, immediately')
  })
})
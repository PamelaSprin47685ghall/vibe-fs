import assert from 'node:assert/strict'
import test from 'node:test'
import { clearAllForTests, refreshGlobalLanguage, languageOfSession } from '../../../dist/Participant/Provider/LanguageSurface.js'
import { withPreference } from './support/language-fixtures.mjs'

test.beforeEach(clearAllForTests)
test.afterEach(clearAllForTests)

test('WHAT[provider-language-002] no per-session binding exists: every session answers the live global preference', async () => {
  await withPreference('zh-CN', async () => {
    refreshGlobalLanguage()
    assert.equal(languageOfSession('any-session'), 'SimplifiedChinese')
    assert.equal(languageOfSession('another-session'), 'SimplifiedChinese')
  })
  await withPreference('en', async () => {
    refreshGlobalLanguage()
    assert.equal(languageOfSession('any-session'), 'English', 'the same session now answers the changed preference')
  })
})

test.todo('WHAT[provider-language-002] a live preference change must reach a real in-flight Host session on its next request; a surface-level registry check does not prove the Host boundary')
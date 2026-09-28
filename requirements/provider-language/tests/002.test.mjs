import assert from 'node:assert/strict'
import test from 'node:test'
import { clearAllForTests, bindOnce, tryGet, languageOfSession } from '../../../dist/Participant/Provider/LanguageSurface.js'

test.beforeEach(clearAllForTests)
test.afterEach(clearAllForTests)

test('WHAT[provider-language-002] duplicate binding is idempotent and conflicting binding leaves the original intact', () => {
  for (const [language, other] of [['English', 'SimplifiedChinese'], ['SimplifiedChinese', 'English']]) {
    const session = `bound-${language}`
    assert.equal(bindOnce(session, language).ok, true)
    assert.equal(bindOnce(session, language).value, language)
    assert.equal(bindOnce(session, other).ok, false)
    assert.equal(tryGet(session), language)
    assert.equal(languageOfSession(session), language)
  }
})

test.todo('WHAT[provider-language-002] creation-time binding and recovery after a real process restart must preserve the original session language; a process-local registry test does not prove either boundary')

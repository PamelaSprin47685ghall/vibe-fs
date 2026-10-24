import assert from 'node:assert/strict'
import test from 'node:test'
import { clearAllForTests, refreshGlobalLanguage, loadBookkeeperSystem, transformBookkeeperSystem } from '../../../dist/Participant/Provider/LanguageSurface.js'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import { withPreference } from './support/language-fixtures.mjs'

test.beforeEach(clearAllForTests)
test.afterEach(clearAllForTests)

test('WHAT[provider-language-005] owned prose follows the live preference while foreign technical bytes survive unchanged', async () => {
  for (const language of ['English', 'SimplifiedChinese']) {
    const session = `bookkeeper-${language}`
    const preference = language === 'English' ? 'en' : 'zh-CN'
    await withPreference(preference, async () => {
      refreshGlobalLanguage()
      bookkeeper.bindSession(session, 'language-proof', 'language-proof')
      try {
        const expected = loadBookkeeperSystem(language)
        const other = loadBookkeeperSystem(language === 'English' ? 'SimplifiedChinese' : 'English')
        const foreign = 'HOST bytes: exit_code /tmp/中文路径 tool_name'
        const output = await transformBookkeeperSystem(session, [other, foreign])
        assert.equal(output.system[0], expected)
        assert.equal(output.system[1], foreign)
        if (language === 'SimplifiedChinese') {
          assert.match(output.system[2], /状态闭环/)
        } else {
          assert.match(output.system[2], /state closed-loop/i)
        }
        const repeated = await transformBookkeeperSystem(session, output.system)
        assert.deepEqual(repeated.system, output.system)
      } finally {
        bookkeeper.unbindSession(session)
      }
    })
  }
})
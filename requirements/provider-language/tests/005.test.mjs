import assert from 'node:assert/strict'
import test from 'node:test'
import { clearAllForTests, bindOnce, loadBookkeeperSystem, transformBookkeeperSystem } from '../../../dist/Participant/Provider/LanguageSurface.js'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'

test.beforeEach(clearAllForTests)
test.afterEach(clearAllForTests)

test('WHAT[provider-language-005] owned prose follows its language while foreign technical bytes survive unchanged', async () => {
  for (const language of ['English', 'SimplifiedChinese']) {
    const session = `bookkeeper-${language}`
    bookkeeper.bindSession(session, 'language-proof', 'language-proof')
    try {
      assert.equal(bindOnce(session, language).ok, true)
      const expected = loadBookkeeperSystem(language)
      const other = loadBookkeeperSystem(language === 'English' ? 'SimplifiedChinese' : 'English')
      const foreign = 'HOST bytes: exit_code /tmp/中文路径 tool_name'
      const output = await transformBookkeeperSystem(session, [other, foreign])
      assert.deepEqual(output.system, [expected, foreign])
      const repeated = await transformBookkeeperSystem(session, output.system)
      assert.deepEqual(repeated.system, output.system)
    } finally {
      bookkeeper.unbindSession(session)
    }
  }
})

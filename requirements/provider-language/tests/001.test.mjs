import assert from 'node:assert/strict'
import test from 'node:test'
import { parse, tryParse, label, resourceFileName, relativePath } from '../../../dist/Participant/Provider/LanguageSurface.js'

test('WHAT[provider-language-001] the two languages map to their canonical locale leaves', () => {
  for (const [raw, language, file] of [
    ['en', 'English', 'en.md'],
    ['zh-CN', 'SimplifiedChinese', 'zh-CN.md'],
  ]) {
    assert.equal(parse(raw), language)
    assert.equal(label(language), raw)
    assert.equal(resourceFileName(language), file)
    assert.equal(relativePath(language, 'role/manager'), `provider/role/manager/${file}`)
  }
  assert.equal(tryParse('nope'), null)
  assert.throws(() => parse('nope'), /unrecognized/)
})

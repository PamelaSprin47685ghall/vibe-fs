// tests/unit/prompt/provider-language.test.mjs — PROMPT-017 / HOST-026 Phase 2.
//
// ProviderLanguage parse + SessionProviderLanguage bind-once inherit.
// Does not migrate bilingual prose (Phase 17).

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  clearAllForTests,
  readGlobalPreference,
  parse,
  tryParse,
  label,
  resourceDirectory,
  bindOnce,
  inheritFromOwner,
  tryGet,
  inheritFrom,
  languageRootsPresent,
  relativePath,
  exists,
} from '../../../dist/Participant/Provider/LanguageSurface.js'

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'


test('WHAT[PROVIDER-LANGUAGE-004] global preference defaults to English when env unset', () => {
  const previous = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  try {
    assert.equal(readGlobalPreference(), english)
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previous
  }
})

test('WHAT[PROVIDER-LANGUAGE-001] ProviderLanguage parses en and zh-CN with locale mapping', () => {
  assert.equal(parse('en'), english)
  assert.equal(parse('english'), english)
  assert.equal(parse('zh-CN'), simplifiedChinese)
  assert.equal(parse('zh'), simplifiedChinese)
  assert.equal(tryParse('nope'), null)
  assert.equal(label(english), 'en')
  assert.equal(resourceDirectory(simplifiedChinese), 'zh-CN')
})

test('WHAT[PROVIDER-LANGUAGE-002] bind once is immutable and conflicting rebind fails closed', () => {
  clearAllForTests()
  const root = 'ses_root_lang'
  const zh = simplifiedChinese

  const bound = bindOnce(root, zh)
  assert.equal(bound.ok, true)
  assert.equal(bound.value, simplifiedChinese)

  const again = bindOnce(root, zh)
  assert.equal(again.ok, true)

  const conflict = bindOnce(root, english)
  assert.equal(conflict.ok, false)
  assert.match(conflict.error, /already bound/)
})

test('WHAT[PROVIDER-LANGUAGE-003] child inherits owner language without re-reading global', () => {
  clearAllForTests()
  const child = 'ses_child_lang'
  const zh = simplifiedChinese

  const inherited = inheritFromOwner(zh, child)
  assert.equal(inherited.ok, true)
  assert.equal(inherited.value, simplifiedChinese)
  assert.equal(tryGet(child), simplifiedChinese)
  assert.equal(inheritFrom(zh), simplifiedChinese)
})

test('WHAT[PROVIDER-LANGUAGE-001] provider resource language roots map en.md and zh-CN.md', () => {
  assert.equal(languageRootsPresent(), true)
  assert.equal(
    relativePath(english, 'role/manager'),
    'provider/role/manager/en.md',
  )
  assert.equal(
    relativePath(simplifiedChinese, 'role/manager'),
    'provider/role/manager/zh-CN.md',
  )
})

test('WHAT[PROVIDER-LANGUAGE-008] bound language loads its own locale leaf', () => {
  assert.equal(exists(english, 'role/manager'), true)
  assert.equal(exists(simplifiedChinese, 'role/manager'), true)
})

// tests/unit/prompt/provider-language.test.mjs — PROMPT-017 / HOST-026 Phase 2.
//
// ProviderLanguage parse + SessionProviderLanguage bind-once inherit.
// Does not migrate bilingual prose (Phase 17).

import assert from 'node:assert/strict'
import test from 'node:test'
import { readGlobalPreference } from '../../../dist/OpenCode/Host/ProviderLanguageBinding.js'
import { caseOf, providerLanguage, providerResources, sessionId } from '../../verification-system/tests/support/domain.mjs'

test('WHAT[PROVIDER-LANGUAGE-004] global preference defaults to English when env unset', () => {
  const previous = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  try {
    assert.equal(caseOf(readGlobalPreference()), 'English')
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previous
  }
})

test('WHAT[PROVIDER-LANGUAGE-001] ProviderLanguage parses en and zh-CN with locale mapping', () => {
  assert.equal(caseOf(providerLanguage.parse('en')), 'English')
  assert.equal(caseOf(providerLanguage.parse('english')), 'English')
  assert.equal(caseOf(providerLanguage.parse('zh-CN')), 'SimplifiedChinese')
  assert.equal(caseOf(providerLanguage.parse('zh')), 'SimplifiedChinese')
  assert.equal(providerLanguage.tryParse('nope'), undefined)
  assert.equal(providerLanguage.label(providerLanguage.english), 'en')
  assert.equal(providerLanguage.resourceDirectory(providerLanguage.simplifiedChinese), 'zh-CN')
})

test('WHAT[PROVIDER-LANGUAGE-002] bind once is immutable and conflicting rebind fails closed', () => {
  providerLanguage.clearAllForTests()
  const root = sessionId('ses_root_lang')
  const zh = providerLanguage.simplifiedChinese

  const bound = providerLanguage.bindOnce(root, zh)
  assert.equal(bound.ok, true)
  assert.equal(caseOf(bound.value), 'SimplifiedChinese')

  const again = providerLanguage.bindOnce(root, zh)
  assert.equal(again.ok, true)

  const conflict = providerLanguage.bindOnce(root, providerLanguage.english)
  assert.equal(conflict.ok, false)
  assert.match(conflict.error, /already bound/)
})

test('WHAT[PROVIDER-LANGUAGE-003] child inherits owner language without re-reading global', () => {
  providerLanguage.clearAllForTests()
  const root = sessionId('ses_root_lang')
  const child = sessionId('ses_child_lang')
  const zh = providerLanguage.simplifiedChinese

  const inherited = providerLanguage.inheritFromOwner(zh, child)
  assert.equal(inherited.ok, true)
  assert.equal(caseOf(inherited.value), 'SimplifiedChinese')
  assert.equal(caseOf(providerLanguage.tryGet(child)), 'SimplifiedChinese')
  assert.equal(caseOf(providerLanguage.inheritFrom(zh)), 'SimplifiedChinese')
})

test('WHAT[PROVIDER-LANGUAGE-001] provider resource language roots map en.md and zh-CN.md', () => {
  assert.equal(providerResources.languageRootsPresent(), true)
  assert.equal(
    providerResources.relativePath(providerLanguage.english, 'role/manager'),
    'provider/role/manager/en.md',
  )
  assert.equal(
    providerResources.relativePath(providerLanguage.simplifiedChinese, 'role/manager'),
    'provider/role/manager/zh-CN.md',
  )
})

test('WHAT[PROVIDER-LANGUAGE-008] bound language loads its own locale leaf', () => {
  assert.equal(providerResources.exists(providerLanguage.english, 'role/manager'), true)
  assert.equal(providerResources.exists(providerLanguage.simplifiedChinese, 'role/manager'), true)
})

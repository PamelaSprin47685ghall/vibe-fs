// tests/unit/prompt/provider-language.test.mjs — PROMPT-017 / HOST-026 Phase 2.
//
// ProviderLanguage parse + SessionProviderLanguage bind-once inherit.
// Does not migrate bilingual prose (Phase 17).

import assert from 'node:assert/strict'
import test from 'node:test'
import { readGlobalPreference } from '../../../dist/OpenCode/Host/ProviderLanguageBinding.js'
import { caseOf, providerLanguage, providerResources, sessionId } from '../../verification-system/tests/support/domain.mjs'

test('HOST_026_readGlobalPreference_defaults_when_env_unset', () => {
  const previous = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  try {
    assert.equal(caseOf(readGlobalPreference()), 'English')
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previous
  }
})

test('PROMPT_017_ProviderLanguage_parse_en_and_zh_CN', () => {
  assert.equal(caseOf(providerLanguage.parse('en')), 'English')
  assert.equal(caseOf(providerLanguage.parse('english')), 'English')
  assert.equal(caseOf(providerLanguage.parse('zh-CN')), 'SimplifiedChinese')
  assert.equal(caseOf(providerLanguage.parse('zh')), 'SimplifiedChinese')
  assert.equal(providerLanguage.tryParse('nope'), undefined)
  assert.equal(providerLanguage.label(providerLanguage.english), 'en')
  assert.equal(providerLanguage.resourceDirectory(providerLanguage.simplifiedChinese), 'zh-CN')
})

test('HOST_026_SessionProviderLanguage_bind_once_and_inherit', () => {
  providerLanguage.clearAllForTests()
  const root = sessionId('ses_root_lang')
  const child = sessionId('ses_child_lang')
  const zh = providerLanguage.simplifiedChinese

  const bound = providerLanguage.bindOnce(root, zh)
  assert.equal(bound.ok, true)
  assert.equal(caseOf(bound.value), 'SimplifiedChinese')

  const again = providerLanguage.bindOnce(root, zh)
  assert.equal(again.ok, true)

  const conflict = providerLanguage.bindOnce(root, providerLanguage.english)
  assert.equal(conflict.ok, false)
  assert.match(conflict.error, /already bound/)

  const inherited = providerLanguage.inheritFromOwner(zh, child)
  assert.equal(inherited.ok, true)
  assert.equal(caseOf(inherited.value), 'SimplifiedChinese')
  assert.equal(caseOf(providerLanguage.tryGet(child)), 'SimplifiedChinese')
  assert.equal(caseOf(providerLanguage.inheritFrom(zh)), 'SimplifiedChinese')
})

test('PROMPT_017_provider_resource_language_roots_present', () => {
  assert.equal(providerResources.languageRootsPresent(), true)
  assert.equal(
    providerResources.relativePath(providerLanguage.english, 'role/manager'),
    'provider/role/manager/en.md',
  )
  assert.equal(
    providerResources.relativePath(providerLanguage.simplifiedChinese, 'role/manager'),
    'provider/role/manager/zh-CN.md',
  )
  assert.equal(providerResources.exists(providerLanguage.english, 'role/manager'), true)
  assert.equal(providerResources.exists(providerLanguage.simplifiedChinese, 'role/manager'), true)
})

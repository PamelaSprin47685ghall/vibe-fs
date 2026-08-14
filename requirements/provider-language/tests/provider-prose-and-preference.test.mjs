// provider-language — 装载 fail-closed + 全局偏好作用域（PROVIDER-LANGUAGE-004/006/007）。
//
// 覆盖 moved tests 没锁的三条行为：
// - ProviderProse.substitute：缺参/残留 placeholder 必须 fail-closed（007）；
// - ProviderResources.requireLanguagePair：缺 locale leaf 必须抛错（006）；
// - languageOf：未绑 → English（HOST-026 首触达），已绑 → 绑定语言；
// - 全局偏好变更只影响未来 session：已绑 session 不重绑，新 session 取新偏好（004）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { languageOf, substitute } from '../../../dist/Infrastructure/Resources/ProviderProse.js'
import {
  ensureInherited,
  ensureRoot,
} from '../../../dist/Infrastructure/OpenCode/Host/ProviderLanguageBinding.js'
import {
  caseOf,
  mapOf,
  providerLanguage,
  providerResources,
  sessionId,
} from '../../verification-system/tests/support/domain.mjs'

const withPreference = async (raw, fn) => {
  const previous = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  if (raw === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = raw
  try {
    return await fn()
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previous
  }
}

test.beforeEach(() => {
  providerLanguage.clearAllForTests()
})

test('substitute_replaces_values_and_fails_closed_on_missing_or_leftover', () => {
  assert.equal(substitute('Hello {{name}}.', mapOf({ name: 'world' })), 'Hello world.')
  // 缺参：模板里的 {{name}} 没有对应值 → 必须抛错，不许留下未替换洞。
  assert.throws(() => substitute('Hello {{name}}.', mapOf({})), /missing substitution/)
  // 部分缺参：替换中途遇到缺失键 → 必须抛错。
  assert.throws(() => substitute('{{a}} then {{b}}.', mapOf({ a: 'x' })), /missing substitution|retained unsubstituted/)
  // 填值不翻译：值原样进入结果。
  assert.equal(
    substitute('Return {{exit_code}}.', mapOf({ exit_code: 'exit_code' })),
    'Return exit_code.',
  )
})

test('require_language_pair_fails_closed_on_missing_semantic_path', () => {
  // 缺 en.md 或 zh-CN.md → 抛错（bound session 缺 localization ≠ 许可换语言）。
  assert.throws(() => providerResources.requireLanguagePair('role/office-that-does-not-exist'), /missing/)
  // 成对存在的真实资源 → 不抛。
  providerResources.requireLanguagePair('role/manager')
})

test('language_of_unbound_session_is_english_and_bound_follows_session', () => {
  const sid = sessionId('ses_prose_unbound')
  assert.equal(caseOf(languageOf(sid)), 'English')

  const bound = providerLanguage.bindOnce(sid, providerLanguage.simplifiedChinese)
  assert.equal(bound.ok, true)
  assert.equal(caseOf(languageOf(sid)), 'SimplifiedChinese')
})

test('preference_change_only_affects_future_sessions', async () => {
  const existing = sessionId('ses_pref_existing')
  const fresh = sessionId('ses_pref_fresh')

  await withPreference('zh-CN', async () => {
    assert.equal(caseOf(ensureRoot(existing)), 'SimplifiedChinese')
    // 全局切到 en：已绑 session 不重绑（bind-once 拒绝异值）。
    return withPreference('en', async () => {
      assert.equal(caseOf(ensureRoot(existing)), 'SimplifiedChinese')
      // 新 session 首触达 → 取新偏好。
      assert.equal(caseOf(ensureRoot(fresh)), 'English')
      // child 继承 owner 语言，不读全局：owner=zh → child=zh，即使全局已是 en。
      const child = sessionId('ses_pref_child')
      assert.equal(caseOf(ensureInherited(existing, child)), 'SimplifiedChinese')
    })
  })
})

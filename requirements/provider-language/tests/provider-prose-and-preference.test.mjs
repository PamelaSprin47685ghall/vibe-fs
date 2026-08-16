// provider-language — 装载 fail-closed + 全局偏好作用域（PROVIDER-LANGUAGE-004/006/007）。
//
// 覆盖 moved tests 没锁的三条行为：
// - ProviderProse.substitute：缺参/残留 placeholder 必须 fail-closed（007）；
// - ProviderResources.requireLanguagePair：缺 locale leaf 必须抛错（006）；
// - languageOf：未绑 → English（HOST-026 首触达），已绑 → 绑定语言（002/004）；
// - 全局偏好变更只影响未来 session：已绑 session 不重绑，新 session 取新偏好（004）；
// - child 继承 owner 语言，不重读全局（003）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { languageOf, substitute } from '../../../dist/Resources/ProviderProse.js'
import {
  ensureInherited,
  ensureRoot,
} from '../../../dist/OpenCode/Host/ProviderLanguageBinding.js'
import {
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

test('WHAT[PROVIDER-LANGUAGE-007] substitute replaces values and fails closed on missing or leftover', () => {
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

test('WHAT[PROVIDER-LANGUAGE-006] require language pair fails closed on missing semantic path', () => {
  // 缺 en.md 或 zh-CN.md → 抛错（bound session 缺 localization ≠ 许可换语言）。
  assert.throws(() => providerResources.requireLanguagePair('role/office-that-does-not-exist'), /missing/)
  // 成对存在的真实资源 → 不抛。
  providerResources.requireLanguagePair('role/manager')
})

test('WHAT[PROVIDER-LANGUAGE-004] unbound session language is English (first touch)', () => {
  const sid = sessionId('ses_prose_unbound')
  assert.equal(providerLanguage.nameOf(languageOf(sid)), 'English')
})

test('WHAT[PROVIDER-LANGUAGE-002] bound session language follows the session binding', () => {
  const sid = sessionId('ses_prose_bound')
  const bound = providerLanguage.bindOnce(sid, providerLanguage.simplifiedChinese)
  assert.equal(bound.ok, true)
  assert.equal(providerLanguage.nameOf(languageOf(sid)), 'SimplifiedChinese')
})

test('WHAT[PROVIDER-LANGUAGE-004] preference change only affects future sessions', async () => {
  const existing = sessionId('ses_pref_existing')
  const fresh = sessionId('ses_pref_fresh')

  await withPreference('zh-CN', async () => {
    assert.equal(providerLanguage.nameOf(ensureRoot(existing)), 'SimplifiedChinese')
    // 全局切到 en：已绑 session 不重绑（bind-once 拒绝异值）。
    return withPreference('en', async () => {
      assert.equal(providerLanguage.nameOf(ensureRoot(existing)), 'SimplifiedChinese')
      // 新 session 首触达 → 取新偏好。
      assert.equal(providerLanguage.nameOf(ensureRoot(fresh)), 'English')
    })
  })
})

test('WHAT[PROVIDER-LANGUAGE-003] child inherits owner language without reading the global preference', async () => {
  const existing = sessionId('ses_pref_existing')

  await withPreference('zh-CN', async () => {
    assert.equal(providerLanguage.nameOf(ensureRoot(existing)), 'SimplifiedChinese')
    // owner=zh → child=zh，即使全局已是 en。
    return withPreference('en', async () => {
      const child = sessionId('ses_pref_child')
      assert.equal(providerLanguage.nameOf(ensureInherited(existing, child)), 'SimplifiedChinese')
    })
  })
})

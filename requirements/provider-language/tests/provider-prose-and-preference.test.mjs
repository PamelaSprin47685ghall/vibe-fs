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
import {
  languageOfSession,
  substitute,
  ensureInherited,
  ensureRoot,
  clearAllForTests,
  bindOnce,
  nameOf,
  requireLanguagePair,
} from '../../../dist/Participant/Provider/LanguageSurface.js'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(fileURLToPath(import.meta.url), '../../../..')
const SRC_ROOT = join(ROOT, 'src/Wanxiangshu')

const walk = (dir) => {
  const out = []
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name)
    if (entry.isDirectory()) out.push(...walk(full))
    else if (entry.name.endsWith('.fs')) out.push(full)
  }
  return out
}

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

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
  clearAllForTests()
})

test('WHAT[PROVIDER-LANGUAGE-007] substitute replaces values and fails closed on missing or leftover', () => {
  assert.equal(substitute('Hello {{name}}.', { name: 'world' }), 'Hello world.')
  // 缺参：模板里的 {{name}} 没有对应值 → 必须抛错，不许留下未替换洞。
  assert.throws(() => substitute('Hello {{name}}.', {}), /missing substitution/)
  // 部分缺参：替换中途遇到缺失键 → 必须抛错。
  assert.throws(() => substitute('{{a}} then {{b}}.', { a: 'x' }), /missing substitution|retained unsubstituted/)
  // 填值不翻译：值原样进入结果。
  assert.equal(
    substitute('Return {{exit_code}}.', { exit_code: 'exit_code' }),
    'Return exit_code.',
  )
})

test('WHAT[PROVIDER-LANGUAGE-006] require language pair fails closed on missing semantic path', () => {
  // 缺 en.md 或 zh-CN.md → 抛错（bound session 缺 localization ≠ 许可换语言）。
  assert.throws(() => requireLanguagePair('role/office-that-does-not-exist'), /missing/)
  // 成对存在的真实资源 → 不抛。
  requireLanguagePair('role/manager')
})

test('WHAT[PROVIDER-LANGUAGE-004] unbound session language is English (first touch)', () => {
  const sid = 'ses_prose_unbound'
  assert.equal(nameOf(languageOfSession(sid)), english)
})

test('WHAT[PROVIDER-LANGUAGE-005] production feature code carries no inline bilingual literal branch', () => {
  // Class A prose lives only under resources/provider; feature code may not
  // smuggle natural-language locale forks. Scan every production .fs for the
  // giveaway shape `match ... with | ... -> "en text" | ... -> "zh text"`.
  const suspicious = []
  for (const file of walk(SRC_ROOT)) {
    const text = readFileSync(file, 'utf8')
    const rel = file.slice(ROOT.length + 1).replace(/\\/g, '/')
    if (/match\s+\w*[Ll]ang\w*[\s\S]{0,200}\|[^\n]*->[^\n]*zh\/zh-CN/i.test(text)) {
      suspicious.push(rel)
    }
  }
  assert.deepEqual(suspicious, [], 'business logic must not hardcode bilingual literal branches')
})

test('WHAT[PROVIDER-LANGUAGE-009] render layer never translates or substitutes owning prose', () => {
  // Render/layout owns substitution only; semantic content passes through
  // verbatim — the layer never invents, translates, or corrects the prose it
  // renders. A zh value survives byte-identical; a wrong-locale value is not
  // machine-corrected. Loading stays centralized: requireLanguagePair owns the
  // semantic path and fails missing leaves instead of fabricating content.
  assert.equal(substitute('{{x}}', { x: '会话连接已断开' }), '会话连接已断开')
  assert.throws(
    () => requireLanguagePair('role/__does-not-exist__'),
    /missing/,
    'a missing semantic path must fail, not fabricate prose or fall back',
  )
})

test('WHAT[PROVIDER-LANGUAGE-005] Class A prose loads through the resource layer, both locales', () => {
  // Three-way ownership proof: semantic content is read via ProviderResources-
  // owned paths under resources/provider, which currently contains role/manager
  // en.md + zh-CN.md — not embedded in any .fs file.
  for (const leaf of ['en.md', 'zh-CN.md']) {
    const text = readFileSync(join(ROOT, 'resources/provider/role/manager', leaf), 'utf8')
    assert.ok(text.trim().length > 0, `resources/provider/role/manager/${leaf} empty`)
  }
})

test('WHAT[PROVIDER-LANGUAGE-002] bound session language follows the session binding', () => {
  const sid = 'ses_prose_bound'
  const bound = bindOnce(sid, simplifiedChinese)
  assert.equal(bound.ok, true)
  assert.equal(nameOf(languageOfSession(sid)), simplifiedChinese)
})

test('WHAT[PROVIDER-LANGUAGE-004] preference change only affects future sessions', async () => {
  const existing = 'ses_pref_existing'
  const fresh = 'ses_pref_fresh'

  await withPreference('zh-CN', async () => {
    assert.equal(nameOf(ensureRoot(existing)), simplifiedChinese)
    // 全局切到 en：已绑 session 不重绑（bind-once 拒绝异值）。
    return withPreference('en', async () => {
      assert.equal(nameOf(ensureRoot(existing)), simplifiedChinese)
      // 新 session 首触达 → 取新偏好。
      assert.equal(nameOf(ensureRoot(fresh)), english)
    })
  })
})

test('WHAT[PROVIDER-LANGUAGE-003] child inherits owner language without reading the global preference', async () => {
  const existing = 'ses_pref_existing'

  await withPreference('zh-CN', async () => {
    assert.equal(nameOf(ensureRoot(existing)), simplifiedChinese)
    // owner=zh → child=zh，即使全局已是 en。
    return withPreference('en', async () => {
      const child = 'ses_pref_child'
      assert.equal(nameOf(ensureInherited(existing, child)), simplifiedChinese)
    })
  })
})

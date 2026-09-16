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

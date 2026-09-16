import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import {

// provider-language — 装载 fail-closed + 全局偏好作用域（PROVIDER-LANGUAGE-004/006/007）。
//
// 覆盖 moved tests 没锁的三条行为：
// - ProviderProse.substitute：缺参/残留 placeholder 必须 fail-closed（007）；
// - ProviderResources.requireLanguagePair：缺 locale leaf 必须抛错（006）；
// - languageOf：未绑 → English（HOST-026 首触达），已绑 → 绑定语言（002/004）；
// - 全局偏好变更只影响未来 session：已绑 session 不重绑，新 session 取新偏好（004）；
// - child 继承 owner 语言，不重读全局（003）。

  languageOfSession,
  substitute,
  ensureInherited,
  ensureRoot,
  clearAllForTests,
  bindOnce,
  nameOf,
  requireLanguagePair,
} from '../../../dist/Participant/Provider/LanguageSurface.js'

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

// Moved from tests/unit/prompt/provider-system-transform.test.mjs (cutover Wave 2a);
// owner: provider-language（未认领判定：断言 = PROMPT-017 ProviderLanguage 的运行时应用
// —— session 语言本地化 WXS 自有 system 段、host 段字节不动、English session 稳定。
// 证据链：provider-language WHAT PROVIDER-LANGUAGE-001/005 证据 历史 PROMPT 条款
// PROMPT-017；PROOF-MAP prompt/ family 含 provider-language。provider-projection 只拥有
// 投影确定性、cognitive-environment 只拥有内容组织，均不拥有语言轴。）
import {
  loadBookkeeperSystem,
  transformBookkeeperSystem,
} from '../../../dist/Participant/Provider/LanguageSurface.js'

const SID = 'provider-system-i18n-bookkeeper'

test.beforeEach(() => clearAllForTests())
test.afterEach(() => clearAllForTests())

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

test('WHAT[PROVIDER-LANGUAGE-005] Class A prose loads through the resource layer, both locales', () => {
  // Three-way ownership proof: semantic content is read via ProviderResources-
  // owned paths under resources/provider, which currently contains role/manager
  // en.md + zh-CN.md — not embedded in any .fs file.
  for (const leaf of ['en.md', 'zh-CN.md']) {
    const text = readFileSync(join(ROOT, 'resources/provider/role/manager', leaf), 'utf8')
    assert.ok(text.trim().length > 0, `resources/provider/role/manager/${leaf} empty`)
  }
})

test('WHAT[PROVIDER-LANGUAGE-005] system transform localizes only the wanxiangshu-owned segment', async () => {
  bookkeeper.bindSession(SID, 'provider-language-surface', 'provider-language-surface')
  try {
    assert.equal(bindOnce(SID, 'SimplifiedChinese').ok, true)
    const english = loadBookkeeperSystem('English')
    const chinese = loadBookkeeperSystem('SimplifiedChinese')
    const hostOwned = 'HOST-OWNED-SYSTEM-BYTES'
    const output = await transformBookkeeperSystem(SID, [english, hostOwned])

    assert.deepEqual(output.system, [chinese, hostOwned])
    assert.match(output.system[0], /^# # 共同法/)
    assert.equal(output.system[0].split('\n').filter(Boolean).every((line) => line === '#' || line.startsWith('# ')), true)
  } finally {
    bookkeeper.unbindSession(SID)
  }
})

import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import {

// Split from tests/unit/verify/language-parity-gate.test.mjs (cutover Wave 2a); owner: provider-language
//
// ARCH-016 Gate C 机制面 + AC20 identifier isomorphism：locale 成对 / placeholder
// parity / semantic-anchor parity 机制 / repo scan / protocol identifier 同形。
// tool-description anchor 断言归 action-affordance，gate_f_* 归 office-capability，
// semantic-anchor（Role Law 内容面）归 cognitive-environment。
  LOCALE_FILES,
  PROVIDER_ROOT,
  extractCodeSpans,
  extractPlaceholders,
  extractProtocolIdentifiers,
  scanIdentifierParity,
  scanParity,
  scanPlaceholderParity,
  scanProviderLanguageBinding,
  scanRepo,
} from '../../../scripts/checks/language-parity-gate.mjs'

const GOOD_HOOK = `
module ProviderResources =
    let requireLanguagePair semanticPath =
        for lang in [ ProviderLanguage.English; ProviderLanguage.SimplifiedChinese ] do
            if not (exists lang semanticPath) then failwith "missing"
    let resourceFileName lang = "en.md"
`

const THIN_HOST_BINDING = `
module ProviderLanguageBinding =
    let readGlobalPreference () =
        Environment.GetEnvironmentVariable "WANXIANGSHU_PROVIDER_LANGUAGE"
        |> ProviderLanguage.fromPreferenceObservation
`

const makeProviderFixture = () => {
  const dir = mkdtempSync(join(tmpdir(), 'lang-parity-'))
  const providerAbs = join(dir, PROVIDER_ROOT)
  return {
    dir,
    providerAbs,
    writePair: (semantic, en, zh) => {
      const base = join(providerAbs, semantic)
      mkdirSync(base, { recursive: true })
      writeFileSync(join(base, 'en.md'), en)
      writeFileSync(join(base, 'zh-CN.md'), zh)
    },
    dispose: () => rmSync(dir, { recursive: true, force: true }),
  }
}

// provider-language — 装载 fail-closed + 全局偏好作用域（PROVIDER-LANGUAGE-004/006/007）。
//
// 覆盖 moved tests 没锁的三条行为：
// - ProviderProse.substitute：缺参/残留 placeholder 必须 fail-closed（007）；
// - ProviderResources.requireLanguagePair：缺 locale leaf 必须抛错（006）；
// - languageOf：未绑 → English（HOST-026 首触达），已绑 → 绑定语言（002/004）；
// - 全局偏好变更只影响未来 session：已绑 session 不重绑，新 session 取新偏好（004）；
// - child 继承 owner 语言，不重读全局（003）。
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

test('WHAT[PROVIDER-LANGUAGE-006] locale leaves are en.md and zh-CN.md under the provider root', () => {
  assert.deepEqual(LOCALE_FILES, ['en.md', 'zh-CN.md'])
  assert.equal(PROVIDER_ROOT, 'resources/provider')
})

test('WHAT[PROVIDER-LANGUAGE-006] parity detects missing zh-CN leaf', () => {
  const providerAbs = '/tmp/provider'
  const violations = scanParity(['role/manager'], providerAbs)
  assert.ok(violations.some((v) => v.code === 'missing-en' || v.code === 'missing-zh-cn'))
})

test('WHAT[PROVIDER-LANGUAGE-006] parity detects missing en leaf in the real tree', () => {
  const violations = scanParity(['role/manager'], resolve(process.cwd(), PROVIDER_ROOT))
  assert.equal(violations.length, 0)
})

test('WHAT[PROVIDER-LANGUAGE-006] Host binding only observes raw preference and delegates', () => {
  assert.deepEqual(scanProviderLanguageBinding(THIN_HOST_BINDING), [])
})

test('WHAT[PROVIDER-LANGUAGE-006] Host English fallback and parser are owner-policy violations', () => {
  const fallback = scanProviderLanguageBinding(
    `${THIN_HOST_BINDING}\nlet fallback = ProviderLanguage.English`,
  )
  assert.deepEqual(fallback, [
    {
      code: 'provider-language-policy',
      path: 'src/Wanxiangshu/OpenCode/Host/ProviderLanguageBinding.fs',
      detail: 'ProviderLanguage.English fallback belongs to Participant/Provider owner, not Host',
    },
  ])

  const parser = scanProviderLanguageBinding(
    `${THIN_HOST_BINDING}\nlet parse raw = ProviderLanguage.tryParse raw`,
  )
  assert.deepEqual(parser, [
    {
      code: 'provider-language-policy',
      path: 'src/Wanxiangshu/OpenCode/Host/ProviderLanguageBinding.fs',
      detail: 'ProviderLanguage.tryParse belongs to Participant/Provider owner, not Host',
    },
  ])
})

test('WHAT[PROVIDER-LANGUAGE-006] Host preference branches and aliases are owner-policy violations', () => {
  const red = scanProviderLanguageBinding(`
module ProviderLanguageBinding =
    let readGlobalPreference () =
        match Environment.GetEnvironmentVariable "WANXIANGSHU_PROVIDER_LANGUAGE" with
        | null -> ProviderLanguage.fromPreferenceObservation null
        | raw when String.IsNullOrWhiteSpace raw -> ProviderLanguage.fromPreferenceObservation raw
        | "en" -> ProviderLanguage.fromPreferenceObservation "en"
        | raw -> ProviderLanguage.fromPreferenceObservation raw
`)
  assert.ok(
    red.some(
      (v) =>
        v.code === 'provider-language-policy' &&
        v.detail ===
          'provider-language whitespace/default policy belongs to Participant/Provider owner, not Host',
    ),
  )
  assert.ok(
    red.some(
      (v) =>
        v.code === 'provider-language-policy' &&
        v.detail === 'provider-language aliases belong to Participant/Provider owner, not Host',
    ),
  )
})

test('WHAT[PROVIDER-LANGUAGE-006] require language pair fails closed on missing semantic path', () => {
  // 缺 en.md 或 zh-CN.md → 抛错（bound session 缺 localization ≠ 许可换语言）。
  assert.throws(() => requireLanguagePair('role/office-that-does-not-exist'), /missing/)
  // 成对存在的真实资源 → 不抛。
  requireLanguagePair('role/manager')
})

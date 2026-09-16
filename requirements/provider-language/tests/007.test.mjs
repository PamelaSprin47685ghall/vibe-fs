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

test('WHAT[PROVIDER-LANGUAGE-007] placeholder parity passes on equal sets', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair('tool/demo', '{{byname}} has returned.', '{{byname}} 已经回来了。')
    assert.deepEqual(scanPlaceholderParity(['tool/demo'], fx.providerAbs), [])
  } finally {
    fx.dispose()
  }
})

test('WHAT[PROVIDER-LANGUAGE-007] placeholder parity mismatch reports diff', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair('tool/demo', '{{byname}} carries {{charge}}.', '{{byname}} 承担托付。')
    const violations = scanPlaceholderParity(['tool/demo'], fx.providerAbs)
    assert.equal(violations.length, 1)
    assert.equal(violations[0].code, 'placeholder-parity')
    assert.equal(violations[0].path, 'resources/provider/tool/demo')
    assert.match(violations[0].detail ?? '', /only-en: \[charge\]/)
  } finally {
    fx.dispose()
  }
})

test('WHAT[PROVIDER-LANGUAGE-007] placeholder extraction dedupes and skips plain text', () => {
  assert.deepEqual([...extractPlaceholders('{{byname}} / {{charge}} / {{byname}}')].sort(), [
    'byname',
    'charge',
  ])
  assert.deepEqual([...extractPlaceholders('no holes')].sort(), [])
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

import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { mkdirSync, mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join, resolve } = await import("node:path");
const { default: test } = await import("node:test");
const { LOCALE_FILES, PROVIDER_ROOT, extractCodeSpans, extractPlaceholders, extractProtocolIdentifiers, scanIdentifierParity, scanParity, scanPlaceholderParity, scanProviderLanguageBinding, scanRepo } = await import("../../../scripts/checks/language-parity-gate.mjs");

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

test('WHAT[provider-language-006] locale leaves are en.md and zh-CN.md under the provider root', () => {
  assert.deepEqual(LOCALE_FILES, ['en.md', 'zh-CN.md'])
  assert.equal(PROVIDER_ROOT, 'resources/provider')
})
test('WHAT[provider-language-006] parity detects missing zh-CN leaf', () => {
  const providerAbs = '/tmp/provider'
  const violations = scanParity(['role/manager'], providerAbs)
  assert.ok(violations.some((v) => v.code === 'missing-en' || v.code === 'missing-zh-cn'))
})
test('WHAT[provider-language-006] parity detects missing en leaf in the real tree', () => {
  const violations = scanParity(['role/manager'], resolve(process.cwd(), PROVIDER_ROOT))
  assert.equal(violations.length, 0)
})
test('WHAT[provider-language-006] Host binding only observes raw preference and delegates', () => {
  assert.deepEqual(scanProviderLanguageBinding(THIN_HOST_BINDING), [])
})
test('WHAT[provider-language-006] Host English fallback and parser are owner-policy violations', () => {
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
test('WHAT[provider-language-006] Host preference branches and aliases are owner-policy violations', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { languageOfSession, substitute, ensureInherited, ensureRoot, clearAllForTests, bindOnce, nameOf, requireLanguagePair } = await import("../../../dist/Participant/Provider/LanguageSurface.js");
const { readFileSync, readdirSync, statSync } = await import("node:fs");
const { join, resolve } = await import("node:path");
const { fileURLToPath } = await import("node:url");

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

test('WHAT[provider-language-006] require language pair fails closed on missing semantic path', () => {
  // 缺 en.md 或 zh-CN.md → 抛错（bound session 缺 localization ≠ 许可换语言）。
  assert.throws(() => requireLanguagePair('role/office-that-does-not-exist'), /missing/)
  // 成对存在的真实资源 → 不抛。
  requireLanguagePair('role/manager')
})
}

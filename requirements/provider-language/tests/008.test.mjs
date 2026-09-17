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

test('WHAT[PROVIDER-LANGUAGE-008] repo scan is green across every semantic surface', () => {
  const result = scanRepo()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { clearAllForTests, readGlobalPreference, parse, tryParse, label, resourceDirectory, bindOnce, inheritFromOwner, tryGet, inheritFrom, languageRootsPresent, relativePath, exists } = await import("../../../dist/Participant/Provider/LanguageSurface.js");

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

test('WHAT[PROVIDER-LANGUAGE-008] bound language loads its own locale leaf', () => {
  assert.equal(exists(english, 'role/manager'), true)
  assert.equal(exists(simplifiedChinese, 'role/manager'), true)
})
}

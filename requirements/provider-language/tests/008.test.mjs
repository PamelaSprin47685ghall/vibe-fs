import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
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

// tests/unit/prompt/provider-language.test.mjs — PROMPT-017 / HOST-026 Phase 2.
//
// ProviderLanguage parse + SessionProviderLanguage bind-once inherit.
// Does not migrate bilingual prose (Phase 17).

import {
  clearAllForTests,
  readGlobalPreference,
  parse,
  tryParse,
  label,
  resourceDirectory,
  bindOnce,
  inheritFromOwner,
  tryGet,
  inheritFrom,
  languageRootsPresent,
  relativePath,
  exists,
} from '../../../dist/Participant/Provider/LanguageSurface.js'

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

test('WHAT[PROVIDER-LANGUAGE-008] repo scan is green across every semantic surface', () => {
  const result = scanRepo()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[PROVIDER-LANGUAGE-008] bound language loads its own locale leaf', () => {
  assert.equal(exists(english, 'role/manager'), true)
  assert.equal(exists(simplifiedChinese, 'role/manager'), true)
})

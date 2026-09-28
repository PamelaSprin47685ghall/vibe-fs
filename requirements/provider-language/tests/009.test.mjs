import assert from 'node:assert/strict'
import test from 'node:test'
import { scanProviderLanguageBinding } from '../../../scripts/checks/language-parity-gate.mjs'
import * as prompts from '../../../dist/Resources/PromptSurface.js'

const thinHost = `
module ProviderLanguageBinding =
    let readGlobalPreference () =
        Environment.GetEnvironmentVariable "WANXIANGSHU_PROVIDER_LANGUAGE"
        |> ProviderLanguage.fromPreferenceObservation
`

test('WHAT[provider-language-009] the existing owner-policy scanner accepts observation and detects duplicated language decisions', () => {
  assert.deepEqual(scanProviderLanguageBinding(thinHost), [])
  for (const policy of [
    'let fallback = ProviderLanguage.English',
    'let parse raw = ProviderLanguage.tryParse raw',
    'let empty raw = String.IsNullOrWhiteSpace raw',
    'let alias = "zh-CN"',
  ]) {
    const violations = scanProviderLanguageBinding(`${thinHost}\n${policy}`)
    assert.ok(violations.some((item) => item.code === 'provider-language-policy'), policy)
  }
})

test.todo('WHAT[provider-language-009] complete Class A loading ownership needs production call-path evidence; a few source patterns cannot establish all prose ownership')

test('WHAT[provider-language-009] package-owned prompt resources load unchanged from another working directory', () => {
  const before = prompts.load()
  const runtimeBefore = prompts.runtimeLoad().Prompts
  assert.ok(Object.values(before).every((text) => typeof text === 'string' && text.trim().length > 0))
  const previous = process.cwd()
  try {
    process.chdir('/')
    assert.deepEqual(prompts.load(), before)
    assert.deepEqual(prompts.runtimeLoad().Prompts, runtimeBefore)
  } finally {
    process.chdir(previous)
  }
})

import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import {
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

test('WHAT[PROVIDER-LANGUAGE-011] code span extraction skips fenced blocks', () => {
  const text = 'Use `exit_code`.\n\n```\ntranslated_should_ignore\n```\nAlso `deadline_seconds`.'
  assert.deepEqual([...extractCodeSpans(text)].sort(), ['deadline_seconds', 'exit_code'])
})

test('WHAT[PROVIDER-LANGUAGE-011] identifier parity passes when both locales keep the same spans', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair(
      'library/demo',
      'Choose `deadline_seconds = 120` and keep `exit_code`.',
      '选择 `deadline_seconds = 120`，并保留 `exit_code`。',
    )
    const violations = scanIdentifierParity(['library/demo'], fx.providerAbs)
    assert.deepEqual(violations, [])
  } finally {
    fx.dispose()
  }
})

test('WHAT[PROVIDER-LANGUAGE-011] identifier parity mismatch reports semantic and diff', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair(
      'role/demo',
      'Wire field `exit_code` must stay.',
      'Wire field `退出码` must stay.',
    )
    const violations = scanIdentifierParity(['role/demo'], fx.providerAbs)
    assert.equal(violations.length, 1)
    assert.equal(violations[0].code, 'identifier-parity')
    assert.equal(violations[0].path, 'resources/provider/role/demo')
    assert.match(violations[0].detail ?? '', /only-en: \[exit_code\]/)
    assert.match(violations[0].detail ?? '', /only-zh-CN: \[退出码\]/)
  } finally {
    fx.dispose()
  }
})

test('WHAT[PROVIDER-LANGUAGE-011] tip and tool catalog hits must match across locales', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair(
      'role/tip-demo',
      'Avoid blind-edit and call open-terminal.',
      '避免 blind-edit，并调用 open-terminal。',
    )
    const catalogs = {
      tipIdentities: ['blind-edit'],
      toolNames: ['open-terminal'],
    }
    assert.deepEqual(scanIdentifierParity(['role/tip-demo'], fx.providerAbs, catalogs), [])

    fx.writePair(
      'role/tip-demo',
      'Avoid blind-edit and call open-terminal.',
      '避免 盲目编辑，并调用 打开终端。',
    )
    const red = scanIdentifierParity(['role/tip-demo'], fx.providerAbs, catalogs)
    assert.equal(red.length, 1)
    assert.equal(red[0].path, 'resources/provider/role/tip-demo')
    assert.match(red[0].detail ?? '', /only-en: \[blind-edit, open-terminal\]/)
    assert.match(red[0].detail ?? '', /only-zh-CN: \[\]/)
  } finally {
    fx.dispose()
  }
})

test('WHAT[PROVIDER-LANGUAGE-011] protocol identifier extraction unions sources', () => {
  const ids = extractProtocolIdentifiers('See `exit_code` then blind-edit via open-terminal.', {
    tipIdentities: ['blind-edit'],
    toolNames: ['open-terminal'],
  })
  assert.deepEqual([...ids].sort(), ['blind-edit', 'exit_code', 'open-terminal'])
})

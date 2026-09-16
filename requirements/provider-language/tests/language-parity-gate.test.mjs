// Split from tests/unit/verify/language-parity-gate.test.mjs (cutover Wave 2a); owner: provider-language
//
// ARCH-016 Gate C 机制面 + AC20 identifier isomorphism：locale 成对 / placeholder
// parity / semantic-anchor parity 机制 / repo scan / protocol identifier 同形。
// tool-description anchor 断言归 action-affordance，gate_f_* 归 office-capability，
// semantic-anchor（Role Law 内容面）归 cognitive-environment。
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

test('WHAT[PROVIDER-LANGUAGE-008] repo scan is green across every semantic surface', () => {
  const result = scanRepo()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

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


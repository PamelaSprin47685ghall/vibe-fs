/**
 * ARCH-016 Gate C — provider language parity + AC20 identifier isomorphism.
 */
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
  listSemanticResourceDirs,
  scanIdentifierParity,
  scanParity,
  scanPlaceholderParity,
  scanProviderResourcesHook,
  scanRepo,
} from '../../../scripts/checks/language-parity-gate.mjs'

const GOOD_HOOK = `
module ProviderResources =
    let requireLanguagePair semanticPath =
        for lang in [ ProviderLanguage.English; ProviderLanguage.SimplifiedChinese ] do
            if not (exists lang semanticPath) then failwith "missing"
    let resourceFileName lang = "en.md"
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

test('gate_c_documents_locale_leaves', () => {
  assert.deepEqual(LOCALE_FILES, ['en.md', 'zh-CN.md'])
  assert.equal(PROVIDER_ROOT, 'resources/provider')
})

test('gate_c_parity_detects_missing_zh_cn', () => {
  const providerAbs = '/tmp/provider'
  const violations = scanParity(['role/manager'], providerAbs)
  assert.ok(violations.some((v) => v.code === 'missing-en' || v.code === 'missing-zh-cn'))
})

test('gate_c_parity_detects_missing_en', () => {
  const violations = scanParity(['role/manager'], resolve(process.cwd(), PROVIDER_ROOT))
  assert.equal(violations.length, 0)
})

test('gate_c_provider_resources_hook_required', () => {
  assert.equal(scanProviderResourcesHook(GOOD_HOOK).length, 0)
  assert.ok(scanProviderResourcesHook('module ProviderResources = let x = 1').some((v) => v.code === 'missing-require-language-pair'))
})

test('gate_c_repo_lists_role_semantic_dirs', () => {
  const root = resolve(process.cwd())
  const semanticDirs = listSemanticResourceDirs(resolve(root, PROVIDER_ROOT))
  assert.ok(semanticDirs.includes('role/manager'))
  assert.ok(semanticDirs.includes('role/coder'))
})

test('gate_c_repo_scan_is_green', () => {
  const result = scanRepo()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('ac20_extract_code_spans_skips_fenced_blocks', () => {
  const text = 'Use `exit_code`.\n\n```\ntranslated_should_ignore\n```\nAlso `deadline_seconds`.'
  assert.deepEqual([...extractCodeSpans(text)].sort(), ['deadline_seconds', 'exit_code'])
})

test('ac20_identifier_parity_equal_spans_pass', () => {
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

test('ac20_identifier_parity_mismatch_reports_semantic_and_diff', () => {
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

test('ac20_tip_and_tool_catalog_hits_must_match', () => {
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

test('ac20_extract_protocol_identifiers_unions_sources', () => {
  const ids = extractProtocolIdentifiers('See `exit_code` then blind-edit via open-terminal.', {
    tipIdentities: ['blind-edit'],
    toolNames: ['open-terminal'],
  })
  assert.deepEqual([...ids].sort(), ['blind-edit', 'exit_code', 'open-terminal'])
})

test('gate_c_placeholder_parity_equal_sets_pass', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair('tool/demo', '{{byname}} has returned.', '{{byname}} 已经回来了。')
    assert.deepEqual(scanPlaceholderParity(['tool/demo'], fx.providerAbs), [])
  } finally {
    fx.dispose()
  }
})

test('gate_c_placeholder_parity_mismatch_reports_diff', () => {
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

test('gate_c_extract_placeholders', () => {
  assert.deepEqual([...extractPlaceholders('{{byname}} / {{charge}} / {{byname}}')].sort(), [
    'byname',
    'charge',
  ])
  assert.deepEqual([...extractPlaceholders('no holes')].sort(), [])
})

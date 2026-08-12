/**
 * ARCH-016 Gate C — provider language parity (no dist).
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  EN_ROOT,
  ZH_ROOT,
  listRelativeFiles,
  scanParity,
  scanProviderResourcesHook,
  scanRepo,
} from '../../../scripts/checks/language-parity-gate.mjs'
import { resolve } from 'node:path'

const GOOD_HOOK = `
module ProviderResources =
    let requireLanguagePair semanticPath =
        for lang in [ ProviderLanguage.English; ProviderLanguage.SimplifiedChinese ] do
            if not (exists lang semanticPath) then failwith "missing"
`

test('gate_c_documents_language_roots', () => {
  assert.equal(EN_ROOT, 'resources/provider/en')
  assert.equal(ZH_ROOT, 'resources/provider/zh-CN')
})

test('gate_c_parity_detects_missing_zh_cn', () => {
  const violations = scanParity(['README.md', 'tools/join.md'], ['README.md'])
  assert.ok(violations.some((v) => v.code === 'missing-zh-cn' && v.detail?.includes('tools/join.md')))
})

test('gate_c_parity_detects_missing_en', () => {
  const violations = scanParity(['README.md'], ['README.md', 'tools/join.md'])
  assert.ok(violations.some((v) => v.code === 'missing-en'))
})

test('gate_c_provider_resources_hook_required', () => {
  assert.equal(scanProviderResourcesHook(GOOD_HOOK).length, 0)
  assert.ok(scanProviderResourcesHook('module ProviderResources = let x = 1').some((v) => v.code === 'missing-require-language-pair'))
})

test('gate_c_repo_language_roots_have_matching_files', () => {
  const root = resolve(process.cwd())
  const en = listRelativeFiles(resolve(root, EN_ROOT))
  const zh = listRelativeFiles(resolve(root, ZH_ROOT))
  assert.deepEqual(en, zh)
})

test('gate_c_repo_scan_is_green', () => {
  const result = scanRepo()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

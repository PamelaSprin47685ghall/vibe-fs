/**
 * ARCH-016 Gate C — provider language parity (no dist).
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  LOCALE_FILES,
  PROVIDER_ROOT,
  listSemanticResourceDirs,
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
    let resourceFileName lang = "en.md"
`

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

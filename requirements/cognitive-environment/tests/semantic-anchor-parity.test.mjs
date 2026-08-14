// Split from tests/unit/verify/language-parity-gate.test.mjs (cutover Wave 2a); owner: cognitive-environment
//
// ARCH-016 Gate C semantic-anchor 面（Role Law 内容义务，CE 002/010）：Role Law 语义锚点
// 必须双语同 id 命中，且每个 role 目录必须在 catalog 内。anchor-parity 机制实现归
// provider-language；本包钉「内容必须双语同 ID」的义务面。
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  PROVIDER_ROOT,
  scanSemanticAnchorCatalog,
  scanSemanticAnchorParity,
} from '../../../scripts/checks/language-parity-gate.mjs'

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

test('gate_c_semantic_anchor_parity_detects_missing_zh_id', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair('role/demo', 'Waiting is justified by dependency.', '等待是一种习惯。')
    const catalog = {
      demo: [{ id: 'waiting-by-dependency', en: /justified by dependency/i, zh: /等待由依赖证明/ }],
    }
    const violations = scanSemanticAnchorParity(fx.providerAbs, catalog)
    assert.equal(violations.length, 1)
    assert.equal(violations[0].code, 'semantic-anchor')
    assert.match(violations[0].path, /zh-CN\.md$/)
    assert.match(violations[0].detail ?? '', /waiting-by-dependency/)
  } finally {
    fx.dispose()
  }
})

test('gate_c_semantic_anchor_catalog_requires_every_role_law', () => {
  const violations = scanSemanticAnchorCatalog(['role/manager', 'role/unknown-office'])
  assert.ok(violations.some((v) => v.code === 'semantic-anchor-catalog' && /unknown-office/.test(v.path)))
  assert.equal(
    violations.filter((v) => /role\/manager$/.test(v.path)).length,
    0,
  )
})

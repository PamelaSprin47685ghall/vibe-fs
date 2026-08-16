// Split from tests/unit/verify/language-parity-gate.test.mjs (cutover Wave 2a); owner: action-affordance
//
// ARCH-016 Gate C tool-description anchor 面（AA 002/013）：high-risk verb 必须携带
// semantic anchor catalog，且双语文档同 id 命中。机制实现与其余 gate_c_* 断言在
// provider-language 包测试内。
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  PROVIDER_ROOT,
  scanToolDescriptionAnchorCatalog,
  scanToolDescriptionAnchorParity,
} from '../../../scripts/checks/language-parity-gate.mjs'
import { TOOL_DESCRIPTION_ANCHORS } from '../../../scripts/checks/semantic-anchors.mjs'

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

test('WHAT[ACTION-AFFORDANCE-013] gate_c_tool_description_anchor_parity_detects_missing_zh_id', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair(
      'tool/fork/description',
      'Entrust bounded work to another office within this mission. Coder / Engineer Changes repository source.',
      '把工作交给另一个职位。',
    )
    const catalog = {
      fork: [{ id: 'coder-mutation', en: /Changes repository source/i, zh: /改变 repository source/ }],
    }
    const violations = scanToolDescriptionAnchorParity(fx.providerAbs, catalog)
    assert.equal(violations.length, 1)
    assert.equal(violations[0].code, 'tool-description-anchor')
    assert.match(violations[0].path, /zh-CN\.md$/)
    assert.match(violations[0].detail ?? '', /coder-mutation/)
  } finally {
    fx.dispose()
  }
})

test('WHAT[ACTION-AFFORDANCE-002] gate_c_tool_description_anchor_catalog_requires_high_risk_verbs', () => {
  const highRisk = [
    'commission',
    'establish-behavior',
    'fork',
    'inspect',
    'query-shell',
    'repair-behavior',
    'run',
  ]
  for (const tool of highRisk) {
    assert.ok(tool in TOOL_DESCRIPTION_ANCHORS, `anchor catalog must include ${tool}`)
  }
  assert.equal(
    Object.getOwnPropertyNames(TOOL_DESCRIPTION_ANCHORS).length,
    highRisk.length,
    'anchor catalog must contain exactly the high-risk minimum set',
  )
  assert.ok(TOOL_DESCRIPTION_ANCHORS.inspect.some((a) => a.id === 'no-implement-or-repair'))
  const violations = scanToolDescriptionAnchorCatalog(['tool/inspect/description'])
  const missing = violations
    .filter((v) => v.code === 'tool-description-anchor-catalog')
    .map((v) => v.path)
    .sort()
  assert.ok(missing.some((p) => /tool\/fork\/description$/.test(p)))
  assert.ok(missing.some((p) => /tool\/commission\/description$/.test(p)))
  assert.ok(missing.some((p) => /tool\/establish-behavior\/description$/.test(p)))
  assert.ok(missing.some((p) => /tool\/repair-behavior\/description$/.test(p)))
  assert.ok(missing.some((p) => /tool\/query-shell\/description$/.test(p)))
  assert.ok(missing.some((p) => /tool\/run\/description$/.test(p)))
  assert.equal(
    violations.filter((v) => /tool\/inspect\/description$/.test(v.path)).length,
    0,
  )
})

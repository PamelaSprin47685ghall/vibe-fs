/**
 * EXTERNAL-INVESTIGATION-011 — 外部事实不自动产生 repository/product obligation。
 *
 * 本包无 F# observation 类型，负边界落在 Browser Role Law。本 canary 锁实质区分
 * `observation-not-obligation`：双语命中；删区分 → 红；反面句子（把网上的「应该」
 * 直接当成仓库义务）不得命中。义务产生路径仍归 office-capability / obligation-ledger。
 */
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { BROWSER_OBLIGATION_BOUNDARY_ANCHORS } from '../../../scripts/checks/semantic-anchors.mjs'
import {
  PROVIDER_ROOT,
  scanSemanticAnchorParity,
} from '../../../scripts/checks/language-parity-gate.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const realProvider = resolve(here, '../../../resources/provider')
const [ANCHOR] = BROWSER_OBLIGATION_BOUNDARY_ANCHORS.browser

const EN_FIXTURE = `# The Far Shore

Observation is not obligation.
Facts from the far shore do not mint a repository or product obligation.
`

const ZH_FIXTURE = `# 远岸

观察不是义务。
远岸事实不铸造 repository 或 product obligation。
`

const makeFixture = () => {
  const dir = mkdtempSync(join(tmpdir(), 'facts-not-obligations-'))
  const roleAbs = join(dir, PROVIDER_ROOT, 'role', 'browser')
  mkdirSync(roleAbs, { recursive: true })
  writeFileSync(join(roleAbs, 'en.md'), EN_FIXTURE)
  writeFileSync(join(roleAbs, 'zh-CN.md'), ZH_FIXTURE)
  return { providerAbs: join(dir, PROVIDER_ROOT), dispose: () => rmSync(dir, { recursive: true, force: true }) }
}

test('EXTERNAL-INVESTIGATION-011 observation-not-obligation is pinned', () => {
  assert.equal(ANCHOR.id, 'observation-not-obligation')
  assert.deepEqual(
    BROWSER_OBLIGATION_BOUNDARY_ANCHORS.browser.map((a) => a.id),
    ['observation-not-obligation'],
  )
})

test('EXTERNAL-INVESTIGATION-011 Role Law hits observation-not-obligation in both locales', () => {
  const violations = scanSemanticAnchorParity(realProvider, BROWSER_OBLIGATION_BOUNDARY_ANCHORS)
  assert.deepEqual(violations, [], JSON.stringify(violations, null, 2))
})

test('EXTERNAL-INVESTIGATION-011 removing the distinction turns red', () => {
  const fx = makeFixture()
  try {
    const enPath = join(fx.providerAbs, 'role', 'browser', 'en.md')
    const zhPath = join(fx.providerAbs, 'role', 'browser', 'zh-CN.md')
    writeFileSync(enPath, '# The Far Shore\n\nBring back what the shore showed you.\n')
    writeFileSync(zhPath, '# 远岸\n\n带回远岸显示的东西。\n')
    const violations = scanSemanticAnchorParity(fx.providerAbs, BROWSER_OBLIGATION_BOUNDARY_ANCHORS)
    const missing = violations.filter((v) => v.code === 'semantic-anchor' && /observation-not-obligation/.test(v.detail ?? ''))
    assert.equal(missing.length, 2, JSON.stringify(violations, null, 2))
  } finally {
    fx.dispose()
  }
})

test('EXTERNAL-INVESTIGATION-011 is not a word-level obligation regex', () => {
  assert.doesNotMatch(
    'A web finding that the project should change is therefore a repository obligation.',
    ANCHOR.en,
  )
  assert.doesNotMatch('Mint the web should as a product obligation.', ANCHOR.en)
  assert.doesNotMatch('网上的「应该改」因此就是一条 repository obligation。', ANCHOR.zh)
  assert.doesNotMatch('把外部可能性直接变成仓库义务。', ANCHOR.zh)
})

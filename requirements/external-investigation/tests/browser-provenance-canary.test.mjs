/**
 * Browser provenance canary — `external-investigation` package-owned proof (Oracle 1).
 *
 * 边界（必须读）：
 * - 真实 runtime provenance oracle 需要 browser MCP adapter / Long Stroke：真实 browsing 在
 *   外部 `stealth-browser-mcp`，Wanxiangshu 只注入服务器 + 按角色锁。它落在无 browser 的
 *   unit 套件之外，不在本文件内模拟。
 * - role-lock（只有 Browser 有 `stealth-browser-mcp_*` allow）由本包
 *   `tests/stealth-browser-role-lock.test.mjs` 的
 *   `WHAT[EXTERNAL-INVESTIGATION-010] browser_is_the_only_network_office` 覆盖，本 canary 不重复。
 * - 本 canary 锁的是 contract 的「实质区分」：8 条 provenance 锚点必须在双语 Role Law 中
 *   同 id 命中（结构 parity 机制来自 provider-language 的 Gate C），且 `disagreement-not-averaged`
 *   不是单词级正则——若合同退化成反面（例如「Just average the disagreement.」也算命中），
 *   本测试必须红。
 */
import assert from 'node:assert/strict'
import {
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { ROLE_SEMANTIC_ANCHORS } from '../../../scripts/checks/semantic-anchors.mjs'
import {
  PROVIDER_ROOT,
  scanSemanticAnchorParity,
} from '../../../scripts/checks/language-parity-gate.mjs'

const here = dirname(fileURLToPath(import.meta.url))

/** Oracle 1 pinned ids — 删除或新增锚点必须先改这里（pin 是冻结清单，不是示例）。 */
const PINNED_BROWSER_PROVENANCE_IDS = Object.freeze([
  'provenance-not-reachability',
  'far-shore',
  'source-closest',
  'visual-truth',
  'condition-preserved',
  'inference-not-observation',
  'disagreement-not-averaged',
  'no-cross-sea-certainty',
])

/** 真实 provider 资源树根（仓库内 `resources/provider`）。 */
const realProvider = resolve(here, '../../../resources/provider')

/** 按 id 取 browser 锚点；不存在则断言失败（锚点目录是合同的一部分）。 */
const anchorById = (id) => {
  const found = ROLE_SEMANTIC_ANCHORS.browser.filter((a) => a.id === id)
  assert.equal(found.length, 1, `browser anchor ${id} must exist in ROLE_SEMANTIC_ANCHORS`)
  return found[0]
}

/** 单锚点双语命中扫描：该锚点在真实 Role Law en.md 与 zh-CN.md 都必须命中。 */
const assertAnchorHitsBothLocales = (id) => {
  const violations = scanSemanticAnchorParity(realProvider, {
    browser: [anchorById(id)],
  })
  assert.deepEqual(violations, [], JSON.stringify(violations, null, 2))
}

const BROWSER_EN_FIXTURE = `# The Far Shore

Reachability does not determine ownership. Provenance does.

Prefer the source closest to the fact.

Some truths are only visible. Read visual evidence when the charge depends on what appears.

Preserve the conditions that make a fact true. Carry the condition with the claim.

Inference is not a second observation. Do not promote a plausible inference into a witnessed fact.

Disagreement is not a confidence average. Do not average conflicting authorities.

Do not cross the sea with more certainty than you found on the other shore.
`

const BROWSER_ZH_FIXTURE = `# 远岸

Reachability 并不决定 ownership。Provenance 才决定。

优先选择最接近事实本身的来源。

有些事实只有看见才成立。当 charge 依赖“出现了什么”时，读取视觉证据。

保留使事实成立的条件。把条件与主张一起带走。

Inference 不是第二次 observation。不要把看似合理的推断升格为已被见证的事实。

分歧不是置信度的平均。不要把互相冲突的权威平均。

不要带着比远岸本身提供得更多的确定性渡海归来。
`

const makeBrowserFixture = () => {
  const dir = mkdtempSync(join(tmpdir(), 'browser-provenance-canary-'))
  const roleAbs = join(dir, PROVIDER_ROOT, 'role', 'browser')
  mkdirSync(roleAbs, { recursive: true })
  writeFileSync(join(roleAbs, 'en.md'), BROWSER_EN_FIXTURE)
  writeFileSync(join(roleAbs, 'zh-CN.md'), BROWSER_ZH_FIXTURE)
  return { dir, providerAbs: join(dir, PROVIDER_ROOT), dispose: () => rmSync(dir, { recursive: true, force: true }) }
}

test('WHAT[EXTERNAL-INVESTIGATION-001] provenance contract is stated in Role Law in both locales', () => {
  // 001 规范：采集必须带 provenance——来源 / 时间 / 不确定性，足以支撑 claim。
  // 真实 Role Law「Provenance, compression, and certainty」节必须陈述该合同
  // （canonical location + version/date + condition that binds）。
  const en = readFileSync(join(realProvider, 'role', 'browser', 'en.md'), 'utf8')
  const zh = readFileSync(join(realProvider, 'role', 'browser', 'zh-CN.md'), 'utf8')
  assert.match(en, /## Provenance, compression, and certainty/)
  assert.match(en, /Bring back the fact and enough of its provenance/i)
  assert.match(en, /canonical location/i)
  assert.match(en, /the relevant\s+version or date/i)
  assert.match(en, /the condition that binds the claim/i)
  assert.match(zh, /## Provenance、压缩与确定性/)
  assert.match(zh, /带回事实，也带回足够的 provenance/)
  assert.match(zh, /canonical location/)
  assert.match(zh, /相关的 version 或 date/)
  assert.match(zh, /约束主张的条件/)
})

test('WHAT[EXTERNAL-INVESTIGATION-002] browser_provenance_anchor_ids_are_pinned_to_the_eight_distinctions', () => {
  const actual = ROLE_SEMANTIC_ANCHORS.browser.map((a) => a.id).sort()
  assert.deepEqual(actual, [...PINNED_BROWSER_PROVENANCE_IDS].sort())
})

test('WHAT[EXTERNAL-INVESTIGATION-002] provenance-not-reachability anchor hits real Role Law in both locales', () => {
  assertAnchorHitsBothLocales('provenance-not-reachability')
})

test('WHAT[EXTERNAL-INVESTIGATION-003] far-shore anchor hits real Role Law in both locales', () => {
  assertAnchorHitsBothLocales('far-shore')
})

test('WHAT[EXTERNAL-INVESTIGATION-004] source-closest anchor hits real Role Law in both locales', () => {
  assertAnchorHitsBothLocales('source-closest')
})

test('WHAT[EXTERNAL-INVESTIGATION-005] visual-truth anchor hits real Role Law in both locales', () => {
  assertAnchorHitsBothLocales('visual-truth')
})

test('WHAT[EXTERNAL-INVESTIGATION-006] condition-preserved anchor hits real Role Law in both locales', () => {
  assertAnchorHitsBothLocales('condition-preserved')
})

test('WHAT[EXTERNAL-INVESTIGATION-007] inference-not-observation anchor hits real Role Law in both locales', () => {
  assertAnchorHitsBothLocales('inference-not-observation')
})

test('WHAT[EXTERNAL-INVESTIGATION-007] removing_one_distinction_from_the_fixture_turns_red', () => {
  const fx = makeBrowserFixture()
  try {
    // 删掉 inference-not-observation 的 en + zh 区分行 → 扫描必须报 missing。
    const enPath = join(fx.providerAbs, 'role', 'browser', 'en.md')
    const zhPath = join(fx.providerAbs, 'role', 'browser', 'zh-CN.md')
    const dropEn = (text) =>
      text
        .replace(
          'Inference is not a second observation. Do not promote a plausible inference into a witnessed fact.',
          '',
        )
        .replace(/\n{3,}/g, '\n\n')
    const dropZh = (text) =>
      text
        .replace('Inference 不是第二次 observation。不要把看似合理的推断升格为已被见证的事实。', '')
        .replace(/\n{3,}/g, '\n\n')
    writeFileSync(enPath, dropEn(BROWSER_EN_FIXTURE))
    writeFileSync(zhPath, dropZh(BROWSER_ZH_FIXTURE))

    const violations = scanSemanticAnchorParity(fx.providerAbs, {
      browser: ROLE_SEMANTIC_ANCHORS.browser,
    })
    const missing = violations.filter((v) => v.code === 'semantic-anchor' && /inference-not-observation/.test(v.detail ?? ''))
    assert.equal(missing.length, 2, JSON.stringify(violations, null, 2))
    assert.ok(missing.some((v) => /en\.md$/.test(v.path)))
    assert.ok(missing.some((v) => /zh-CN\.md$/.test(v.path)))
  } finally {
    fx.dispose()
  }
})

test('WHAT[EXTERNAL-INVESTIGATION-008] disagreement-not-averaged anchor hits real Role Law in both locales', () => {
  assertAnchorHitsBothLocales('disagreement-not-averaged')
})

test('WHAT[EXTERNAL-INVESTIGATION-008] disagreement_not_averaged_is_not_a_word_level_regex', () => {
  const [anchor] = ROLE_SEMANTIC_ANCHORS.browser.filter((a) => a.id === 'disagreement-not-averaged')
  assert.ok(anchor, 'disagreement-not-averaged must stay in the browser catalog')
  // 反面句子只含单词「average the disagreement」→ 不得命中；否则合同退化成单词级。
  assert.doesNotMatch('Just average the disagreement.', anchor.en)
  assert.doesNotMatch('Just average the disagreement across sources.', anchor.en)
  assert.doesNotMatch('把分歧平均一下。', anchor.zh)
  assert.doesNotMatch('对互相冲突的权威做平均。', anchor.zh)
})

test('WHAT[EXTERNAL-INVESTIGATION-009] no-cross-sea-certainty anchor hits real Role Law in both locales', () => {
  assertAnchorHitsBothLocales('no-cross-sea-certainty')
})

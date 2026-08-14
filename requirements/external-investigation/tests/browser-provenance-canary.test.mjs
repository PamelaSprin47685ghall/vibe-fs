/**
 * Browser provenance canary — `external-investigation` package-owned proof (Oracle 1).
 *
 * 边界（必须读）：
 * - 真实 runtime provenance oracle 需要 browser MCP adapter / Long Stroke：真实 browsing 在
 *   外部 `stealth-browser-mcp`，Wanxiangshu 只注入服务器 + 按角色锁。它落在无 browser 的
 *   unit 套件之外，不在本文件内模拟。
 * - role-lock（只有 Browser 有 `stealth-browser-mcp_*` allow）已由
 *   `tests/unit/agent/stealth-browser-mcp.test.mjs` 的
 *   `AGENT_026_browser_only_wildcard_permission` 覆盖（capability-enforcement 交叉），
 *   本 canary 不重复。
 * - 本 canary 锁的是 contract 的「实质区分」：8 条 provenance 锚点必须在双语 Role Law 中
 *   同 id 命中（结构 parity 机制来自 provider-language 的 Gate C），且 `disagreement-not-averaged`
 *   不是单词级正则——若合同退化成反面（例如「Just average the disagreement.」也算命中），
 *   本测试必须红。
 */
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
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

test('browser_provenance_anchor_ids_are_pinned_to_the_eight_distinctions', () => {
  const actual = ROLE_SEMANTIC_ANCHORS.browser.map((a) => a.id).sort()
  assert.deepEqual(actual, [...PINNED_BROWSER_PROVENANCE_IDS].sort())
})

test('browser_provenance_anchors_hit_real_role_law_in_both_locales', () => {
  const violations = scanSemanticAnchorParity(realProvider, {
    browser: ROLE_SEMANTIC_ANCHORS.browser,
  })
  assert.deepEqual(violations, [], JSON.stringify(violations, null, 2))
})

test('removing_one_distinction_from_the_fixture_turns_red', () => {
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

test('disagreement_not_averaged_is_not_a_word_level_regex', () => {
  const [anchor] = ROLE_SEMANTIC_ANCHORS.browser.filter((a) => a.id === 'disagreement-not-averaged')
  assert.ok(anchor, 'disagreement-not-averaged must stay in the browser catalog')
  // 反面句子只含单词「average the disagreement」→ 不得命中；否则合同退化成单词级。
  assert.doesNotMatch('Just average the disagreement.', anchor.en)
  assert.doesNotMatch('Just average the disagreement across sources.', anchor.en)
  assert.doesNotMatch('把分歧平均一下。', anchor.zh)
  assert.doesNotMatch('对互相冲突的权威做平均。', anchor.zh)
})

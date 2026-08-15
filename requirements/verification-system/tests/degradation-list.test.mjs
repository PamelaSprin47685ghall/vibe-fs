// requirements/verification-system/tests/degradation-list.test.mjs — VERIFY-004 「禁止退化清单」.
//
// W7 has to give every forbidden degradation a gate case, which first requires
// knowing what the items ARE without a human retyping them. This locks the reader
// that answers that question: the thirteen items, in SSOT order, whole strings,
// plus the id each downstream case will name.
//
// The fail-closed tests are the load-bearing ones. `degradation-list.mjs` feeds a
// completeness gate ("every item has a case"), and a completeness gate over an
// empty list passes trivially — design-script-forest.md:630, 「一个能对错误实现给出
// 绿灯的验证装置，比没有验证装置更危险」. So the parser returning `[]` on a
// restructured SSOT would be worse than deleting W7 outright.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  ANCHOR,
  DEGRADATIONS,
  SSOT_ORIGIN,
  parseDegradations,
} from './e2e/support/degradation-list.mjs'

/**
 * The thirteen items as they stand in requirements/verification-system/WHAT.md today.
 *
 * Written out here on purpose, and it is NOT a second source of truth: the
 * production path reads the SSOT, and this array's only job is to fail when that
 * text changes. A count check would not do it — an edited item keeps the count.
 */
const EXPECTED_TEXTS = [
  '把 wall-clock 总超时当作唯一挂死判据',
  '让原始 SSE 或 provider 流量续期 watchdog',
  '让背景车道进展续期 watchdog',
  '删除 watchdog 的诊断转储，只保留退出码',
  '让 watchdog 计时器持有事件循环，使干净结束也要等满静默窗口',
  '存在只有总超时保护的时间窗',
  '声明了断言心跳但未接线',
  '用固定 sleep 代替因果 bark 交错启动',
  '就绪超时或就绪前退出被当作通过',
  'Release gate 变成「最多 N 轮」或「重跑直到通过」',
  '数量常量与清单各自维护',
  '静态门禁的路径判据指向不存在的目录',
  '延长静默窗口或测试超时以掩盖竞态',
]

/**
 * The id↔text binding, asserted as pairs rather than as two lists.
 *
 * A downstream case names an id. Nothing about the id itself proves the case is
 * about the degradation it claims, so the proof has to live somewhere; this is it.
 */
const EXPECTED_PAIRS = [
  ['VERIFY_004_D_WALL_CLOCK_AS_ONLY_HANG_CRITERION', '把 wall-clock 总超时当作唯一挂死判据'],
  ['VERIFY_004_D_RAW_TRAFFIC_RENEWS_WATCHDOG', '让原始 SSE 或 provider 流量续期 watchdog'],
  ['VERIFY_004_D_BACKGROUND_LANE_RENEWS_WATCHDOG', '让背景车道进展续期 watchdog'],
  ['VERIFY_004_D_WATCHDOG_DUMP_REDUCED_TO_EXIT_CODE', '删除 watchdog 的诊断转储，只保留退出码'],
  [
    'VERIFY_004_D_WATCHDOG_TIMER_HOLDS_EVENT_LOOP',
    '让 watchdog 计时器持有事件循环，使干净结束也要等满静默窗口',
  ],
  ['VERIFY_004_D_WINDOW_GUARDED_ONLY_BY_TOTAL_TIMEOUT', '存在只有总超时保护的时间窗'],
  ['VERIFY_004_D_DECLARED_HEARTBEAT_NOT_WIRED', '声明了断言心跳但未接线'],
  ['VERIFY_004_D_FIXED_SLEEP_REPLACES_CAUSAL_BARK', '用固定 sleep 代替因果 bark 交错启动'],
  ['VERIFY_004_D_READY_TIMEOUT_OR_EARLY_EXIT_PASSES', '就绪超时或就绪前退出被当作通过'],
  ['VERIFY_004_D_RELEASE_GATE_BECOMES_AT_MOST_N_ROUNDS', 'Release gate 变成「最多 N 轮」或「重跑直到通过」'],
  ['VERIFY_004_D_COUNT_CONSTANT_MAINTAINED_APART_FROM_LIST', '数量常量与清单各自维护'],
  ['VERIFY_004_D_STATIC_GATE_PATH_DOES_NOT_EXIST', '静态门禁的路径判据指向不存在的目录'],
  ['VERIFY_004_D_WINDOW_WIDENED_TO_HIDE_A_RACE', '延长静默窗口或测试超时以掩盖竞态'],
]

/** A minimal SSOT-shaped document, so the fail-closed paths need no temp files. */
const syntheticSource = ({ heading = ANCHOR, items = EXPECTED_TEXTS } = {}) =>
  [
    '## VERIFY-004：因果推进门禁',
    '',
    `${heading}`,
    '',
    '以下任一出现即为门禁退化，等同于 VERIFY-006 的 No-Go：',
    '',
    '```text',
    ...items,
    '```',
    '',
    '最后一条是最隐蔽的。',
    '',
  ].join('\n')

// ── the derivation ──────────────────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-006] forbidden degradations are read from the SSOT in order', () => {
  // Whole strings, in order. A substring or count check would pass a list whose
  // items had been reworded, and a reworded item is a different degradation.
  assert.deepEqual(
    DEGRADATIONS.map((degradation) => degradation.text),
    EXPECTED_TEXTS,
  )

  assert.equal(DEGRADATIONS.length, 13)
  assert.equal(EXPECTED_TEXTS.length, 13, 'the expectation itself must hold thirteen items')
  assert.equal(SSOT_ORIGIN, 'requirements/verification-system/WHAT.md')
})

test('WHAT[VERIFICATION-SYSTEM-006] every item carries a unique id bound to the text it names', () => {
  assert.deepEqual(
    DEGRADATIONS.map((degradation) => [degradation.id, degradation.text]),
    EXPECTED_PAIRS,
  )

  const ids = DEGRADATIONS.map((degradation) => degradation.id)
  assert.equal(new Set(ids).size, ids.length, `duplicate id: ${ids.join(', ')}`)
})

test('WHAT[VERIFICATION-SYSTEM-006] each item records the SSOT line it was read from', () => {
  // The completeness gate reports an id; whoever reads that report needs the line
  // to go argue with. Asserted as consecutive from the first rather than as pinned
  // numbers — the numbers move with any edit above the clause, but the items being
  // one contiguous run is what proves they came from a single block.
  const lines = DEGRADATIONS.map((degradation) => degradation.line)

  assert.deepEqual(
    lines,
    Array.from({ length: 13 }, (_unused, offset) => lines[0] + offset),
  )
})

// ── fail closed: a restructured SSOT must not read as an empty list ─────────

test('WHAT[VERIFICATION-SYSTEM-004] a missing anchor names the heading and the file it looked in', () => {
  assert.throws(
    () => parseDegradations(syntheticSource({ heading: '### 别的小节' }), { origin: 'synthetic/10.md' }),
    (error) => {
      assert.equal(error.message.includes(ANCHOR), true, `message must name the anchor, got: ${error.message}`)
      assert.equal(
        error.message.includes('synthetic/10.md'),
        true,
        `message must name where it looked, got: ${error.message}`,
      )
      return true
    },
  )
})

test('WHAT[VERIFICATION-SYSTEM-004] an empty block is a restructured SSOT not a list of zero', () => {
  assert.throws(
    () => parseDegradations(syntheticSource({ items: [] }), { origin: 'synthetic/10.md' }),
    (error) => {
      assert.equal(error.message.includes(ANCHOR), true, `message must name the anchor, got: ${error.message}`)
      assert.equal(error.message.includes('no items'), true, `message must say the block was empty, got: ${error.message}`)
      return true
    },
  )
})

test('WHAT[VERIFICATION-SYSTEM-004] a section with no fenced block at all fails closed', () => {
  // The clause file holds several other ```text blocks. Scanning past the end of
  // this section would silently hand back VERIFY-005's hard-block list instead.
  const source = ['### 禁止退化清单', '', '以下任一出现即为门禁退化：', '', '## VERIFY-005', '', '```text', 'Kernel 引用 Host raw obj', '```', ''].join('\n')

  assert.throws(
    () => parseDegradations(source, { origin: 'synthetic/10.md' }),
    (error) => {
      assert.equal(error.message.includes('no fenced block'), true, `got: ${error.message}`)
      return true
    },
  )
})

test('WHAT[VERIFICATION-SYSTEM-004] an item with no id and an id with no item both fail closed', () => {
  // Both directions matter. An unnamed item would enter the list with no way for a
  // case to cite it; a stale id would let a case cite a degradation the SSOT has
  // stopped forbidding and still pass.
  assert.throws(
    () =>
      parseDegradations(syntheticSource({ items: [...EXPECTED_TEXTS, '一条新的退化'] }), {
        origin: 'synthetic/10.md',
      }),
    (error) => {
      assert.equal(error.message.includes('一条新的退化'), true, `got: ${error.message}`)
      return true
    },
  )

  assert.throws(
    () => parseDegradations(syntheticSource({ items: EXPECTED_TEXTS.slice(1) }), { origin: 'synthetic/10.md' }),
    (error) => {
      assert.equal(
        error.message.includes('VERIFY_004_D_WALL_CLOCK_AS_ONLY_HANG_CRITERION'),
        true,
        `message must name the orphaned id, got: ${error.message}`,
      )
      return true
    },
  )
})

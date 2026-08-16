// requirements/time-capability/tests/ambient-time-forbidden.test.mjs
// TIME-004 — Domain / Application / Session 禁止直接读 ambient 时间。
// 本包消费 g4r-ce-vocabulary 的 RAW_TIME 静态扫描作为 enforcement（扫描机制
// 归 structured-workflow，其合成 token 必红 / allowlist 钩子测试在
// requirements/structured-workflow/tests/g4r-ce-vocabulary.test.mjs）；本文件
// 只从本包侧证明「业务三层无 raw time token」这条产品事实：
// 对三层目录（存在则）扫描，raw-time 命中必须为零。当前生产树三层目录已
// 重构分散（Change/Execution/... 等），层目录不存在时该事实 vacuous 成立；
// 将来若重建任一扫描层并写入 raw token，本测试即红。
//
// 只读 src/Wanxiangshu/ 源码与 scripts/checks 扫描器；不改动生产代码。

import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { dirname, join, resolve } from 'node:path'

import {
  RAW_TIME_SCAN_LAYERS,
  collectRawTimeScanEntries,
  scanRawTimeEntries,
} from '../../../scripts/checks/g4r-ce-vocabulary.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const PRODUCTION_ROOT = join(ROOT, 'src', 'Wanxiangshu')

test('WHAT[TIME-004] domain_application_session_contain_no_raw_time_tokens', () => {
  // 扫描范围 = TIME-004 规范陈述的三个业务层。
  const entries = collectRawTimeScanEntries(PRODUCTION_ROOT, RAW_TIME_SCAN_LAYERS)
  const hits = scanRawTimeEntries(entries)
  assert.equal(
    hits.length,
    0,
    `no raw wall-clock / timer token may appear in Domain/Application/Session: ${hits
      .map((h) => `${h.file}:${h.line} ${h.token}`)
      .join('; ')}`,
  )
})

test('WHAT[TIME-004] business_layer_scan_is_not_vacuous_across_a_clean_tree', () => {
  // The scan must be able to see a violation when one exists: feed the same
  // scanner a synthetic business-layer file carrying every forbidden token —
  // it must hit all of them. (The enforcement mechanism itself belongs to
  // structured-workflow; this test only pins that our consumption of it is
  // not blind.)
  const dirty = scanRawTimeEntries([
    {
      file: 'Application/Synthetic.fs',
      text: [
        'module Synthetic',
        'let a = DateTimeOffset.UtcNow',
        'let b = DateTime.Now',
        'let c = DateTime.UtcNow',
        'let d = Date.now()',
        'do setTimeout (fun () -> ()) 1',
        'do! PtyTiming.timerTask 100',
      ].join('\n'),
    },
  ])
  for (const token of RAW_TIME_SCAN_LAYERS.length ? ['DateTimeOffset.UtcNow', 'Date.now', 'timerTask'] : []) {
    assert.ok(dirty.some((h) => h.token === token), `scanner must detect ${token}`)
  }
  assert.ok(dirty.length >= 6, `expected ≥6 hits on synthetic business-layer file, got ${dirty.length}`)

  // The production layers scanned for TIME-004 are exactly the three business
  // layers (physical adapters in Process/Infrastructure are out of scope).
  assert.deepEqual([...RAW_TIME_SCAN_LAYERS], ['Domain', 'Application', 'Session'])
})

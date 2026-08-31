// requirements/causal-wait/tests/boundary-observation.test.mjs
// CAUSAL-003 — 观测不进 Journal / Fact codec / 决策路径（本包拥有的产品事实）。
// 静态 enforcement 在 scripts/checks/causal-wait-boundary.mjs（REUSE，经 check.mjs）；
// 本测试 pin 同一事实的最小可执行子集，保证命题在 node --test 下有 active proof：
// Journal 目录与 Fact.fs 的表面不得出现诊断词汇，决策/提示词路径不得读取诊断 snapshot。

import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const SRC = join(ROOT, 'src/Wanxiangshu')

const descriptor = causal.createWait({
  waitKind: 'boundary-observation',
  owner: causal.owner('workflow', { id: 'decision-decoy' }),
  subject: { journalFact: 'lexical-decoy' },
  producer: causal.externalProducer('provider', { id: 'producer-1' }),
  escapes: [causal.escape('processLifetime')],
  source: 'boundary-observation.test',
})

// CausalWait / WaitKind / IWaitSnapshotReader / CausalAwait 是 process-local 诊断词汇；
// 出现在 Journal 与 Fact codec 表面 = 观测进入世界事实路径（违反 CAUSAL-003）。
const FORBIDDEN = /CausalWait|WaitKind|IWaitSnapshotReader|CausalAwait/

const listFs = (dir) => {
  const out = []
  for (const name of readdirSync(dir)) {
    const full = join(dir, name)
    if (statSync(full).isDirectory()) out.push(...listFs(full))
    else if (full.endsWith('.fs')) out.push(full)
  }
  return out
}

test('WHAT[CAUSAL-003] business observer cannot read the diagnostic snapshot', () => {
  const registry = causal.createRegistry()
  const observer = causal.observerCapability(registry)
  const reader = causal.snapshotReaderCapability(registry)
  assertOpaque(observer, 'wait observer capability')
  assertOpaque(reader, 'wait snapshot reader capability')

  const lease = causal.observerEnter(observer, descriptor)
  assert.equal(causal.readerSnapshot(reader).active.length, 1)
  assert.throws(() => causal.readerSnapshot(observer), /snapshot reader capability required/)

  causal.dispose(lease)
})

test('WHAT[CAUSAL-003] Journal codec surfaces stay free of the causal-wait vocabulary', () => {
  const journalDir = join(SRC, 'Persistence', 'Journal')
  assert.ok(existsSync(journalDir), 'Journal directory must exist')
  const files = listFs(journalDir)
  assert.ok(files.length >= 1, 'Journal must contain source files to scan')
  const hits = files.filter((f) => FORBIDDEN.test(readFileSync(f, 'utf8')))
  assert.deepEqual(hits, [], `CausalWait must not enter Journal codec surfaces (hits: ${hits.join(', ')})`)
})

test('WHAT[CAUSAL-003] Fact codec surface stays free of the causal-wait vocabulary', () => {
  const factFile = join(SRC, 'Composition', 'Durable', 'Fact.fs')
  assert.ok(existsSync(factFile), 'Fact.fs must exist')
  assert.ok(
    !FORBIDDEN.test(readFileSync(factFile, 'utf8')),
    'Fact codec must not reference the causal-wait vocabulary',
  )
})

test('WHAT[CAUSAL-003] diagnostics snapshot stays out of decision and prompt paths', () => {
  // 与 causal-wait-boundary.mjs 第 4 条同一清单；optional path 缺席时同样放行。
  const decisionPaths = [
    'Interaction/Dispatch/Dispatcher.fs',
    'Session/PromptDispatcher.fs',
    'Application/Reconciliation/TurnCompletionProgram.fs',
  ]
  const readerRef = /IWaitSnapshotReader|CausalWaitHub\.(?:snapshot|read)|causal-waits\.json/
  for (const rel of decisionPaths) {
    const abs = join(SRC, rel)
    if (!existsSync(abs)) continue
    assert.ok(
      !readerRef.test(readFileSync(abs, 'utf8')),
      `${rel} must not read the diagnostics snapshot into decision/prompt paths`,
    )
  }
})

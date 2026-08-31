// VERIFICATION-SYSTEM-003 — 禁止跨级的物理契约声明面。
//
// 机器可红半边（case 天花板 / 唯一入口 / event 天花板）已由 e2e-watchdog-feed 与
// proof-ladder 承接。本文件锁人工裁决面：声称必须走 OpenCode 的唯一 Long Stroke
// 入口必须写出它依赖的不可模拟 physical contract；删掉声明即红。答不出则不得
// 留在 e2e。

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const ENTRY = join(ROOT, 'requirements/verification-system/tests/e2e/entry.test.mjs')
const LONG_STROKE = join(ROOT, 'requirements/verification-system/tests/e2e/scenarios/long-stroke.toml')
const MARKER = 'PHYSICAL CONTRACTS (VERIFICATION-SYSTEM-003)'
const REQUIRED = [
  /OpenCode process lifetime|spawn count === 1|spawn === 1/,
  /Host-assigned assistant messageID|HOST-010/,
  /repeat-until-pass/i,
]

test('WHAT[VERIFICATION-SYSTEM-003] sole e2e entry declares unsimulatable physical contracts', () => {
  const text = readFileSync(ENTRY, 'utf8')
  assert.equal(text.includes(MARKER), true, 'e2e entry must name PHYSICAL CONTRACTS (VERIFICATION-SYSTEM-003)')
  for (const contract of REQUIRED) {
    assert.match(text, contract, `e2e entry must declare ${contract}`)
  }
  assert.equal(text.replace(MARKER, '').includes(MARKER), false)
})

test('WHAT[VERIFICATION-SYSTEM-003] active-join user-message injection waits for physical ToolPart running', () => {
  const scenario = readFileSync(LONG_STROKE, 'utf8')
  const entry = readFileSync(ENTRY, 'utf8')
  const joinExpectation = scenario.indexOf('{ wait = "manager.1"')
  const runningBarrier = scenario.indexOf('{ custom = "awaitManagerJoinRunning" }')
  const userWake = scenario.indexOf('Interrupt the active join.')

  assert.ok(joinExpectation >= 0, 'Long Stroke must wait for manager.1 provider expectation')
  assert.ok(runningBarrier > joinExpectation, 'physical join-running barrier must follow manager.1')
  assert.ok(userWake > runningBarrier, 'user-message wake must be injected only after join ToolPart is running')
  assert.match(entry, /awaitManagerJoinRunning/)
  assert.match(entry, /message\.part\.updated/)
  assert.match(entry, /toolName\s*===\s*['"]join['"]/)
  assert.match(entry, /toolStatus\s*===\s*['"]running['"]/)
})

test('WHAT[VERIFICATION-SYSTEM-003] format-build-test does not repeat-until-pass', () => {
  const { scripts } = JSON.parse(readFileSync(join(ROOT, 'package.json'), 'utf8'))
  const command = scripts['format-build-test']
  assert.equal(typeof command, 'string')
  assert.doesNotMatch(command, /repeat-until-pass|--repeat|until-pass/i)
})

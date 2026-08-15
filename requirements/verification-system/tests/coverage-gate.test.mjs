// coverage-gate.test.mjs — VERIFICATION-SYSTEM-011 的机器落点。
//
// 覆盖率门禁（run.mjs --coverage → support/run-inner.mjs）的「分母完整」义务：
// 1) 覆盖率必须先预导入全部 dist 生产模块（排除 fable_modules），未加载模块以 0%
//    计入分母而不是消失；2) 低于 COVERAGE_LINE_THRESHOLD 必须 exit 1，无豁免通道；
// 3) 排除项固定为 node_modules / fable_modules / tests / scripts。
// 本测试静态断言 run-inner.mjs 的这三个形状（与 proof-ladder pin check.mjs 同模式），
// 删掉其中任一义务立即红。

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const RUNNER = readFileSync(join(ROOT, 'requirements/verification-system/tests/support/run-inner.mjs'), 'utf8')

test('WHAT[VERIFICATION-SYSTEM-011] coverage pre-imports every production module so the denominator is whole', () => {
  // 未预导入，V8 覆盖率只统计被加载文件，分母缩水让百分比虚高。
  assert.match(RUNNER, /walk\('dist', \['\.js'\]\)\.filter\(\(file\) => !file\.includes\('fable_modules'\)\)/)
  assert.match(RUNNER, /pre-imported .* production modules/)
})

test('WHAT[VERIFICATION-SYSTEM-011] a module that fails to load aborts the run (no partial denominator)', () => {
  // 加载失败只统计到失败点 = 不诚实分母；必须 fail closed。
  assert.match(RUNNER, /failures > 0/)
  assert.match(RUNNER, /failed pre-import — aborting/)
})

test('WHAT[VERIFICATION-SYSTEM-011] line threshold gates the summary and excludes are fixed', () => {
  assert.match(RUNNER, /COVERAGE_LINE_THRESHOLD\s*\?\?\s*80/)
  for (const glob of ['node_modules', 'fable_modules', 'tests', 'scripts']) {
    assert.ok(RUNNER.includes(`'**/${glob}/**'`), `coverage must exclude ${glob}`)
  }
})

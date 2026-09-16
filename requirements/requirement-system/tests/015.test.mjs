// REQUIREMENT-SYSTEM-013/014/015 — change-lifecycle verifier.
//
// 检查器不读正文推断生命周期状态（GOV 机器检查纪律）。本文件只锁：
//   015 小修复豁免仍写在 AGENTS.md（删句即红）
//   014 blocker 四步仍写在 WHAT-014（删步即红）
//   013 Completed 不作当前依据仍写在 WHAT-013；若用户重开 live `changes/active/`，
//       文件必须有 Original proposal / Work origin 标题（目录位置仍是状态源）
//   013 Active 冻结 origin 边界 + 正文段白名单 + 禁止 progress/commit/code-snapshot 段
//       由 activeBodyViolations 纯验证器机械承接（纯文本输入，不扫 changes/active/，
//       不从正文推断生命周期）。原文跨版本不被反向改写由 frozenOriginViolations
//       纯验证器承接。

import assert from 'node:assert/strict'
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import {
  activeBodyViolations,
  frozenOriginViolations,
} from '../../../scripts/lib/spec-rules.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const SMALL_FIX = /普通小型修复[、,].{0,40}不要求创建 Change/
const BLOCKER_STEPS = [
  /停止受影响的\s*产品语义修改/,
  /Blockers/,
  /报告用户/,
  /Amendment/,
]
const COMPLETED_NOT_CURRENT = /Completed.{0,40}不解释当前产品行为/
const ACTIVE_ORIGIN = /Original proposal|Work origin|用户已冻结的裁决/

test('WHAT[REQUIREMENT-SYSTEM-015] AGENTS.md keeps the small-fix exemption', () => {
  const agents = read('AGENTS.md')
  assert.match(agents, SMALL_FIX)
  const dropped = agents.replace(SMALL_FIX, '')
  assert.doesNotMatch(dropped, SMALL_FIX)
})

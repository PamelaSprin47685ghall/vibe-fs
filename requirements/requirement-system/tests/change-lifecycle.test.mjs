// REQUIREMENT-SYSTEM-013/014/015 — change-lifecycle verifier.
//
// 检查器不读正文推断生命周期状态（GOV 机器检查纪律）。本文件只锁：
//   015 小修复豁免仍写在 AGENTS.md（删句即红）
//   014 blocker 四步仍写在 WHAT-014（删步即红）
//   013 Completed 不作当前依据仍写在 WHAT-013；若用户重开 live `changes/active/`，
//       文件必须有 Original proposal / Work origin 标题（目录位置仍是状态源）
// Active 原文冻结与正文白名单仍是人工评审（GAP-003 保持 PARTIAL）。

import assert from 'node:assert/strict'
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

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

test('WHAT[REQUIREMENT-SYSTEM-014] WHAT states the four-step blocker protocol', () => {
  const what = read('requirements/requirement-system/WHAT.md')
  const section = what.slice(what.indexOf('## REQUIREMENT-SYSTEM-014'))
  const body = section.slice(0, section.indexOf('## REQUIREMENT-SYSTEM-015'))
  for (const step of BLOCKER_STEPS) {
    assert.match(body, step, `WHAT-014 must keep ${step}`)
  }
  const dropped = body.replace(/停止受影响的\s*产品语义修改/, '')
  assert.doesNotMatch(dropped, /停止受影响的\s*产品语义修改/)
})

test('WHAT[REQUIREMENT-SYSTEM-013] Completed is not current product behavior', () => {
  const what = read('requirements/requirement-system/WHAT.md')
  const section = what.slice(what.indexOf('## REQUIREMENT-SYSTEM-013'))
  const body = section.slice(0, section.indexOf('## REQUIREMENT-SYSTEM-014'))
  assert.match(body, COMPLETED_NOT_CURRENT)
  const dropped = body.replace(COMPLETED_NOT_CURRENT, '')
  assert.doesNotMatch(dropped, COMPLETED_NOT_CURRENT)
})

test('WHAT[REQUIREMENT-SYSTEM-013] live Active files declare frozen origin', () => {
  const live = join(ROOT, 'changes/active')
  if (!existsSync(live)) return
  const files = readdirSync(live).filter((name) => name.endsWith('.md'))
  for (const name of files) {
    const path = join(live, name)
    if (!statSync(path).isFile()) continue
    assert.match(
      readFileSync(path, 'utf8'),
      ACTIVE_ORIGIN,
      `${name}: live Active must carry Original proposal / Work origin / 冻结裁决`,
    )
  }
})

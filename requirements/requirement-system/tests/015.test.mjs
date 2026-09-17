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

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

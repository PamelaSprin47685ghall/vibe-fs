// Split from tests/unit/resources/prompt-semantic-depth.test.mjs (cutover Wave 2a); owner: office-capability
//
// Prompt Restoration — semantic-depth gate, office-capability half: the
// OFFICE_CAPABILITY_ANCHORS block (five offices) must hit manager role law and
// fork tool in both locales, with the not-interchangeable / fork-forbidden
// negatives. Catalog = Gate C (ARCH-016), shared checker (MECHANISM).
// Role-law anchors moved to cognitive-environment; tool anchors to
// action-affordance.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import {
  OFFICE_CAPABILITY_ANCHORS,
  OFFICE_CAPABILITY_NEGATIVES,
} from '../../../scripts/checks/semantic-anchors.mjs'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const rolePath = (role, locale) => join(root, 'resources/provider/role', role, locale)
const toolPath = (tool, locale) => join(root, 'resources/provider/tool', tool, 'description', locale)

const readRole = (role, locale) => readFileSync(rolePath(role, locale), 'utf8')
const readTool = (tool, locale) => readFileSync(toolPath(tool, locale), 'utf8')

test('WHAT[OFF-002] PROMPT_depth_office_capability_catalog_covers_five_offices', () => {
  assert.deepEqual(Object.keys(OFFICE_CAPABILITY_ANCHORS).sort(), [
    'browser',
    'coder',
    'devops',
    'inquiry',
    'inspector',
  ])
})

test('WHAT[OFF-005] PROMPT_depth_office_capability_hits_manager_and_fork', () => {
  const managerEn = readRole('manager', 'en.md')
  const managerZh = readRole('manager', 'zh-CN.md')
  const forkEn = readTool('fork', 'en.md')
  const forkZh = readTool('fork', 'zh-CN.md')
  for (const spec of Object.values(OFFICE_CAPABILITY_ANCHORS)) {
    assert.match(managerEn, spec.managerEn, `manager/en.md missing ${spec.id}`)
    assert.match(managerZh, spec.managerZh, `manager/zh-CN.md missing ${spec.id}`)
    assert.match(forkEn, spec.forkEn, `fork/en.md missing ${spec.id}`)
    assert.match(forkZh, spec.forkZh, `fork/zh-CN.md missing ${spec.id}`)
  }
  assert.match(managerEn, OFFICE_CAPABILITY_NEGATIVES.managerEnRequired, 'manager/en.md missing not-interchangeable')
  assert.match(managerZh, OFFICE_CAPABILITY_NEGATIVES.managerZhRequired, 'manager/zh-CN.md missing not-interchangeable')
  assert.doesNotMatch(forkEn, OFFICE_CAPABILITY_NEGATIVES.forkForbidden)
  assert.doesNotMatch(forkZh, OFFICE_CAPABILITY_NEGATIVES.forkForbidden)
})

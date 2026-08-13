// Prompt Restoration — semantic-depth gate.
// Role Law must teach a durable cognition contract, not merely avoid tool names.
// Word-count is not quality; missing anchors are. Catalog = Gate C (ARCH-016).

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { roles } from '../support/domain.mjs'
import {
  OFFICE_CAPABILITY_ANCHORS,
  OFFICE_CAPABILITY_NEGATIVES,
  ROLE_SEMANTIC_ANCHORS,
  TOOL_DESCRIPTION_ANCHORS,
} from '../../../scripts/checks/semantic-anchors.mjs'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const rolePath = (role, locale) => join(root, 'resources/provider/role', role, locale)

const readRole = (role, locale) => readFileSync(rolePath(role, locale), 'utf8')

test('PROMPT_depth_EN_role_laws_carry_cognition_anchors', () => {
  for (const [role, anchors] of Object.entries(ROLE_SEMANTIC_ANCHORS)) {
    const text = readRole(role, 'en.md')
    for (const { id, en } of anchors) {
      assert.match(text, en, `${role}/en.md missing semantic anchor: ${id}`)
    }
  }
})

test('PROMPT_depth_ZH_role_laws_carry_matching_cognition_anchors', () => {
  for (const [role, anchors] of Object.entries(ROLE_SEMANTIC_ANCHORS)) {
    const text = readRole(role, 'zh-CN.md')
    for (const { id, zh } of anchors) {
      assert.match(text, zh, `${role}/zh-CN.md missing semantic anchor: ${id}`)
    }
  }
})

test('PROMPT_depth_Inquiry_Sphinx_capability_requires_Kernel_self_model', () => {
  const permissions = roles.permissions(roles.of('Inquiry'))
  assert.ok(
    permissions.some((n) => /Sphinx/i.test(n)),
    `Inquiry must retain Sphinx permission; got ${permissions.join(',')}`,
  )

  const en = readRole('inquiry', 'en.md')
  const zh = readRole('inquiry', 'zh-CN.md')
  assert.match(en, /Kernel/)
  assert.match(en, /Inquirer/)
  assert.match(zh, /Kernel/)
  assert.doesNotMatch(en, /sphinx_start|sphinx_resume/)
  assert.doesNotMatch(zh, /sphinx_start|sphinx_resume/)
})

test('PROMPT_depth_no_universal_closing_report_schema_in_role_laws', () => {
  for (const role of Object.keys(ROLE_SEMANTIC_ANCHORS)) {
    const en = readRole(role, 'en.md')
    assert.doesNotMatch(
      en,
      /Report back with exactly these fields|result, files changed, tests run, evidence, remaining risks, blockers/,
      role,
    )
  }
})

const toolPath = (tool, locale) => join(root, 'resources/provider/tool', tool, 'description', locale)
const readTool = (tool, locale) => readFileSync(toolPath(tool, locale), 'utf8')

test('PROMPT_depth_tool_anchor_catalog_covers_high_risk_verbs', () => {
  assert.ok(TOOL_DESCRIPTION_ANCHORS.inspect.some((a) => a.id === 'no-implement-or-repair'))
  assert.ok(TOOL_DESCRIPTION_ANCHORS['establish-behavior'].some((a) => a.id === 'coder-writes-source'))
  assert.ok(TOOL_DESCRIPTION_ANCHORS['establish-behavior'].some((a) => a.id === 'not-execution-evidence'))
  assert.ok(TOOL_DESCRIPTION_ANCHORS['repair-behavior'].some((a) => a.id === 'meaning-decided'))
  assert.ok(TOOL_DESCRIPTION_ANCHORS['repair-behavior'].some((a) => a.id === 'not-passing-proof'))
  assert.ok(TOOL_DESCRIPTION_ANCHORS['query-shell'].some((a) => a.id === 'observation-not-execution'))
  assert.ok(TOOL_DESCRIPTION_ANCHORS['query-shell'].some((a) => a.id === 'not-build-test'))
  assert.ok(TOOL_DESCRIPTION_ANCHORS.run.some((a) => a.id === 'command-is-act'))
  assert.ok(TOOL_DESCRIPTION_ANCHORS.run.some((a) => a.id === 'economic-commitment'))
  assert.ok(TOOL_DESCRIPTION_ANCHORS.commission.some((a) => a.id === 'independent-road'))
  assert.ok(TOOL_DESCRIPTION_ANCHORS.commission.some((a) => a.id === 'not-lifecycle-stage'))
})

test('PROMPT_depth_EN_tool_descriptions_carry_cognition_anchors', () => {
  for (const [tool, anchors] of Object.entries(TOOL_DESCRIPTION_ANCHORS)) {
    const text = readTool(tool, 'en.md')
    for (const { id, en } of anchors) {
      assert.match(text, en, `${tool}/en.md missing semantic anchor: ${id}`)
    }
  }
})

test('PROMPT_depth_ZH_tool_descriptions_carry_matching_cognition_anchors', () => {
  for (const [tool, anchors] of Object.entries(TOOL_DESCRIPTION_ANCHORS)) {
    const text = readTool(tool, 'zh-CN.md')
    for (const { id, zh } of anchors) {
      assert.match(text, zh, `${tool}/zh-CN.md missing semantic anchor: ${id}`)
    }
  }
})

test('PROMPT_depth_office_capability_catalog_covers_five_offices', () => {
  assert.deepEqual(Object.keys(OFFICE_CAPABILITY_ANCHORS).sort(), [
    'browser',
    'coder',
    'devops',
    'inquiry',
    'inspector',
  ])
})

test('PROMPT_depth_office_capability_hits_manager_and_fork', () => {
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

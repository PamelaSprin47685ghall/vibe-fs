// Split from tests/unit/resources/prompt-semantic-depth.test.mjs (cutover Wave 2a); owner: cognitive-environment
//
// Prompt Restoration — semantic-depth gate, cognitive-environment half
// (COGNITIVE-ENVIRONMENT-002/012): Role Law must teach a durable cognition
// contract, not merely avoid tool names. Word-count is not quality; missing
// anchors are. Catalog = Gate C (ARCH-016), shared checker (MECHANISM).
// The tool-description anchors moved to action-affordance; the OFFICE_CAPABILITY
// block moved to office-capability.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { permissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { ROLE_ANCHOR_DIRS, ROLE_SEMANTIC_ANCHORS } from '../../../scripts/checks/semantic-anchors.mjs'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const rolePath = (role, locale) => join(root, 'resources/provider/role', role, locale)

const readRole = (role, locale) => readFileSync(rolePath(role, locale), 'utf8')

test('WHAT[COGNITIVE-ENVIRONMENT-002] PROMPT_depth_EN_role_laws_carry_cognition_anchors', () => {
  for (const role of ROLE_ANCHOR_DIRS) {
    const text = readRole(role, 'en.md')
    for (const { id, en } of ROLE_SEMANTIC_ANCHORS[role]) {
      assert.match(text, en, `${role}/en.md missing semantic anchor: ${id}`)
    }
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-002] PROMPT_depth_ZH_role_laws_carry_matching_cognition_anchors', () => {
  for (const role of ROLE_ANCHOR_DIRS) {
    const text = readRole(role, 'zh-CN.md')
    for (const { id, zh } of ROLE_SEMANTIC_ANCHORS[role]) {
      assert.match(text, zh, `${role}/zh-CN.md missing semantic anchor: ${id}`)
    }
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-012] PROMPT_depth_Inquiry_Sphinx_capability_requires_Kernel_self_model', () => {
  const inquiryPermissions = permissions('inquiry')
  assert.ok(
    inquiryPermissions.some((n) => /Sphinx/i.test(n)),
    `Inquiry must retain Sphinx permission; got ${inquiryPermissions.join(',')}`,
  )

  const en = readRole('inquiry', 'en.md')
  const zh = readRole('inquiry', 'zh-CN.md')
  assert.match(en, /Kernel/)
  assert.match(en, /Inquirer/)
  assert.match(zh, /Kernel/)
  assert.doesNotMatch(en, /sphinx_start|sphinx_resume/)
  assert.doesNotMatch(zh, /sphinx_start|sphinx_resume/)
})

test('WHAT[COGNITIVE-ENVIRONMENT-012] PROMPT_depth_no_universal_closing_report_schema_in_role_laws', () => {
  for (const role of ROLE_ANCHOR_DIRS) {
    const en = readRole(role, 'en.md')
    assert.doesNotMatch(
      en,
      /Report back with exactly these fields|result, files changed, tests run, evidence, remaining risks, blockers/,
      role,
    )
  }
})

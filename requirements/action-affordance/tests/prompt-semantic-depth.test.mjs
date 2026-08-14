// Split from tests/unit/resources/prompt-semantic-depth.test.mjs (cutover Wave 2a); owner: action-affordance
//
// Prompt Restoration — semantic-depth gate, action-affordance half: high-risk
// tool descriptions must carry cognition anchors (no-implement-or-repair,
// observation-not-execution, command-is-act, …). Catalog = Gate C (ARCH-016),
// shared checker (MECHANISM). Role-law anchors moved to cognitive-environment;
// the OFFICE_CAPABILITY block moved to office-capability.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { TOOL_DESCRIPTION_ANCHORS } from '../../../scripts/checks/semantic-anchors.mjs'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
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

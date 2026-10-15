import assert from 'node:assert/strict'
import test from 'node:test'
import * as ToolSurface from '../../../dist/OpenCode/Tools/ToolSurface.js'
import * as Strength from '../../../dist/Strength/Surface.js'

test('WHAT[work-030] StaticTools and estimate contract explicitly distinguish all five plan tools', () => {
  const planTools = ['js-plan', 'devops', 'ask', 'review', 'deliver']

  // ToolSurface exports toolSpecNames as an array of all recognized tool names
  const names = ToolSurface.toolSpecNames()
  assert.ok(Array.isArray(names), 'ToolSurface.toolSpecNames() must return an array')

  for (const tool of planTools) {
    assert.ok(names.includes(tool), `StaticTools.knownToolNames must explicitly include "${tool}"`)
  }

  // This existing surface excludes resume from the spec list. Its separate
  // Manager permission remains intact; Plan still cannot use it.
  assert.ok(!names.includes('resume'))
  assert.ok(!names.includes('handoff'))

  // Strength.classifyTool must explicitly classify plan tools
  assert.ok(typeof Strength.classifyTool === 'function', 'Strength.classifyTool must be a function')

  // Both forms of asking are explicit tools, not repository estimates.
  for (const tool of planTools) {
    const policy = Strength.classifyTool(tool)
    assert.equal(policy, 'NoEstimate', `${tool} must be explicitly classified as NoEstimate`)
  }
})

test('WHAT[work-030] tool registration does not allow prefix or wildcard matching for plan tools', () => {
  assert.ok(typeof Strength.classifyTool === 'function', 'Strength.classifyTool must be a function')

  // Prefix or wildcard attempts must be rejected or classified as Unreviewed
  // They must NEVER match EstimateAfterCall or NoEstimate
  const invalidAttempts = ['js-plan-fake', 'plan*', 'ask_question', 'devops_extra', 'review_next', 'deliver_final']
  for (const attempt of invalidAttempts) {
    const res = Strength.classifyTool(attempt)
    assert.equal(res, 'Unreviewed', `"${attempt}" must not match plan tool via prefix; must be Unreviewed`)
  }
})

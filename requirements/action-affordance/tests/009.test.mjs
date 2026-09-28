import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { withPlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

integrationTest('WHAT[action-affordance-009] commission descriptions do not advertise the retired Coordinator / Lead choice rejected by the actual schema', async () => {
  await withPlugin(async (hooks) => {
    const calling = hooks.tool.commission.args.calling
    assert.equal(calling.safeParse('lead').success, true)
    assert.equal(calling.safeParse('coordinator').success, false)
    for (const locale of ['en', 'zh-CN']) {
      for (const leaf of ['description', 'arg-calling']) {
        const text = readFileSync(new URL(`../../../resources/provider/tool/commission/${leaf}/${locale}.md`, import.meta.url), 'utf8')
        assert.doesNotMatch(text, /Coordinator\s*\/\s*Lead/, `${locale}/${leaf}: obsolete selectable personas`)
      }
    }
  })
})

test.todo('WHAT[action-affordance-009] all calling choices explain their actual responsibilities without implying new permissions; one retired-choice regression is not semantic completeness (GAP-078)')

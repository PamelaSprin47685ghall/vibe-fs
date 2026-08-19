import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const read = (path) => readFileSync(new URL(`../../../${path}`, import.meta.url), 'utf8')

const workflow = () => read('src/Wanxiangshu/Mission/Finality/Workflow.fs')
const cohort = () => read('src/Wanxiangshu/Mission/Finality/Cohort.fs')
const revision = () => read('src/Wanxiangshu/Mission/Finality/Revision.fs')
const blessing = () => read('src/Wanxiangshu/Mission/Finality/Blessing.fs')
const types = () => read('src/Wanxiangshu/Mission/Finality/Types.fs')
const tool = () => read('src/Wanxiangshu/Mission/Finality/OpenCode/Tool.fs')

test('WHAT[FINALITY-026] modern Finality has no Undecided business outcome or failure sink', () => {
  const modern = [workflow(), cohort(), revision(), blessing(), types(), tool()].join('\n')

  for (const forbidden of [
    'FinalityOutcome.Undecided',
    '| Undecided of prompt',
    'CohortJudgement.Undecided',
    'concludeUndecided',
    'undecidedPrompt',
  ]) {
    assert.equal(modern.includes(forbidden), false, `${forbidden} must not be reachable in modern Finality`)
  }
})

test('WHAT[FINALITY-026] Finality infrastructure exceptions terminate through the diagnostic fuse', () => {
  const source = tool()
  assert.match(source, /Diagnostic\.fatal\s+"finality-infrastructure-failed"/)
  assert.match(source, /session_id/)
  assert.match(source, /ex\.ToString\(\)/)
})

test('WHAT[FINALITY-025] legacy FinalityUndecided is replay-only, never a modern prompt', () => {
  const source = tool()
  assert.doesNotMatch(source, /FinalityUndecidable/)
  assert.doesNotMatch(source, /finality-undecidable/)
})

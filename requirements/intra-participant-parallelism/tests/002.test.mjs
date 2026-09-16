import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const fission = await import('../../../dist/Execution/Fission/Surface.js')
const root = resolve(import.meta.dirname, '../../..')
const read = (p) => readFileSync(resolve(root, p), 'utf8')

const mustOk = (result) => {
  assertJsData(result, 'Fission operation result')
  assert.equal(result.ok, true, JSON.stringify(result))
  return result
}

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-002] canonical lane array preserves each prompt including embedded newlines', () => {
  const parsed = mustOk(fission.parsePrompt(['  A  \r\nstill A', 'B\r\n']))
  assert.deepEqual(
    parsed.lanes.map((lane) => [lane.index, lane.prompt]),
    [
      [0, '  A  \r\nstill A'],
      [1, 'B\r\n'],
    ],
  )
  assert.equal(parsed.count, 2)

  const internalBlank = fission.parsePrompt(['A', '   ', 'C'])
  assert.equal(internalBlank.ok, false)
  assert.equal(internalBlank.reason, 'EmptyLanePrompt')
  assert.equal(internalBlank.laneIndex, 1)

  const tooFew = fission.parsePrompt(['A'])
  assert.equal(tooFew.ok, false)
  assert.equal(tooFew.reason, 'TooFewLanes')
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-002] fission tool exposes prompts as a string array without newline splitting', () => {
  const model = read('src/Wanxiangshu/Execution/Fission/Model.fs')
  const tool = read('src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs')

  assert.match(model, /let parse \(prompts: string list\)/)
  assert.doesNotMatch(model, /normalizeNewlines|\.Split\('\n'\)/)
  assert.match(tool, /args\.Texts "prompts"/)
  assert.match(tool, /ToolHostCodec\.stringArraySchema factory/)
  assert.doesNotMatch(tool, /args\.Text "prompts"/)
})

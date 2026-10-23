import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[cognitive-workspace-006] compression checkpoint is retired and assume does not produce compression boundary', () => {
  const facts = read('src/Wanxiangshu/Context/Companion/Facts.fs')
  const assume = read('src/Wanxiangshu/OpenCode/Tools/AssumeTool.fs')
  assert.doesNotMatch(facts, /ViewCheckpointCommitted/)
  assert.doesNotMatch(assume, /ViewCheckpointCommitted|TodoCheckpointCommitted|Context\.Prefix|PhaseWindow/)
})

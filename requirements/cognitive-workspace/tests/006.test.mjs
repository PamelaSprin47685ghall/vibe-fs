import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[cognitive-workspace-006] compression checkpoint belongs to todowrite, not assume', () => {
  const facts = read('src/Wanxiangshu/Context/Companion/Facts.fs')
  const assume = read('src/Wanxiangshu/OpenCode/Tools/AssumeTool.fs')
  assert.match(facts, /TodoCheckpointCommitted/)
  assert.doesNotMatch(assume, /TodoCheckpointCommitted|Context\.Prefix|PhaseWindow/)
})

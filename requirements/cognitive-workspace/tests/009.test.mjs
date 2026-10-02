import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[cognitive-workspace-009] assume owns no session cache or mutable workspace state', () => {
  const assume = read('src/Wanxiangshu/OpenCode/Tools/AssumeTool.fs')
  assert.doesNotMatch(assume, /Dictionary|mutable|workspace|canvasJson|CurrentCanvas/)
})

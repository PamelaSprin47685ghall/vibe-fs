import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

// Superseded by js.fork / js.join / js.deliver / js.ask: Pair Hint no longer prepares or injects concern announcements or messages.
test('WHAT[concern-routing-002] PairProgrammingThoughtTransform no longer prepares concern fragments or carries ConcernPlacement on wire records', () => {
  const transform = read('src/Wanxiangshu/OpenCode/Host/PairProgrammingThoughtTransform.fs')
  const transformSig = read('src/Wanxiangshu/OpenCode/Host/PairProgrammingThoughtTransform.fsi')
  assert.doesNotMatch(transform, /prepareConcernFragments|concernInstructions|Interaction\.Concern/)
  assert.doesNotMatch(transformSig, /ConcernPlacement|Interaction\.Concern/)
})

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

// Superseded by js.fork / js.join / js.deliver / js.ask: HostFactFold no longer folds ConcernPlacement into a Concern projection.
test('WHAT[concern-routing-004] HostFactFold no longer folds applyConcernPlacement on PairProgrammingGuidelineAnchored', () => {
  const hostFactFold = read('src/Wanxiangshu/Composition/Durable/HostFactFold.fs')
  assert.doesNotMatch(hostFactFold, /applyConcernPlacement|ConcernProjection|Interaction\.Concern/)
})

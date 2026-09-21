import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const ROOT = new URL('../../..', import.meta.url).pathname
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

/// A regex matching `<head>{{ordinal}}<tail>`. Built from strings because a
/// literal `{{` inside a regex literal is not valid JavaScript.
const ordinalSentence = (head, tail) => new RegExp(escapeRegExp(head) + '\\{\\{ordinal\\}\\}' + escapeRegExp(tail))

const escapeRegExp = (value) => value.replace(/[.*+?^${}()|[\\]\\]/g, '\\$&')

const open = (state, road = 'road-1', incumbent = 'inc-1') =>
  relay.openIncumbency(state, road, incumbent, 'snapshot-1', 'authority-1')

const assessAndContinue = (state, incumbent, snapshot = 'snapshot-1') => {
  const assessed = relay.assess(
    state,
    'road-1',
    incumbent,
    `assessment-${incumbent}`,
    snapshot,
    'authority-1',
    ...Array(8).fill('REVISE'),
  )
  assert.equal(assessed.ok, true)
  return relay.retireContinue(
    assessed.state,
    'road-1',
    incumbent,
    `ret-${incumbent}`,
    `run-${incumbent}`,
    `tool-${incumbent}`,
    snapshot,
  )
}

test('WHAT[relay-incumbency-013] every iteration is told which successor number it is', () => {
  const first = open(relay.empty())
  assert.equal(relay.view(first.state, 'road-1').iterationOrdinal, 1)

  const retired = assessAndContinue(first.state, 'inc-1')
  assert.equal(retired.ok, true)

  const second = relay.openIncumbency(retired.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.equal(second.ok, true)
  assert.equal(relay.view(second.state, 'road-1').iterationOrdinal, 2)

  const retiredAgain = assessAndContinue(second.state, 'inc-2', 'snapshot-2')
  assert.equal(retiredAgain.ok, true)
  const third = relay.openIncumbency(retiredAgain.state, 'road-1', 'inc-3', 'snapshot-3', 'authority-1')
  assert.equal(third.ok, true)
  assert.equal(relay.view(third.state, 'road-1').iterationOrdinal, 3)
})

test('WHAT[relay-incumbency-013] the ordinal counts durable openings, not provider requests', () => {
  // Replaying the same opening is idempotent and must not inflate the ordinal:
  // a Manager that is woken twice on one iteration is still that same iteration.
  const first = open(relay.empty())
  const replay = relay.openIncumbency(first.state, 'road-1', 'inc-1', 'snapshot-1', 'authority-1')
  assert.equal(replay.ok, true)
  assert.equal(relay.view(replay.state, 'road-1').iterationOrdinal, 1)

  // A road with no view at all is the first iteration.
  assert.equal(relay.view(relay.empty(), 'road-missing'), null)
})

test('WHAT[relay-incumbency-013] the assessment resource states the successor ordinal in both languages', () => {
  for (const locale of ['en.md', 'zh-CN.md']) {
    const rel = `resources/provider/runtime/manager-assess/${locale}`
    assert.ok(existsSync(join(ROOT, rel)), `${rel} must exist`)
    const text = read(rel)
    assert.match(text, /\{\{ordinal\}\}/, `${rel} must carry the {{ordinal}} placeholder`)
    // The placeholder is the only substitution: the prose must fail closed if
    // the ordinal is missing rather than shipping an unsubstituted token.
    const placeholders = [...text.matchAll(/\{\{([A-Za-z][A-Za-z0-9_]*)\}\}/g)].map((m) => m[1])
    assert.deepEqual([...new Set(placeholders)], ['ordinal'], `${rel} must substitute only the ordinal`)
  }

  assert.match(
    read('resources/provider/runtime/manager-assess/en.md'),
    ordinalSentence('You are the ', ' Manager taking over this mission\\.'),
    'en assessment resource must state the successor ordinal',
  )
  assert.match(
    read('resources/provider/runtime/manager-assess/en.md'),
    /A predecessor may already have done\s+part of the work, or may already have finished it; investigate the actual workspace/,
    'en assessment resource must defer to the successor own investigation',
  )
  assert.match(
    read('resources/provider/runtime/manager-assess/zh-CN.md'),
    ordinalSentence('你是接手此任务的第 ', ' 个 Manager。'),
    'zh-CN assessment resource must state the successor ordinal',
  )
  assert.match(
    read('resources/provider/runtime/manager-assess/zh-CN.md'),
    /前任可能已经做了一些工作，也可能已经完成，以你的实际调查为准。/,
    'zh-CN assessment resource must defer to the successor own investigation',
  )
})

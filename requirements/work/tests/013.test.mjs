import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const opening = () => surface.encodeOpening('plan', 'work-1', '/repo', 7n)

function canonical(value) {
  if (Array.isArray(value)) return `[${value.map(canonical).join(',')}]`
  if (value !== null && typeof value === 'object') {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonical(value[key])}`).join(',')}}`
  }
  return JSON.stringify(value)
}

const incumbencyIdOf = (text) =>
  [...text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line)).flatMap((frame) => frame.events ?? [])].reverse().find((event) => event.type === 'IncumbencyOpened').incumbencyId

test('WHAT[work-013] 一段式无评审的 deliver 不得写成 Delivered，也不得被拒绝', () => {
  const plan = opening()
  const delivered = surface.deliverProbe(plan, 'run-1', 'call-1', 9n)
  assert.equal(delivered.decision, 'Write')
  assert.equal(delivered.detail, 'Relayed')
  assert.equal(delivered.names.includes('IncumbencyRetired'), true)
  assert.equal(delivered.names.includes('IncumbencyOpened'), true)
  assert.equal(delivered.bytes.includes('Delivered'), false)
})
test('WHAT[work-013] S4 退休载荷出现 retirementCursor 必须被拒', () => {
  const s4 = surface.encodeOpening('S4', 'work-cursor', '/repo', 4n)
  const reviewed = surface.reviewProbe(s4, incumbencyIdOf(s4), 'Stayed')
  const confirmed = surface.deliverProbe(s4 + reviewed.bytes, 'run-c', 'call-c', 5n)
  const retired = surface.deliverProbe(s4 + reviewed.bytes + confirmed.bytes, 'run-c', 'call-d', 6n)
  assert.equal(retired.decision, 'Write')
  const frame = JSON.parse(retired.bytes)
  const event = frame.events.find((item) => item.type === 'IncumbencyRetired')
  event.retirementCursor = 6
  const mutated = `${canonical(frame)}\n`
  const read = surface.readProbe(s4 + reviewed.bytes + confirmed.bytes + mutated)
  assert.equal(read.ok, false)
  assert.equal(read.opened, false)
  assert.equal(read.names, '')
  assert.equal(read.activeId, '')
  assert.match(read.reason, /Unknown field: retirementCursor|Execution retirement has plan-only fields/)
  assert.notEqual(read.next, 'Done')
})
test('WHAT[work-013] 缺期望推进值的已落盘 Delivered 不得被读成 Done', () => {
  const plan = opening()
  const relayed = surface.deliverProbe(plan, 'run-d', 'call-d', 9n)
  const frame = JSON.parse(relayed.bytes)
  const event = frame.events.find((item) => item.type === 'IncumbencyRetired')
  event.outcome = 'Delivered'
  delete event.expectedAdvance
  const mutated = `${canonical(frame)}\n`
  const read = surface.readProbe(plan + mutated)
  assert.equal(read.ok, false)
  assert.equal(read.names, '')
  assert.equal(read.activeId, '')
  assert.match(read.reason, /Missing field: expectedAdvance/)
  assert.notEqual(read.next, 'Done')
})
test('WHAT[work-013] 无活跃任期时调用方不得追加 IncumbencyOpened', () => {
  const opened = opening()
  const probe = surface.detachedOpeningProbe(opened)
  assert.equal(probe.decision, 'Refuse')
  assert.equal(probe.names, '')
})

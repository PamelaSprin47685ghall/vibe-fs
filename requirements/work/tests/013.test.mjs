import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const opening = () => surface.encodeOpening('plan', 'work-1', '/repo', 7n)

const incumbencyIdOf = (text) =>
  JSON.parse(text.split('\n').find((line) => line.includes('IncumbencyOpened'))).payload.incumbencyId

test('WHAT[work-013] 一段式无评审的 deliver 不得写成 Delivered，也不得被拒绝', () => {
  const plan = opening()
  const delivered = surface.deliverProbe(plan, 'run-1', 'call-1', 9n)
  assert.equal(delivered.decision, 'Write')
  assert.equal(delivered.detail, 'relayed')
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
  const mutated = retired.bytes.replace(
    '"projectionCut"',
    '"retirementCursor":6,"projectionCut"',
  )
  const read = surface.readProbe(s4 + reviewed.bytes + confirmed.bytes + mutated)
  assert.equal(read.position, 'StorageInvalid')
})
test('WHAT[work-013] 缺期望推进值的已落盘 Delivered 不得返回 DeliveredIdempotent', () => {
  const plan = opening()
  const relayed = surface.deliverProbe(plan, 'run-d', 'call-d', 9n)
  const mutated = relayed.bytes
    .replace('"Relayed"', '"Delivered"')
    .replace(/"expectedAdvance":"[^"]*",/, '')
  const read = surface.readProbe(plan + mutated)
  assert.notEqual(read.position, 'DeliveredIdempotent')
})
test('WHAT[work-013] 无活跃任期时调用方不得追加 IncumbencyOpened', () => {
  const opened = opening()
  const probe = surface.detachedOpeningProbe(opened)
  assert.equal(probe.decision, 'Refuse')
  assert.equal(probe.names, '')
})

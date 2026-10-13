import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const opening = () => surface.encodeOpening('plan', 'work-1', '/repo', 7n)

const incumbencyIdOf = (text) =>
  JSON.parse(text.split('\n').find((line) => line.includes('IncumbencyOpened'))).payload.incumbencyId

test('同一 workId 的第二笔 WorkOpened 不得被折叠成已开立', () => {
  const first = opening()
  assert.equal(first.includes('confirmationCount'), false)
  assert.equal(first.includes('"spawn"'), false)
  const opened = surface.readProbe(first)
  assert.equal(opened.position, 'ActiveRebound')
  assert.equal(opened.sequence, 'S1,S2,S3')
  assert.equal(opened.names.includes('WorkOpened'), true)
  assert.equal(opened.names.includes('IncumbencyOpened'), true)

  const second = first + first
  const again = surface.readProbe(second)
  assert.notEqual(again.position, 'ActiveRebound')
  assert.equal(again.detail.includes('second WorkOpened'), true)
})

test('开立载荷出现确认次数或 spawn 字段时，真实折叠拒绝', () => {
  const bytes = opening()
  const mutated = bytes.replace(
    '{"authorityInitial"',
    '{"confirmationCount":1,"spawn":true,"authorityInitial"',
  )
  const read = surface.readProbe(mutated)
  assert.equal(read.position, 'StorageInvalid')
})

test('S4 的 Promoted 不得置交付授权，S1 的 Promoted 也不得置真', () => {
  const s4 = surface.encodeOpening('S4', 'work-s4', '/repo', 3n)
  const s4Folded = surface.reviewProbe(s4, incumbencyIdOf(s4), 'Promoted')
  assert.equal(s4Folded.decision, 'Write')
  assert.equal(s4Folded.authorized, false)

  const plan = opening()
  const s1 = surface.reviewProbe(plan, incumbencyIdOf(plan), 'Promoted')
  assert.equal(s1.decision, 'Write')
  assert.equal(s1.authorized, false)
  const s3 = surface.finalPromotionProbe(plan)
  assert.equal(s3.authorized, true)
  assert.equal(s3.detail, 'S3')
})

test('一段式无评审的 deliver 不得写成 Delivered，也不得被拒绝', () => {
  const plan = opening()
  const delivered = surface.deliverProbe(plan, 'run-1', 'call-1', 9n)
  assert.equal(delivered.decision, 'Write')
  assert.equal(delivered.detail, 'relayed')
  assert.equal(delivered.names.includes('IncumbencyRetired'), true)
  assert.equal(delivered.names.includes('IncumbencyOpened'), true)
  assert.equal(delivered.bytes.includes('Delivered'), false)
})

test('两段式第一次确认落地前不得返回已确认，同一对重放不得第二次结束', () => {
  const s4 = surface.encodeOpening('S4', 'work-e', '/repo', 4n)
  const first = surface.deliverProbe(s4, 'run-a', 'call-a', 5n)
  assert.equal(first.decision, 'Done')
  assert.equal(first.detail, 'assessment required')
  assert.notEqual(first.detail, 'confirmed')

  const reviewed = surface.reviewProbe(
    s4,
    JSON.parse(s4.split('\n').find((line) => line.includes('IncumbencyOpened'))).payload.incumbencyId,
    'Stayed',
  )
  assert.equal(reviewed.decision, 'Write')
  const afterReview = s4 + reviewed.bytes
  const confirm = surface.deliverProbe(afterReview, 'run-a', 'call-a', 5n)
  assert.equal(confirm.decision, 'Write')
  assert.equal(confirm.names, 'ConfirmationCommitted')
  const replay = surface.replayProbe(afterReview + confirm.bytes, 'run-a', 'call-a')
  assert.equal(replay.decision, 'Done')
  assert.equal(replay.names, '')
  assert.equal(replay.detail.includes('delivered') || replay.detail.includes('relayed'), false)
})

test('S4 退休载荷出现 retirementCursor 必须被拒', () => {
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

test('缺期望推进值的已落盘 Delivered 不得返回 DeliveredIdempotent', () => {
  const plan = opening()
  const relayed = surface.deliverProbe(plan, 'run-d', 'call-d', 9n)
  const mutated = relayed.bytes
    .replace('"Relayed"', '"Delivered"')
    .replace(/"expectedAdvance":"[^"]*",/, '')
  const read = surface.readProbe(plan + mutated)
  assert.notEqual(read.position, 'DeliveredIdempotent')
})

test('只见 Relayed 不见后任是 PairIncomplete，不是 Conflict', () => {
  const plan = opening()
  const relayed = surface.deliverProbe(plan, 'run-p', 'call-p', 9n)
  const lines = relayed.bytes.split('\n').filter((line) => line.length > 0)
  const read = surface.readProbe(`${plan}${lines[0]}\n`)
  assert.equal(read.position, 'PairIncomplete')
  assert.notEqual(read.position, 'Conflict')
})

test('文件不以换行结尾必须 StorageInvalid', () => {
  const bytes = opening().replace(/\n$/, '')
  const read = surface.readProbe(bytes)
  assert.equal(read.position, 'StorageInvalid')
})

test('无活跃任期时调用方不得追加 IncumbencyOpened', () => {
  const opened = opening()
  const probe = surface.detachedOpeningProbe(opened)
  assert.equal(probe.decision, 'Refuse')
  assert.equal(probe.names, '')
})

test('AppendNotAttempted 与 CommitUnknown 不得被说成已落地', () => {
  assert.equal(surface.commitProbe('AppendNotAttempted'), 'AppendNotAttempted')
  assert.equal(surface.commitProbe('CommitUnknown'), 'CommitUnknown')
  assert.notEqual(surface.commitProbe('AppendNotAttempted'), 'AppendDurable')
  assert.notEqual(surface.commitProbe('CommitUnknown'), 'AppendDurable')
})

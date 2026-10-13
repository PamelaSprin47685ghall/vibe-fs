import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const opening = () => surface.encodeOpening('plan', 'work-1', '/repo', 7n)

const incumbencyIdOf = (text) =>
  JSON.parse(text.split('\n').find((line) => line.includes('IncumbencyOpened'))).payload.incumbencyId

const linesOf = (text) => text.split('\n').filter((line) => line.length > 0)

test('WHAT[work-015] 只见 Relayed 不见后任是 PairIncomplete，不是 Conflict', () => {
  const plan = opening()
  const relayed = surface.deliverProbe(plan, 'run-p', 'call-p', 9n)
  const lines = relayed.bytes.split('\n').filter((line) => line.length > 0)
  const read = surface.readProbe(`${plan}${lines[0]}\n`)
  assert.equal(read.position, 'PairIncomplete')
  assert.notEqual(read.position, 'Conflict')
})

test('WHAT[work-015] 文件不以换行结尾必须 StorageInvalid', () => {
  const bytes = opening().replace(/\n$/, '')
  const read = surface.readProbe(bytes)
  assert.equal(read.position, 'StorageInvalid')
})

test('WHAT[work-015] AppendNotAttempted 与 CommitUnknown 不得被说成已落地', () => {
  assert.equal(surface.commitProbe('AppendNotAttempted'), 'AppendNotAttempted')
  assert.equal(surface.commitProbe('CommitUnknown'), 'CommitUnknown')
  assert.notEqual(surface.commitProbe('AppendNotAttempted'), 'AppendDurable')
  assert.notEqual(surface.commitProbe('CommitUnknown'), 'AppendDurable')
})

test('WHAT[work-015] 只见 Relayed 的 IncumbencyRetired、不见后任 IncumbencyOpened，是 PairIncomplete', () => {
  const plan = surface.encodeOpening('plan', 'work-unpaired-relay', '/repo', 7n)
  const relayed = surface.deliverProbe(plan, 'run-unpaired', 'call-unpaired', 9n)
  assert.equal(relayed.decision, 'Write')
  assert.equal(relayed.detail, 'relayed')
  assert.equal(relayed.names.includes('IncumbencyRetired'), true)
  assert.equal(relayed.names.includes('IncumbencyOpened'), true)

  const lines = linesOf(relayed.bytes)
  const retirement = JSON.parse(lines[0])
  assert.equal(retirement.event_type, 'IncumbencyRetired')
  assert.equal(retirement.payload.outcome, 'Relayed')
  const successor = JSON.parse(lines[1])
  assert.equal(successor.event_type, 'IncumbencyOpened')

  const paired = surface.readProbe(`${plan}${relayed.bytes}`)
  assert.notEqual(paired.position, 'PairIncomplete')
  assert.notEqual(paired.position, 'StorageInvalid')

  const unpaired = `${plan}${lines[0]}\n`
  const openingLine = linesOf(plan).find((line) => JSON.parse(line).event_type === 'IncumbencyOpened')
  const unpairedLines = linesOf(unpaired)
  const openedLines = unpairedLines.filter((line) => JSON.parse(line).event_type === 'IncumbencyOpened')
  assert.equal(unpaired.endsWith('\n'), true)
  assert.equal(unpairedLines.at(-1), lines[0])
  assert.deepEqual(openedLines, [openingLine])

  const read = surface.readProbe(unpaired)
  assert.equal(read.position, 'PairIncomplete')
  assert.equal(read.authorized, false)
  assert.equal(read.names, '')
  assert.notEqual(read.position, 'Conflict')
  assert.notEqual(read.position, 'ActiveRebound')
  assert.notEqual(read.position, 'DeliveredIdempotent')
  assert.notEqual(read.next, 'EnterDispatch')
  assert.notEqual(read.next, 'EnterSuccessorPrompt')
  assert.notEqual(read.next, 'WaitForUserPrompt')

  const decoded = surface.decodeDocument(unpaired)
  assert.equal(decoded.ok, false)
  assert.equal(decoded.names, '')
  assert.equal(decoded.names.includes('IncumbencyOpened'), false)
})

test('WHAT[work-015] 统一事件文件最后一行没有换行，读者整份 StorageInvalid', () => {
  const opened = surface.encodeOpening('plan', 'work-truncated-line', '/repo', 7n)
  assert.equal(opened.endsWith('\n'), true)
  const prefix = surface.readProbe(opened)
  assert.equal(prefix.position, 'ActiveRebound')

  const truncated = `${opened}{"event_type":"IncumbencyOpened"`
  assert.equal(truncated.endsWith('\n'), false)

  const read = surface.readProbe(truncated)
  assert.equal(read.position, 'StorageInvalid')
  assert.equal(read.names, '')
  assert.equal(read.sequence, '')
  assert.notEqual(read.next, 'EnterDispatch')
  assert.notEqual(read.next, 'EnterSuccessorPrompt')
  assert.notEqual(read.next, 'WaitForUserPrompt')
  assert.notEqual(read.position, 'ActiveRebound')
  assert.notEqual(read.position, 'DeliveredIdempotent')
  assert.notEqual(read.position, 'PairIncomplete')
  assert.notEqual(read.position, 'Conflict')

  const decoded = surface.decodeDocument(truncated)
  assert.equal(decoded.ok, false)
  assert.equal(decoded.names, '')
  assert.equal(decoded.names.includes('IncumbencyOpened'), false)
  assert.equal(decoded.names.includes('WorkOpened'), false)
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const linesOf = (text) => text.split('\n').filter((line) => line.length > 0)

test('只见 Relayed 的 IncumbencyRetired、不见后任 IncumbencyOpened，是 PairIncomplete', () => {
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

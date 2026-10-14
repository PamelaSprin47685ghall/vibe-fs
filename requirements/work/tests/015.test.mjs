import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const opening = () => surface.encodeOpening('plan', 'work-1', '/repo', 7n)

const linesOf = (text) => text.split('\n').filter((line) => line.length > 0)
const eventsOf = (text) => linesOf(text).flatMap((line) => JSON.parse(line).events ?? [])

function canonical(value) {
  if (Array.isArray(value)) return `[${value.map(canonical).join(',')}]`
  if (value !== null && typeof value === 'object') {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonical(value[key])}`).join(',')}}`
  }
  return JSON.stringify(value)
}

const refused = (read) => {
  assert.equal(read.ok, false)
  assert.equal(read.opened, false)
  assert.equal(read.names, '')
  assert.equal(read.sequence, '')
  assert.equal(read.activeId, '')
  assert.equal(read.authorized, false)
  assert.equal(read.next, '')
}

test('WHAT[work-015] 只见 Relayed 不见后任被公开读取拒绝，不恢复成已开立的工作', () => {
  const plan = opening()
  const relayed = surface.deliverProbe(plan, 'run-p', 'call-p', 9n)
  const frame = JSON.parse(linesOf(relayed.bytes)[0])
  assert.equal(frame.events[0].type, 'IncumbencyRetired')
  assert.equal(frame.events[0].outcome, 'Relayed')
  assert.equal(frame.events[1].type, 'IncumbencyOpened')
  const unpaired = canonical({ ...frame, events: [frame.events[0]] })
  const read = surface.readProbe(`${plan}${unpaired}\n`)
  refused(read)
  assert.match(read.reason, /Relay is missing its successor|atomic event pair is incomplete/)
})

test('WHAT[work-015] 残行不能被折成已开立', () => {
  const opened = surface.encodeOpening('plan', 'work-truncated-line', '/repo', 7n)
  const prefix = surface.readProbe(opened)
  assert.equal(prefix.ok, true)
  assert.equal(prefix.next, 'Review')
  assert.equal(prefix.activeId.length > 0, true)

  const truncated = `${opened}{"event_type":"IncumbencyOpened"`
  assert.equal(truncated.endsWith('\n'), false)
  const read = surface.readProbe(truncated)
  refused(read)
  assert.notEqual(read.reason, '')

  const decoded = surface.decodeDocument(truncated)
  assert.equal(decoded.ok, false)
  assert.equal(decoded.names, '')
})

test('WHAT[work-015] 没写与结果未知用公开 journal 状态，旧名字不是通过条件', () => {
  assert.equal(surface.commitProbe('NotWritten'), 'NotWritten')
  assert.equal(surface.commitProbe('Unknown'), 'Unknown')
  assert.notEqual(surface.commitProbe('NotWritten'), 'Committed')
  assert.notEqual(surface.commitProbe('Unknown'), 'Committed')
  assert.notEqual(surface.commitProbe('AppendNotAttempted'), 'NotWritten')
  assert.notEqual(surface.commitProbe('CommitUnknown'), 'Unknown')
})

test('WHAT[work-015] 成对的 Relayed 与后任恢复为 Review；缺后任则拒绝且不另造身份', () => {
  const plan = surface.encodeOpening('plan', 'work-unpaired-relay', '/repo', 7n)
  const relayed = surface.deliverProbe(plan, 'run-unpaired', 'call-unpaired', 9n)
  assert.equal(relayed.decision, 'Write')
  assert.equal(relayed.detail, 'Relayed')
  assert.equal(relayed.names.includes('IncumbencyRetired'), true)
  assert.equal(relayed.names.includes('IncumbencyOpened'), true)

  const frame = JSON.parse(linesOf(relayed.bytes)[0])
  const retirement = frame.events[0]
  assert.equal(retirement.type, 'IncumbencyRetired')
  assert.equal(retirement.outcome, 'Relayed')
  const successor = frame.events[1]
  assert.equal(successor.type, 'IncumbencyOpened')

  const paired = surface.readProbe(`${plan}${relayed.bytes}`)
  assert.equal(paired.ok, true)
  assert.equal(paired.next, 'Review')
  assert.equal(paired.opened, true)
  assert.notEqual(paired.activeId, '')
  assert.notEqual(paired.activeId, eventsOf(plan).find((event) => event.type === 'IncumbencyOpened').incumbencyId)

  const unpaired = canonical({ ...frame, events: [retirement] })
  const read = surface.readProbe(`${plan}${unpaired}\n`)
  refused(read)
  assert.match(read.reason, /Relay is missing its successor|atomic event pair is incomplete/)
})

test('WHAT[work-015] 完整记录仅缺结尾换行，公开 journal 没有对应的拒绝名', () => {
  const bytes = opening().replace(/\n$/, '')
  assert.equal(bytes.endsWith('\n'), false)
  assert.equal(JSON.parse(bytes).events.some((event) => event.type === 'WorkOpened'), true)
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const opening = () => surface.encodeOpening('plan', 'work-1', '/repo', 7n)

const incumbencyIdOf = (text) =>
  [...text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line)).flatMap((frame) => frame.events ?? [])].reverse().find((event) => event.type === 'IncumbencyOpened').incumbencyId

test('WHAT[work-014] S4 一段式交付直接完成，同一 fence 重放返回既有记录且不重复退休', () => {
  const s4 = surface.encodeOpening('S4', 'work-e', '/repo', 4n)
  // 一段式交付：无需两段式确认，一次 deliver 即原子写入退休与后继
  const deliver = surface.deliverProbe(s4, 'run-a', 'call-a', 5n)
  assert.equal(deliver.decision, 'Write')
  assert.equal(deliver.names, 'IncumbencyRetired,IncumbencyOpened')

  // 同一 fence 重放：返回原记录，零追加，不重复结束
  const replay = surface.replayProbe(s4 + deliver.bytes, 'run-a', 'call-a', 5n)
  assert.equal(replay.decision, 'Done')
  assert.equal(replay.names, '')
})

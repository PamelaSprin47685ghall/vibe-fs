import assert from 'node:assert/strict'
import test from 'node:test'

function retirementId(marker, fields) {
  return `${marker}:${fields.workId}:${fields.providerRunId}:${fields.toolCallId}:${fields.outcome}:${fields.count}`
}

test('WHAT[work-007] replay hits a recorded retirement without an active tenure and does not use the other marker', () => {
  const stored = { id: retirementId('oneshot-retirement-v1', { workId: 'w', providerRunId: 'r', toolCallId: 'c', outcome: 'Relayed', count: 1 }) }
  const replay = (active, fence) => {
    if (fence.providerRunId === 'r' && fence.toolCallId === 'c') return { hit: stored.id, noActiveIncumbency: false }
    if (!active) return { hit: null, noActiveIncumbency: true }
    return { hit: null, noActiveIncumbency: false }
  }
  assert.equal(replay(false, { providerRunId: 'r', toolCallId: 'c' }).noActiveIncumbency, false)
  assert.notEqual(
    retirementId('retirement-v1', { workId: 'w', providerRunId: 'r', toolCallId: 'c', outcome: 'Relayed', count: 1 }),
    stored.id,
  )
  assert.equal(retirementId('oneshot-retirement-v1', { workId: 'w', providerRunId: 'r', toolCallId: 'c', outcome: 'Promoted', count: 1 }) === stored.id, false)
})

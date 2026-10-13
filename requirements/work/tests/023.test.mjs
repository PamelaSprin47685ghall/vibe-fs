import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const eventsOf = (text) => text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line))
const incumbencyIdOf = (text) =>
  [...eventsOf(text)].reverse().find((event) => event.event_type === 'IncumbencyOpened').payload.incumbencyId

const open = (workId) => {
  const text = surface.encodeOpening('plan', workId, '/root', 0n)
  assert.equal(surface.readProbe(text).position, 'ActiveRebound')
  return text
}

const promoteAndRelay = (text, step) => {
  const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  assert.equal(reviewed.decision, 'Write', reviewed.detail)
  const relayed = surface.deliverProbe(`${text}${reviewed.bytes}`, `run-${step}`, `call-${step}`, BigInt(step) * 10n + 10n)
  assert.equal(relayed.decision, 'Write', relayed.detail)
  assert.equal(relayed.detail, 'relayed')
  return { reviewed, relayed, text: `${text}${reviewed.bytes}${relayed.bytes}` }
}

test('WHAT[work-023] the projection advances only on Promoted, and only the final S1-S3 element authorizes delivery', () => {
  const opened = open('work-1')
  assert.equal(surface.documentView(opened).projection, 'S1')
  assert.equal(surface.documentView(opened).authorized, false)

  const stayed = surface.reviewProbe(opened, incumbencyIdOf(opened), 'Stayed')
  assert.equal(stayed.decision, 'Write')
  const stayedView = surface.documentView(`${opened}${stayed.bytes}`)
  assert.equal(stayedView.projection, 'S1', 'Stayed keeps the stage')
  assert.equal(stayedView.authorized, false)
  assert.equal(stayedView.openingStage, 'S1', 'review does not rewrite the opening stage')

  const first = surface.reviewProbe(opened, incumbencyIdOf(opened), 'Promoted')
  assert.equal(first.decision, 'Write')
  assert.equal(first.authorized, false, 'an S1 promotion grants the next stage, not delivery authorization')
  const s2 = surface.documentView(`${opened}${first.bytes}`)
  assert.equal(s2.projection, 'S2')
  assert.equal(s2.activeId, incumbencyIdOf(opened), 'promotion stays on the same incumbency')

  const relay1 = surface.deliverProbe(`${opened}${first.bytes}`, 'run-1', 'call-1', 10n)
  assert.equal(relay1.decision, 'Write', relay1.detail)
  assert.equal(eventsOf(relay1.bytes)[1].payload.stage, 'S2', 'relay opens the successor at the retired projection')
  const s2Tenure = surface.documentView(`${opened}${first.bytes}${relay1.bytes}`)
  assert.equal(s2Tenure.projection, 'S2')
  assert.equal(s2Tenure.authorized, false)

  const second = surface.reviewProbe(`${opened}${first.bytes}${relay1.bytes}`, incumbencyIdOf(`${opened}${first.bytes}${relay1.bytes}`), 'Promoted')
  assert.equal(second.decision, 'Write')
  assert.equal(second.authorized, false, 'an S2 promotion is not the final element')
  assert.equal(surface.documentView(`${opened}${first.bytes}${relay1.bytes}${second.bytes}`).projection, 'S3')

  const relay2 = surface.deliverProbe(
    `${opened}${first.bytes}${relay1.bytes}${second.bytes}`,
    'run-2',
    'call-2',
    20n,
  )
  assert.equal(relay2.decision, 'Write', relay2.detail)
  const s3Text = `${opened}${first.bytes}${relay1.bytes}${second.bytes}${relay2.bytes}`
  assert.equal(surface.documentView(s3Text).authorized, false, 'the S3 tenure is unauthorized before its own review')

  const third = surface.reviewProbe(s3Text, incumbencyIdOf(s3Text), 'Promoted')
  assert.equal(third.decision, 'Write')
  assert.equal(third.authorized, true, 'the final S1-S3 element sets delivery authorization')
  const authorized = surface.documentView(`${s3Text}${third.bytes}`)
  assert.equal(authorized.projection, 'S3', 'the final promotion does not invent a fourth stage')
  assert.equal(authorized.openingStage, 'S3')
})

test('WHAT[work-023] delivery and reopen do not touch the stage', () => {
  let text = open('work-2')
  text = promoteAndRelay(text, 1).text
  text = promoteAndRelay(text, 2).text
  const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  assert.equal(reviewed.decision, 'Write', reviewed.detail)
  text = `${text}${reviewed.bytes}`
  const before = surface.documentView(text)
  assert.equal(before.projection, 'S3')
  assert.equal(before.authorized, true)

  const delivered = surface.deliverStateProbe(text, 'run-deliver', 'call-deliver', 30n)
  assert.equal(delivered.decision, 'Write', delivered.detail)
  assert.equal(delivered.detail, 'delivered')
  const deliveredText = `${text}${delivered.bytes}`
  const deliveredView = surface.documentView(deliveredText)
  assert.equal(deliveredView.delivered, true)
  assert.equal(deliveredView.active, false)
  assert.equal(deliveredView.projection, 'S3', 'delivery does not move the stage')

  const reopened = surface.reopenProbe(deliveredText, before.activeId)
  assert.equal(reopened.decision, 'Write', reopened.detail)
  const restored = surface.documentView(`${deliveredText}${reopened.bytes}`)
  assert.equal(restored.delivered, false)
  assert.equal(restored.projection, 'S3')
  assert.equal(restored.activeId, before.activeId)
  assert.equal(restored.authorized, true, 'reopen keeps the authorization of the delivered tenure')
})

test('WHAT[work-023] illegal stage and review transitions are rejected', () => {
  const opened = open('work-3')
  const ghost = surface.reviewProbe(opened, 'inc-ghost', 'Promoted')
  assert.equal(ghost.decision, 'Refuse', 'review of an unknown incumbency is rejected')
  assert.match(ghost.detail, /mismatch/i)

  const promoted = surface.reviewProbe(opened, incumbencyIdOf(opened), 'Promoted')
  const relayed = surface.deliverProbe(`${opened}${promoted.bytes}`, 'run-3', 'call-3', 10n)
  assert.equal(relayed.decision, 'Write', relayed.detail)
  const retiredText = `${opened}${promoted.bytes}${relayed.bytes}`
  const late = surface.reviewProbe(retiredText, incumbencyIdOf(opened), 'Promoted')
  assert.equal(late.decision, 'Refuse', 'review of a retired incumbency is rejected')

  assert.equal(eventsOf(relayed.bytes)[1].payload.stage, 'S2', 'the kernel opens the successor at the retired projection, not S3')
  const paired = surface.readProbe(`${opened}${promoted.bytes}${relayed.bytes}`)
  assert.equal(paired.position, 'ActiveRebound')

  const execution = surface.encodeOpening('S4', 'work-s4', '/root', 3n)
  const executionPromotion = surface.reviewProbe(execution, incumbencyIdOf(execution), 'Promoted')
  assert.equal(executionPromotion.decision, 'Write')
  assert.equal(executionPromotion.authorized, false, 'S4 is not an S1-S3 final element')
})

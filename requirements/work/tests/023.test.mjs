import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as planning from '../../../dist/Mission/Planning/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const framesOf = (text) => text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line))
const eventsOf = (text) => framesOf(text).flatMap((frame) => frame.events ?? [])
const incumbencyIdOf = (text) => [...eventsOf(text)].reverse().find((event) => event.type === 'IncumbencyOpened').incumbencyId

const open = (workId) => {
  const text = surface.encodeOpening('plan', workId, '/root', 0n)
  assert.equal(surface.readProbe(text).next, 'Review')
  return text
}

// An empty first review fixes eligibility; deliver performs the promotion and
// relays to a fresh per-stage tenure (each stage starts read-only again).
const reviewThenDeliver = (text, step, expect) => {
  const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  assert.equal(reviewed.decision, 'Write', reviewed.detail)
  const reviewedText = `${text}${reviewed.bytes}`
  const delivered = surface.deliverProbe(reviewedText, `run-${step}`, `call-${step}`, BigInt(step) * 10n, null)
  assert.equal(delivered.decision, 'Write', delivered.detail)
  assert.equal(delivered.detail, expect)
  return `${reviewedText}${delivered.bytes}`
}

test('WHAT[work-023] review grants eligibility; deliver performs the stage promotion', () => {
  const opened = open('work-1')
  assert.equal(surface.documentView(opened).projection, 'S1')
  assert.equal(surface.documentView(opened).authorized, false)

  const s1Id = incumbencyIdOf(opened)
  const eligible = surface.reviewProbe(opened, s1Id, 'Promoted')
  assert.equal(eligible.decision, 'Write')
  assert.equal(eligible.authorized, false, 'eligibility never authorises delivery')
  const eligibleView = surface.documentView(`${opened}${eligible.bytes}`)
  assert.equal(eligibleView.projection, 'S1', 'eligibility never advances the stage')

  let text = `${opened}${eligible.bytes}`
  const s1Deliver = surface.deliverProbe(text, 'run-1', 'call-1', 10n, null)
  assert.equal(s1Deliver.decision, 'Write', s1Deliver.detail)
  assert.equal(s1Deliver.detail, 'Promoted')
  text = `${text}${s1Deliver.bytes}`
  const s2View = surface.documentView(text)
  assert.equal(s2View.projection, 'S2', 'deliver advanced S1 -> S2')
  assert.notEqual(s2View.activeId, s1Id, 'promotion relays to a fresh S2 tenure')

  text = reviewThenDeliver(text, 2, 'Promoted')
  assert.equal(surface.documentView(text).projection, 'S3', 'deliver advanced S2 -> S3')

  text = reviewThenDeliver(text, 3, 'Authorized')
  const authorized = surface.documentView(text)
  assert.equal(authorized.projection, 'S3', 'authorization does not invent a fourth stage')
  assert.equal(authorized.authorized, true)
})

test('WHAT[work-023] delivery and reopen do not touch the stage beyond the promotion', () => {
  let text = open('work-2')
  text = reviewThenDeliver(text, 1, 'Promoted')
  text = reviewThenDeliver(text, 2, 'Promoted')
  text = reviewThenDeliver(text, 3, 'Authorized')
  const before = surface.documentView(text)
  assert.equal(before.projection, 'S3')
  assert.equal(before.authorized, true)

  const root = mkdtempSync(join(tmpdir(), 'wxs-work-023-'))
  let delivered
  try {
    const resolved = planning.PlanningSurface.resolvePlanPath(root, 'work-2')
    assert.equal(resolved.ok, true, resolved.error)
    mkdirSync(dirname(resolved.path), { recursive: true })
    writeFileSync(resolved.path, '# plan\n', 'utf8')
    const selected = planning.PlanningSurface.readAndValidatePlan(root, 'work-2')
    assert.equal(selected.ok, true, selected.error)
    delivered = surface.deliverStateProbe(text, 'run-deliver', 'call-deliver', 40n, { digest: selected.digest, path: selected.path })
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
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
  assert.notEqual(restored.activeId, before.activeId, 'reopen starts a new incumbency, not the delivered one')
  assert.equal(restored.authorized, false, 'a reopened tenure starts unauthorized')
})

test('WHAT[work-023] illegal stage and review transitions are rejected', () => {
  const opened = open('work-3')
  const ghost = surface.reviewProbe(opened, 'inc-ghost', 'Promoted')
  assert.equal(ghost.decision, 'Refuse', 'review of an unknown incumbency is rejected')
  assert.match(ghost.detail, /mismatch/i)

  // A non-eligible deliver relays: the successor tenure opens fresh (read-only again).
  const relayed = surface.deliverProbe(opened, 'run-3', 'call-3', 10n, null)
  assert.equal(relayed.decision, 'Write', relayed.detail)
  assert.equal(relayed.detail, 'Relayed')
  const retiredText = `${opened}${relayed.bytes}`
  const paired = surface.readProbe(retiredText)
  assert.equal(paired.next, 'Review')

  // Reviewing the retired tenure's id is rejected.
  const retiredId = incumbencyIdOf(opened)
  const late = surface.reviewProbe(retiredText, retiredId, 'Stayed')
  assert.equal(late.decision, 'Refuse')
  assert.match(late.detail, /mismatch/i)

  const execution = surface.encodeOpening('S4', 'work-s4', '/root', 3n)
  const executionPromotion = surface.reviewProbe(execution, incumbencyIdOf(execution), 'Promoted')
  assert.equal(executionPromotion.decision, 'Write')
  assert.equal(executionPromotion.authorized, false, 'S4 is not an S1-S3 final element')
})

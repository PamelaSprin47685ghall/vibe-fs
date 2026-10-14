import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as planning from '../../../dist/Mission/Planning/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const framesOf = (text) => text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line))
const eventsOf = (text) => framesOf(text).flatMap((frame) => frame.events ?? [])
const incumbencyIdOf = (text) => [...eventsOf(text)].reverse().find((event) => event.type === 'IncumbencyOpened').incumbencyId

const replayed = (text, run, call, head, draft) => {
  const again = surface.replayStateProbe(text, run, call, head, draft)
  assert.equal(again.decision, 'Done', again.detail)
  assert.equal(again.detail, 'Replayed')
  const original = surface.documentView(text)
  const after = surface.documentView(text)
  assert.deepEqual(after, original, 'exact replay returns the original folded state')
  return again
}

const climb = (workId, root, head) => {
  let text = surface.encodeOpening('plan', workId, root, head)
  assert.equal(surface.readProbe(text).next, 'Review')
  for (let step = 0; step < 2; step += 1) {
    const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
    assert.equal(reviewed.decision, 'Write', reviewed.detail)
    const relayed = surface.deliverProbe(`${text}${reviewed.bytes}`, `run-${step}`, `call-${step}`, BigInt(head) + BigInt(step + 1))
    assert.equal(relayed.decision, 'Write', relayed.detail)
    assert.equal(relayed.detail, 'Relayed')
    text = `${text}${reviewed.bytes}${relayed.bytes}`
  }
  const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  assert.equal(reviewed.decision, 'Write', reviewed.detail)
  return `${text}${reviewed.bytes}`
}

test('WHAT[work-022] rotation facts and exact replay return the original state', () => {
  const opened = surface.encodeOpening('plan', 'work-1', '/repo/root', 0n)
  const openProbe = surface.readProbe(opened)
  assert.equal(openProbe.next, 'Review')
  assert.equal(openProbe.opened, true)
  assert.equal(openProbe.activeId.length > 0, true)
  assert.equal(openProbe.names, 'WorkOpened,IncumbencyOpened')
  const openView = surface.documentView(opened)
  assert.equal(openView.root, '/repo/root')
  assert.equal(openView.projection, 'S1')
  assert.equal(openView.retiredCount, 0)

  // The same opening record under the same request id is an idempotent
  // replay, not a second opening.
  const secondOpen = surface.readProbe(opened + opened)
  assert.equal(secondOpen.ok, true, secondOpen.reason)
  assert.equal(secondOpen.opened, true)
  assert.equal(secondOpen.names, 'WorkOpened,IncumbencyOpened')
  assert.equal(secondOpen.next, 'Review')

  const firstId = incumbencyIdOf(opened)
  const reviewed = surface.reviewProbe(opened, firstId, 'Promoted')
  assert.equal(reviewed.decision, 'Write')
  assert.equal(reviewed.names, 'Reviewed')
  assert.equal(reviewed.authorized, false, 'S1 promotion advances the projection, not delivery authorization')
  const reviewedText = `${opened}${reviewed.bytes}`
  const reviewedView = surface.documentView(reviewedText)
  assert.equal(reviewedView.projection, 'S2')
  assert.equal(reviewedView.activeId, firstId, 'review keeps the same incumbency')

  const secondReview = surface.reviewProbe(reviewedText, firstId, 'Stayed')
  assert.equal(secondReview.decision, 'Refuse')
  assert.match(secondReview.detail, /already reviewed/i)
  assert.deepEqual(surface.documentView(reviewedText), reviewedView, 'a refused review writes nothing')

  const relayed = surface.deliverProbe(reviewedText, 'run-1', 'call-1', 10n)
  assert.equal(relayed.decision, 'Write')
  assert.equal(relayed.detail, 'Relayed')
  assert.equal(relayed.names, 'IncumbencyRetired,IncumbencyOpened')
  const relayedEvents = eventsOf(relayed.bytes)
  assert.equal(relayedEvents[0].outcome, 'Relayed')
  assert.equal(relayedEvents[0].incumbencyId, firstId)
  const relayedText = `${reviewedText}${relayed.bytes}`
  const relayedView = surface.documentView(relayedText)
  assert.equal(relayedView.delivered, false, 'Relayed must not set Delivered')
  assert.equal(relayedView.retiredCount, 1)
  assert.equal(relayedView.projection, 'S2', 'the successor opens at the retired projection')
  assert.notEqual(relayedView.activeId, firstId)
  const relayAgain = surface.deliverProbe(relayedText, 'run-1', 'call-1', 10n)
  assert.equal(relayAgain.decision, 'Done', relayAgain.detail)
  assert.equal(relayAgain.detail, 'Replayed')
  assert.equal(relayAgain.names, '')
  assert.deepEqual(surface.documentView(relayedText), relayedView, 'exact relay replay returns the original state')

  const authorized = climb('work-1b', '/repo/root', 0n)
  const authorizedView = surface.documentView(authorized)
  assert.equal(authorizedView.projection, 'S3')
  assert.equal(authorizedView.authorized, true)
  const planRoot = mkdtempSync(join(tmpdir(), 'wxs-work-022-plan-'))
  let delivered
  let selected
  try {
    const written = planning.PlanningSurface.rewritePlanAtomic(planRoot, 'work-1b', '# plan\n')
    assert.equal(written.ok, true, written.error)
    selected = planning.PlanningSurface.readAndValidatePlan(planRoot, 'work-1b')
    assert.equal(selected.ok, true, selected.error)
    delivered = surface.deliverStateProbe(authorized, 'run-final', 'call-final', 30n, { digest: selected.digest, path: selected.path })
  } finally {
    rmSync(planRoot, { recursive: true, force: true })
  }
  assert.equal(delivered.decision, 'Write', delivered.detail)
  assert.equal(delivered.detail, 'delivered')
  assert.equal(delivered.names, 'IncumbencyRetired')
  const deliveredText = `${authorized}${delivered.bytes}`
  const deliveredView = surface.documentView(deliveredText)
  assert.equal(deliveredView.delivered, true)
  assert.equal(deliveredView.digest, selected.digest)
  assert.equal(deliveredView.execSessionId.startsWith('exec:'), true)
  assert.equal(deliveredView.active, false)
  replayed(deliveredText, 'run-final', 'call-final', 30n, { digest: selected.digest, path: selected.path })

  const reopened = surface.reopenProbe(deliveredText, incumbencyIdOf(authorized))
  assert.equal(reopened.decision, 'Write', reopened.detail)
  assert.equal(reopened.detail, 'Reopened')
  assert.equal(reopened.names.includes('Reopened'), true)
  assert.equal(reopened.names.includes('IncumbencyOpened'), true)
  const reopenedText = `${deliveredText}${reopened.bytes}`
  const reopenedView = surface.documentView(reopenedText)
  assert.equal(reopenedView.delivered, false)
  assert.equal(reopenedView.projection, 'S3')
  assert.notEqual(reopenedView.activeId, '')
  assert.notEqual(reopenedView.activeId, incumbencyIdOf(authorized), 'reopen opens a new tenure')
  assert.equal(reopenedView.retiredCount, 3, 'reopen does not rewrite retired history')
  assert.equal(reopenedView.authorized, false)
})

test('WHAT[work-022] conflicting payloads and illegal transitions fail closed', () => {
  const opened = surface.encodeOpening('plan', 'work-1', '/repo/root1', 0n)
  const conflicted = surface.encodeOpening('plan', 'work-1', '/repo/root2', 0n)
  const openConflict = surface.readProbe(opened + conflicted)
  assert.equal(openConflict.ok, false)
  assert.equal(openConflict.opened, false)
  assert.equal(openConflict.names, '')
  assert.equal(openConflict.reason.includes('RequestIdConflict'), true)

  const firstId = incumbencyIdOf(opened)
  const secondActive = surface.detachedOpeningProbe(opened)
  assert.equal(secondActive.decision, 'Refuse', 'cannot open a second active incumbency')
  assert.equal(secondActive.names, '')

  const stayed = surface.reviewProbe(opened, firstId, 'Stayed')
  assert.equal(stayed.decision, 'Write')
  const stayedText = `${opened}${stayed.bytes}`
  const secondReview = surface.reviewProbe(stayedText, firstId, 'Promoted')
  assert.equal(secondReview.decision, 'Refuse', 'each incumbency carries at most one review')

  const relayed = surface.deliverProbe(stayedText, 'run-stay', 'call-stay', 10n)
  assert.equal(relayed.decision, 'Write', relayed.detail)
  const successor = eventsOf(relayed.bytes)[1]
  assert.equal(successor.stage, 'S1', 'a Stayed retirement keeps the stage for the successor')
  assert.equal(eventsOf(relayed.bytes)[1].stage, 'S1', 'the kernel keeps the successor at the retired projection')

  const whileActive = surface.deliverProbe(opened, 'run-active', 'call-active', 11n)
  assert.equal(whileActive.decision, 'Write')
  assert.equal(whileActive.detail, 'Relayed', 'an unauthorized active tenure relays; it does not deliver')
  assert.equal(surface.documentView(`${opened}${whileActive.bytes}`).delivered, false)

  const reopenActive = surface.reopenProbe(opened, firstId)
  assert.equal(reopenActive.decision, 'Refuse')
  assert.match(reopenActive.detail, /inactive/i)

  const authorized = climb('work-2', '/repo/root', 0n)
  const planRoot = mkdtempSync(join(tmpdir(), 'wxs-work-022-deliver-'))
  let delivered
  try {
    const written = planning.PlanningSurface.rewritePlanAtomic(planRoot, 'work-2', '# plan\n')
    assert.equal(written.ok, true, written.error)
    const selected = planning.PlanningSurface.readAndValidatePlan(planRoot, 'work-2')
    assert.equal(selected.ok, true, selected.error)
    delivered = surface.deliverStateProbe(authorized, 'run-2', 'call-2', 30n, { digest: selected.digest, path: selected.path })
  } finally {
    rmSync(planRoot, { recursive: true, force: true })
  }
  assert.equal(delivered.decision, 'Write', delivered.detail)
  const deliveredText = `${authorized}${delivered.bytes}`
  const wrong = surface.reopenProbe(deliveredText, 'not-the-delivered-incumbency')
  assert.equal(wrong.decision, 'Refuse', 'reopen must name the delivered incumbency')
  assert.equal(surface.documentView(deliveredText).delivered, true)
})

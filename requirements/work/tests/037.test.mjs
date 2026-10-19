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

const deliveredOf = (workId) => {
  let text = surface.encodeOpening('plan', workId, '/root', 0n)
  assert.equal(surface.readProbe(text).next, 'Review')
  for (let step = 0; step < 2; step += 1) {
    const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
    assert.equal(reviewed.decision, 'Write', reviewed.detail)
    const relayed = surface.deliverProbe(`${text}${reviewed.bytes}`, `run-${step}`, `call-${step}`, BigInt((step + 1) * 10))
    assert.equal(relayed.decision, 'Write', relayed.detail)
    text = `${text}${reviewed.bytes}${relayed.bytes}`
  }
  const reviewed = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  assert.equal(reviewed.decision, 'Write', reviewed.detail)
  text = `${text}${reviewed.bytes}`
  const root = mkdtempSync(join(tmpdir(), 'wxs-work-037-'))
  let delivered
  try {
    const written = planning.PlanningSurface.rewritePlanAtomic(root, workId, '# plan\n')
    assert.equal(written.ok, true, written.error)
    const selected = planning.PlanningSurface.readAndValidatePlan(root, workId)
    assert.equal(selected.ok, true, selected.error)
    delivered = surface.deliverStateProbe(text, 'run-final', 'call-final', 30n, { digest: selected.digest, path: selected.path })
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
  assert.equal(delivered.decision, 'Write', delivered.detail)
  assert.equal(delivered.detail, 'delivered')
  return { text: `${text}${delivered.bytes}`, incumbencyId: incumbencyIdOf(text), authorized: text }
}

test('WHAT[work-037] reprompt reopens the delivered work on a new tenure', () => {
  const delivered = deliveredOf('work-21')
  const view = surface.documentView(delivered.text)
  assert.equal(view.delivered, true)
  assert.equal(view.active, false, 'delivery clears the active incumbency')

  const reopened = surface.reopenProbe(delivered.text, delivered.incumbencyId)
  assert.equal(reopened.decision, 'Write', reopened.detail)
  assert.equal(reopened.detail, 'Reopened')
  assert.equal(reopened.names.includes('Reopened'), true)
  assert.equal(reopened.names.includes('IncumbencyOpened'), true)
  const restored = surface.documentView(`${delivered.text}${reopened.bytes}`)
  assert.equal(restored.delivered, false)
  assert.notEqual(restored.activeId, '')
  assert.notEqual(restored.activeId, delivered.incumbencyId, 'reopen opens a new tenure')
  assert.equal(restored.projection, 'S3')
  assert.equal(restored.authorized, false)
  assert.equal(restored.retiredCount, 3, 'retired history is not rewritten')
})

test('WHAT[work-037] a reopened work continues on a new tenure, not the delivered incumbency', () => {
  const delivered = deliveredOf('work-21b')
  const reopened = surface.reopenProbe(delivered.text, delivered.incumbencyId)
  assert.equal(reopened.decision, 'Write', reopened.detail)
  assert.equal(reopened.detail, 'Reopened')
  const active = `${delivered.text}${reopened.bytes}`
  const restored = surface.documentView(active)
  assert.notEqual(restored.activeId, delivered.incumbencyId)
  assert.equal(restored.authorized, false)
  assert.equal(restored.delivered, false)

  const second = surface.deliverProbe(active, 'run-again', 'call-again', 40n)
  assert.equal(second.decision, 'Write', second.detail)
  assert.equal(second.detail, 'Relayed')
  assert.notEqual(eventsOf(second.bytes)[0].incumbencyId, delivered.incumbencyId)
  assert.equal(eventsOf(second.bytes)[0].incumbencyId, restored.activeId)
})

test('WHAT[work-037] reopening without a delivery is rejected', () => {
  const opened = surface.encodeOpening('plan', 'work-21c', '/root', 0n)
  const active = surface.reopenProbe(opened, incumbencyIdOf(opened))
  assert.equal(active.decision, 'Refuse', 'an active tenure cannot reopen')
  assert.match(active.detail, /Only an inactive delivered work can reopen|No retirement to reopen/)

  const stayed = surface.reviewProbe(opened, incumbencyIdOf(opened), 'Stayed')
  const relayed = surface.deliverProbe(`${opened}${stayed.bytes}`, 'run-relay', 'call-relay', 10n)
  assert.equal(relayed.decision, 'Write', relayed.detail)
  const successorText = `${opened}${stayed.bytes}${relayed.bytes}`
  const relayedReopen = surface.reopenProbe(successorText, incumbencyIdOf(opened))
  assert.equal(relayedReopen.decision, 'Refuse', 'a Relayed retirement is not a delivery')
  assert.match(relayedReopen.detail, /Only an inactive delivered work can reopen|No retirement to reopen/)
})

test('WHAT[work-037] reopen keeps the predecessor range and not the tenure own delivery', () => {
  let text = surface.encodeOpening('plan', 'work-21d', '/root', 0n)
  const first = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  const relay1 = surface.deliverProbe(`${text}${first.bytes}`, 'run-1', 'call-1', 10n)
  text = `${text}${first.bytes}${relay1.bytes}`
  const secondView = surface.documentView(text)
  assert.equal(secondView.predecessor, '0,10', 'a successor inherits the retired tenure consumed range')

  const second = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  const relay2 = surface.deliverProbe(`${text}${second.bytes}`, 'run-2', 'call-2', 20n)
  text = `${text}${second.bytes}${relay2.bytes}`
  assert.equal(surface.documentView(text).predecessor, '10,20')

  const third = surface.reviewProbe(text, incumbencyIdOf(text), 'Promoted')
  const root = mkdtempSync(join(tmpdir(), 'wxs-work-037-range-'))
  let delivered
  try {
    const written = planning.PlanningSurface.rewritePlanAtomic(root, 'work-21d', '# plan\n')
    assert.equal(written.ok, true, written.error)
    const selected = planning.PlanningSurface.readAndValidatePlan(root, 'work-21d')
    assert.equal(selected.ok, true, selected.error)
    delivered = surface.deliverStateProbe(`${text}${third.bytes}`, 'run-3', 'call-3', 30n, { digest: selected.digest, path: selected.path })
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
  assert.equal(delivered.decision, 'Write', delivered.detail)
  const deliveredText = `${text}${third.bytes}${delivered.bytes}`
  const deliveredView = surface.documentView(deliveredText)
  assert.equal(deliveredView.latestRetirement, '20,30', 'the delivered retirement is the tenure own range')
  assert.equal(deliveredView.active, false)

  const reopened = surface.reopenProbe(deliveredText, incumbencyIdOf(text))
  assert.equal(reopened.decision, 'Write', reopened.detail)
  assert.equal(reopened.detail, 'Reopened')
  const restored = surface.documentView(`${deliveredText}${reopened.bytes}`)
  assert.notEqual(restored.activeId, incumbencyIdOf(text))
  assert.equal(restored.predecessor, '10,20', 'reopen keeps the range inherited at opening')
  assert.notEqual(restored.predecessor, restored.latestRetirement)
  assert.equal(restored.latestRetirement, '20,30')
})

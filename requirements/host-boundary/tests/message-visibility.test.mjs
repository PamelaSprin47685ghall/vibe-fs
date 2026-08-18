// requirements/host-boundary/tests/message-visibility.test.mjs
// HOST-BOUNDARY-008 — projection catch-up is event-driven: a session's
// `message.updated` signal wakes the bounded re-read; the ITimerPort deadline
// is only the backstop when no signal ever arrives. Settled waiters leave the
// registry (no leak), and a foreign session's signal never wakes a waiter.

import assert from 'node:assert/strict'
import test from 'node:test'

const surface = await import('../../../dist/OpenCode/Host/MessageVisibilitySurface.js')

// Duck-typed ITimerPort: each Delay(ms) records a handle whose promise is
// released manually; Cancel leaves the deadline permanently pending, matching
// the IDeadlineHandle contract.
const fakeTimer = () => {
  const deadlines = []
  const port = {
    Delay(ms) {
      let release
      const promise = new Promise((resolve) => {
        release = () => {
          if (!handle.cancelled) resolve()
        }
      })
      const handle = { ms, cancelled: false, Delay: promise, Cancel() { this.cancelled = true }, release }
      deadlines.push(handle)
      return handle
    },
    Dispose() {},
  }
  return { port, deadlines }
}

test('WHAT[HOST-BOUNDARY-008] message_visibility_signal_wakes_waiter_and_cancels_deadline', async () => {
  const timer = fakeTimer()
  const hub = surface.create(timer.port)

  const wait = surface.awaitChange(hub, 'session-a', 10)
  assert.equal(surface.pendingCount(hub, 'session-a'), 1)

  surface.notify(hub, 'session-a')
  await wait

  assert.equal(timer.deadlines[0].cancelled, true, 'event fast path must cancel the deadline backstop')
  assert.equal(surface.pendingCount(hub, 'session-a'), 0, 'settled waiter must leave the registry')
})

test('WHAT[HOST-BOUNDARY-008] deadline_backstop_resolves_when_no_signal_arrives', async () => {
  const timer = fakeTimer()
  const hub = surface.create(timer.port)

  const wait = surface.awaitChange(hub, 'session-b', 10)
  timer.deadlines[0].release()
  await wait

  assert.equal(surface.pendingCount(hub, 'session-b'), 0, 'deadline-settled waiter must leave the registry')
})

test('WHAT[HOST-BOUNDARY-008] foreign_session_signal_never_wakes_waiter', async () => {
  const timer = fakeTimer()
  const hub = surface.create(timer.port)

  let woke = false
  const wait = surface.awaitChange(hub, 'session-c', 10).then(() => { woke = true })

  surface.notify(hub, 'session-other')
  await Promise.resolve()
  assert.equal(woke, false, 'a foreign session signal must not resolve the waiter')
  assert.equal(surface.pendingCount(hub, 'session-c'), 1)

  timer.deadlines[0].release()
  await wait
  assert.equal(woke, true)
})

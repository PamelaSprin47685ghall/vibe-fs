import assert from 'node:assert/strict'
import test from 'node:test'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as projection from '../../../dist/Context/Companion/ProjectionSurface.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const scopeFor = (t) => {
  const scope = runtime.scope()
  t.after(() => runtime.dispose(scope))
  return scope
}
const main = (toml, requestId = 'request-main') => runtime.main({ requestId, toml })

// These exercise the actual mailbox and flight registry, not the catch-up coordinator.
test('WHAT[context-compression-018] waiting consumers receive the same typed material event', async (t) => {
  const scope = scopeFor(t)
  const first = runtime.park(scope, 'ses-blog')
  const second = runtime.park(scope, 'ses-blog')
  assert.equal(runtime.offerMaterial(scope, 'ses-blog', main('new work')), 'Delivered')
  const [a, b] = await Promise.all([first, second])
  assert.equal(a.kind, 'MaterialAvailable')
  assert.equal(a.context.kind, 'Main')
  assert.equal(a.context.toml, 'new work')
  assert.deepEqual(b, a)
  assert.equal(runtime.tryGetFlight(scope, 'ses-blog'), null)
})

test('WHAT[context-compression-018] material arriving before park is staged without claiming flight', async (t) => {
  const scope = scopeFor(t)
  assert.equal(runtime.offerParked(scope, 'ses-blog', main('older', 'request-old')), 'Staged')
  assert.equal(runtime.offerParked(scope, 'ses-blog', main('newer', 'request-new')), 'Staged')
  assert.equal(runtime.tryGetFlight(scope, 'ses-blog'), null)
  assert.equal((await runtime.park(scope, 'ses-blog')).context.toml, 'newer')
})

test('WHAT[context-compression-018] cancellation wakes pending consumers and preserves exact flight', async (t) => {
  const scope = scopeFor(t)
  const pending = runtime.park(scope, 'ses-blog')
  assert.equal(runtime.claimCurrentRequest(scope, 'ses-blog', main('in flight')), 'Claimed')
  runtime.cancelParked(scope, 'ses-blog')
  assert.deepEqual(await pending, { kind: 'Cancelled', context: null })
  assert.equal(runtime.peekCurrentRequest(scope, 'ses-blog').toml, 'in flight')
  assert.equal(runtime.releaseCurrentRequest(scope, 'ses-blog', 'request-main'), 'Released')
  assert.equal(runtime.tryGetFlight(scope, 'ses-blog'), null)
})

test('WHAT[context-compression-018] cancellation drops staged material without replacing the active request', async (t) => {
  const scope = scopeFor(t)
  runtime.offerParked(scope, 'ses-blog', main('staged', 'request-staged'))
  runtime.claimCurrentRequest(scope, 'ses-blog', main('flying'))
  runtime.cancelParked(scope, 'ses-blog')
  assert.equal(runtime.peekCurrentRequest(scope, 'ses-blog').toml, 'flying')
  const pending = runtime.park(scope, 'ses-blog')
  runtime.offerParked(scope, 'ses-blog', main('fresh', 'request-fresh'))
  assert.equal((await pending).context.toml, 'fresh')
})

test('WHAT[context-compression-018] disposing a scope cancels every pending session', async () => {
  const scope = runtime.scope()
  const a = runtime.park(scope, 'ses-a')
  const b = runtime.park(scope, 'ses-b')
  runtime.dispose(scope)
  assert.equal((await a).kind, 'Cancelled')
  assert.equal((await b).kind, 'Cancelled')
})

test('WHAT[context-compression-018] material and cancellation are isolated by session', async (t) => {
  const scope = scopeFor(t)
  const a = runtime.park(scope, 'ses-a')
  const b = runtime.park(scope, 'ses-b')
  runtime.offerParked(scope, 'ses-b', main('b'))
  assert.equal((await b).context.toml, 'b')
  runtime.cancelParked(scope, 'ses-a')
  assert.equal((await a).kind, 'Cancelled')
})

test('WHAT[context-compression-018] a flight lease releases its exact request', (t) => {
  const scope = scopeFor(t)
  const lease = runtime.claimFlight(scope, 'ses-blog', main('leased', 'request-lease'))
  assert.notEqual(lease, null)
  assert.equal(runtime.tryGetFlight(scope, 'ses-blog').toml, 'leased')
  lease.Dispose()
  assert.equal(runtime.currentRequest(scope, 'ses-blog'), null)
  runtime.releaseCurrentRequest(scope, 'ses-blog', 'request-lease')
  assert.equal(runtime.currentRequest(scope, 'ses-blog'), null)
})

test('WHAT[context-compression-018] projection with historic frames or tips is not an initial request shape', () => {
  const base = {
    blogger: 'ses_y', epoch: 0, kind: projection.normal,
    delta: { messageId: 'msg_d', items: [{ role: 'user', kind: 'text', text: 'work', truncated: false }] },
  }
  const render = (text) => `«${text}»`
  assert.equal(projection.build(render, { ...base, frames: [{ digest: 'sha-frame', body: 'prior work' }] }).isFirstTurnShape, false)
  assert.equal(projection.build(render, {
    ...base, frames: [], previousTips: [{ field: 'primitive-obsession', cycleId: 'c1' }],
  }).isFirstTurnShape, false)
})

test('WHAT[context-compression-018] fresh ordinary binding preserves its original provider message', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses_fresh_ordinary_binding'
    const userMessageID = 'msg-user-fresh-1'
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    const userOutput = {
      message: { id: userMessageID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: 'ordinary work' }],
    }
    await hooks['chat.message']({ sessionID, messageID: userMessageID }, userOutput)
    const providerOutput = {
      messages: [{ info: { id: userMessageID, sessionID, role: 'user' }, parts: userOutput.parts }],
    }
    await hooks['experimental.chat.messages.transform']({ sessionID }, providerOutput)
    assert.ok(providerOutput.messages.length > 0)
    assert.equal(providerOutput.messages[0].info.id, userMessageID)
  })
})

test.todo('WHAT[context-compression-018] real coordinator commits several bounded chunks and re-reads live coverage after each without a frozen frontier; GAP-104')
test.todo('WHAT[context-compression-018] durable producer between physical steps receives material under its existing authority and process death does not revive continuation; GAP-104')

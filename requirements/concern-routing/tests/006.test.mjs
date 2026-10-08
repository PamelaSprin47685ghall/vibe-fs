import assert from 'node:assert/strict'
import test from 'node:test'
import * as concern from '../../../dist/Interaction/Concern/Surface.js'

// 语言锚定：断言依赖英文投递回执文案（/Message accepted/）；
// WANXIANGSHU_PROVIDER_LANGUAGE 是语言阶梯最高优先级，显式设为英文，使断言不随宿主环境语言漂移。
process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

test('WHAT[concern-routing-006] retirement prevents old messages crossing into a same-concern replacement generation', () => {
  let state = concern.subscribe('owner-a', 'gen-1', 'build', 'build health', concern.empty()).state
  state = concern.publish('sender', 'msg-old', 'build', 'old message', state).state
  const oldBatch = concern.prepare('owner-a', state)
  state = concern.retire('owner-a', 'build', 'gen-1', state).state
  assert.equal(concern.publish('sender', 'after-retirement', 'build', 'too late', state).ok, false)
  assert.equal(concern.subscribe('owner-b', 'gen-2', 'build', 'changed meaning', state).ok, false)
  const rebound = concern.subscribe('owner-b', 'gen-2', 'build', 'build health', state)
  assert.equal(rebound.ok, true)
  assert.equal(rebound.appended, true)
  state = rebound.state
  assert.deepEqual(concern.prepare('owner-b', state).messages, [])
  assert.deepEqual(concern.prepare('owner-b', state).announcements, [{ id: 'build', concern: 'build health' }])
  assert.equal(concern.place('owner-a', oldBatch.announcedGenerations, oldBatch.deliveredMessages, state).ok, false)
  state = concern.applySubscribedClaim('owner-a', 'gen-1', 'build', 'build health', state).state
  const fresh = concern.publish('sender', 'msg-new', 'build', 'new generation', state)
  assert.equal(fresh.ok, true)
  assert.deepEqual(concern.prepare('owner-a', fresh.state).messages, [])
  assert.deepEqual(concern.prepare('owner-b', fresh.state).messages, [{ id: 'build', message: 'new generation' }])
})

const { withExecutablePlugin, acceptAuthorityRoot, openIncumbency, completeManagerLife } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
const { admit, context } = await import('./support/plugin.mjs')

test('WHAT[concern-routing-006] opening an owner incumbency preserves its already subscribed mailbox', async () => {
  await withExecutablePlugin(async (hooks, directory, created, runtime) => {
    const owner = 'opening-owner'
    const sender = 'opening-sender'
    const contender = 'opening-contender'
    await acceptAuthorityRoot(runtime, owner, 'manager', `root-${owner}`)
    await admit(runtime, sender)
    await admit(runtime, contender)
    const subscription = await hooks.tool.subscribe.execute({ id: 'opening-build', concern: 'build health' }, context(owner, 'opening-subscribe', 'manager'))
    assert.match(subscription, /is live for/)
    const before = await hooks.tool.publish.execute({ id: 'opening-build', message: 'pending before opening' }, context(sender, 'opening-before'))
    assert.match(before, /Message accepted/)
    await openIncumbency(runtime, owner, owner)
    const after = await hooks.tool.publish.execute({ id: 'opening-build', message: 'still live after opening' }, context(sender, 'opening-after'))
    assert.match(after, /Message accepted/)
    const conflict = await hooks.tool.subscribe.execute({ id: 'opening-build', concern: 'build health' }, context(contender, 'opening-conflict'))
    assert.match(conflict, /already bound incompatibly/)
  })
})

test('WHAT[concern-routing-006] the committed LifeCompleted Surface retires only its owner mailbox before returning', async () => {
  await withExecutablePlugin(async (hooks, directory, created, runtime) => {
    const owner = 'surface-retiring-owner'
    const sender = 'surface-retiring-sender'
    const decoy = 'surface-unrelated-owner'
    await admit(runtime, owner, 'manager')
    await admit(runtime, sender)
    await admit(runtime, decoy)
    await hooks.tool.subscribe.execute({ id: 'surface-build', concern: 'build health' }, context(owner, 'surface-subscribe', 'manager'))
    await hooks.tool.subscribe.execute({ id: 'surface-deploy', concern: 'deploy health' }, context(decoy, 'surface-subscribe-decoy'))
    await completeManagerLife(runtime, owner)
    const retired = await hooks.tool.publish.execute({ id: 'surface-build', message: 'after committed retirement' }, context(sender, 'surface-retired'))
    assert.doesNotMatch(retired, /Message accepted/)
    const unaffected = await hooks.tool.publish.execute({ id: 'surface-deploy', message: 'unrelated owner remains live' }, context(sender, 'surface-decoy'))
    assert.match(unaffected, /Message accepted/)
  })
})

test('WHAT[concern-routing-006] completing the actual owner life retires its mailbox before later publish', { todo: 'GAP-157: the production Suicide post-commit retirement append failure has no proved recovery' }, async () => {
  await withExecutablePlugin(async (hooks, directory, created, runtime) => {
    const owner = 'retiring-owner'
    const sender = 'retiring-sender'
    const decoy = 'unrelated-owner'
    const successor = 'successor-owner'
    await admit(runtime, owner, 'manager')
    await admit(runtime, sender)
    await admit(runtime, decoy)
    await admit(runtime, successor)
    await hooks.tool.subscribe.execute({ id: 'build', concern: 'build health' }, context(owner, 'subscribe', 'manager'))
    // An unrelated participant's mailbox is established before the termination
    // so isolation is observable on both sides of the lifecycle event.
    await hooks.tool.subscribe.execute({ id: 'deploy', concern: 'deploy health' }, context(decoy, 'subscribe-decoy'))
    const before = await hooks.tool.publish.execute({ id: 'build', message: 'before retirement' }, context(sender, 'before'))
    assert.match(before, /Message accepted/)
    const decoyBefore = await hooks.tool.publish.execute({ id: 'deploy', message: 'decoy live' }, context(sender, 'decoy-before'))
    assert.match(decoyBefore, /Message accepted/)
    await completeManagerLife(runtime, owner)
    const after = await hooks.tool.publish.execute({ id: 'build', message: 'after retirement' }, context(sender, 'after'))
    assert.doesNotMatch(after, /Message accepted/)
    // The terminated participant's mailbox retirement does not leak onto other owners.
    const decoyAfter = await hooks.tool.publish.execute({ id: 'deploy', message: 'decoy still live' }, context(sender, 'decoy-after'))
    assert.match(decoyAfter, /Message accepted/)
    // A successor earns a fresh generation only through an explicit
    // same-concern subscribe; the retired generation does not come back.
    await hooks.tool.subscribe.execute({ id: 'build', concern: 'build health' }, context(successor, 'rebound'))
    const fresh = await hooks.tool.publish.execute({ id: 'build', message: 'new generation' }, context(sender, 'fresh'))
    assert.match(fresh, /Message accepted/)
  })
})

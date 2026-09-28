import assert from 'node:assert/strict'
import test from 'node:test'
import { fission, harness, parsed } from './support/admission.mjs'
import * as fissionHost from '../../../dist/OpenCode/Host/FissionHostSurface.js'
import * as sessionBinding from '../../../dist/OpenCode/Host/SessionBindingSurface.js'
import { acceptAuthorityRoot, bindManagedChild, grantWorkOwned, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[intra-participant-parallelism-013] root admission rejects before reserving, reading work or creating resources', async () => {
  const { events, runtime } = harness({ parent: null })
  const owner = 'root-caller'
  const result = await fission.admit(runtime, owner, parsed())
  assert.equal(result.ok, false)
  assert.equal(result.reason, 'InvalidOrigin')
  assert.deepEqual(events, [['parent', owner]])
  assert.equal(fission.isActive(runtime, owner), false)
})

test('WHAT[intra-participant-parallelism-013] origin projection suppresses root fission and preserves child office entitlement', () => {
  assert.deepEqual(fissionHost.projectFissionToolVisibility(false, { fork: true, fission: true }), { fork: true, fission: false })
  assert.deepEqual(fissionHost.projectFissionToolVisibility(true, { fork: true, fission: true }), { fork: true, fission: true })
})

test('WHAT[intra-participant-parallelism-013] actual Engineer root chat message suppresses fission despite the role entitlement', async () => {
  await withExecutablePlugin(async hooks => {
    const sessionID = 'fission-root-provider-surface'
    const output = {
      message: {
        id: 'msg-fission-root-provider-surface', role: 'user', sessionID, agent: 'engineer',
        model: { providerID: 'host', modelID: 'placeholder' },
        tools: { fork: true, join: true, horizon: true, suicide: true, fission: true },
      },
      parts: [{ type: 'text', text: 'root work' }],
    }
    await hooks['chat.message']({ sessionID, agent: 'engineer' }, output)
    assert.equal(output.message.tools.fission, false)
    for (const tool of ['fork', 'join', 'horizon', 'suicide']) assert.equal(output.message.tools[tool], true)
  })
})

test('WHAT[intra-participant-parallelism-013] a bound Engineer child retains fission when the physical parent cache is empty', async () => {
  await withExecutablePlugin(async hooks => {
    const sessionID = 'fission-bound-child-provider-surface'
    bindManagedChild('fission-binding-parent', sessionID, 'engineer')
    const output = {
      message: {
        id: 'msg-fission-bound-child-provider-surface', role: 'user', sessionID, agent: 'engineer',
        model: { providerID: 'host', modelID: 'placeholder' }, tools: { fork: true, fission: true },
      },
      parts: [{ type: 'text', text: 'Interrupt the active join.' }],
    }
    try {
      await hooks['chat.message']({ sessionID, agent: 'engineer' }, output)
      assert.equal(output.message.tools.fission, true)
      assert.equal(output.message.tools.fork, true)
    } finally {
      sessionBinding.drop(sessionID)
    }
  })
})

test('WHAT[intra-participant-parallelism-013] forced Engineer root invocation rejects origin before parsing and without Host effects', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'fission-root-origin'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')
    await grantWorkOwned(runtime, sessionID)
    const result = await hooks.tool.fission.execute(
      { prompts: 'only one lane' },
      { sessionID, agent: 'engineer', callID: 'call-root-fission', messageID: 'run-root-fission' },
    )
    assert.match(result, /user-facing\/root/i)
    assert.doesNotMatch(result, /at least two|至少需要两条/i)
    assert.deepEqual(createdIds, [])
    assert.deepEqual(runtime.prompts, [])
    assert.deepEqual(runtime.abortedIds, [])
  })
})

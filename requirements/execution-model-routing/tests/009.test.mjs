import assert from 'node:assert/strict'
import test from 'node:test'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

// This exercises low-level SDK encoding; internal synthetic dispatch must still omit the model.
test('WHAT[execution-model-routing-009] SDK target encoding separates model identity from reasoning variant', async () => {
  let payload
  const port = routing.createSdkClientPort({ session: { promptAsync: async (value) => {
    payload = value
    return {}
  } } })
  await routing.sendPrompt(port, 'session', 'hello', {
    agent: 'engineer',
    model: { providerID: 'provider', modelID: 'model', variant: 'high' },
  })
  assert.deepEqual(payload.body.model, { providerID: 'provider', modelID: 'model' })
  assert.equal(payload.body.variant, 'high')
  assert.equal(payload.variant, 'high')
  assert.deepEqual(payload.model, { providerID: 'provider', modelID: 'model' })
})

test('WHAT[execution-model-routing-009] actual chat.message reads session identity from output and projects the configured scheduler target', async () => {
  await withExecutablePlugin(async (hooks) => {
    const output = { message: {
      id: 'msg-output-session', role: 'user', sessionID: 'ses-output-session', agent: 'engineer',
      model: { providerID: 'host', modelID: 'placeholder' },
    }, parts: [] }
    await hooks['chat.message']({ messageID: 'msg-output-session' }, output)
    assert.deepEqual({ ...output.message.model }, { providerID: 'provider', modelID: 'engineer-model', variant: 'none' })
  })
})


test('WHAT[execution-model-routing-009] chat.params cannot create missing admission and internal sends stay model-free', async () => {
  const { withExecutablePlugin, bindManagedChild } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  const chatParams = await import('../../../dist/OpenCode/Host/ChatParamsSurface.js')
  const dispatch = await import('../../../dist/Interaction/Dispatch/DispatchSurface.js')

  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses_emr009_unadmitted'
    const messageID = 'msg-emr009-unadmitted'
    await bindManagedChild(runtime, 'ses_emr009_parent', sessionID, 'engineer')

    // Durable acceptance without a committed model lease: chat.params must
    // fail closed rather than invent the missing admission (WHAT 009).
    const accepted = await dispatch.acceptManagedExternal(runtime.journal, sessionID, messageID, 'engineer')
    assert.equal(accepted.ok, true, JSON.stringify(accepted))
    const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
    const rejected = chatParams.applyWith(
      runtime.journal,
      { sessionID, messageID, agent: 'engineer', model: { providerID: 'anthropic', id: 'fast-haiku' } },
      output,
    )
    assert.equal(rejected.ok, false, 'chat.params must not create a missing admission')
    assert.match(rejected.error, /no committed execution lease/)
    assert.equal(output.model.modelID, 'fast-haiku', 'the caller model is untouched')

    // Internal synthetic dispatch stays model-free on the same runtime.
    const owner = await dispatch.acceptHumanRootSelection(
      runtime.journal,
      'ses_emr009_owner',
      'msg-emr009-owner',
      {
        kind: 'RootSelection',
        ownerSession: null,
        ownerLogicalRun: null,
        ownerAuthorityRoot: null,
        participantIdentity: {
          participant: 'manager', role: 'manager', persona: 'Lead',
          personaCatalogVersion: 1, origin: 'ResolvedAtRoot',
        },
      },
    )
    assert.equal(owner.ok, true, owner.error)
    const seed = (await import('../../../dist/Interaction/Authority/RuntimeSurface.js')).issueInheritedIdentitySeed('engineer', owner.profile)
    assert.equal(seed.ok, true, seed.error)
    const seen = []
    const port = {
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      SendPrompt: async (session, text, options) => {
        seen.push({ session, text, model: options?.model ?? null })
        return dispatch.admittedWithReceipt('receipt-emr009')
      },
    }
    const sent = await dispatch.sendAgentOwnerRoot(port, runtime.journal, 'ses_emr009_child', 'internal work', seed.value)
    assert.equal(sent.ok, true, sent.error)
    assert.equal(seen.length, 1)
    assert.equal(seen[0].model, null, 'internal synthetic send stays model-free')
  })
})
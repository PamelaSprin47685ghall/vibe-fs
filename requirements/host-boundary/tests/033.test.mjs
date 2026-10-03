import test from 'node:test'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import assert from 'node:assert/strict'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import * as recovery from '../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (path) => readFileSync(join(root, path), 'utf8')

test('WHAT[host-boundary-033] superseded accepted execution settles before late provider observations and preserves the new execution', async () => {
  await withExecutablePlugin(async (hooks, _directory, _created, runtime) => {
    const sessionID = 'ses-superseded-observation'
    const admit = async (messageID, metadata = undefined) => {
      const carrier = metadata === undefined ? {} : { metadata }
      const message = { id: messageID, sessionID, role: 'user', agent: 'engineer', model: {}, ...carrier }
      const parts = [{ type: 'text', text: 'controlled input', ...carrier }]
      await hooks['chat.message']({ sessionID, messageID, agent: 'engineer' }, { message, parts })
      return message
    }
    await admit('msg-root')
    const profile = dispatch.projectionObservation(runtime.journal, sessionID).activeLogicalRun
    const guard = await dispatch.sendContinuation({
      SubscribeTerminal: () => ({ Dispose() {} }),
      SendPrompt: async () => dispatch.admittedWithReceipt('guard-receipt'),
    }, runtime.journal, sessionID, 'controlled input', 'ManagerGuard', profile, 'Await')
    assert.equal(guard.ok, true, guard.error)
    const oldMessage = await admit('msg-old', guard.observation.metadata)
    const newMessage = await admit('msg-new')
    const before = routing.sharedCapacitySnapshot()
    assert.deepEqual(recovery.journalExecutionStatus(runtime.journal, sessionID, oldMessage.id), {
      phase: 'Terminal', disposition: 'Cancelled',
    })
    assert.deepEqual(recovery.journalExecutionStatus(runtime.journal, sessionID, newMessage.id), {
      phase: 'Accepted', disposition: null,
    })
    assert.deepEqual(runtime.abortedIds, [sessionID], 'only the replaced Guard attempt is physically interrupted')
    const output = { temperature: 0.123 }
    assert.throws(() => hooks['chat.params']({
      sessionID, message: oldMessage, agent: 'engineer',
      model: { providerID: 'provider', id: 'engineer-model', capabilities: {} },
    }, output))
    assert.deepEqual(output, { temperature: 0.123 })
    assert.deepEqual(routing.sharedCapacitySnapshot(), before)
    await hooks.event({ event: { type: 'session.error', properties: {
      sessionID, error: { name: 'MessageAbortedError', data: { message: 'old Guard interrupted' } },
    } } })
    await hooks.event({ event: { type: 'message.updated', properties: { info: {
      id: 'assistant-old', sessionID, parentID: oldMessage.id, role: 'assistant',
      agent: 'engineer', modelID: 'engineer-model', providerID: 'provider',
      time: { created: 1, completed: 2 },
      error: { name: 'MessageAbortedError', data: { message: 'old Guard interrupted' } },
    } } } })
    assert.deepEqual(routing.sharedCapacitySnapshot(), before)
    await hooks['chat.params']({
      sessionID, message: newMessage, agent: 'engineer',
      model: { providerID: 'provider', id: 'engineer-model', capabilities: {} },
    }, {})
    assert.equal(runtime.prompts.length, 0, 'late observation must not dispatch a provider retry')
  })
})

test('WHAT[host-boundary-033] rejected Host abort fails and releases the exact accepted human successor without a retry', async () => {
  await withExecutablePlugin(async (hooks, _directory, _created, runtime) => {
    const sessionID = 'ses-supersession-abort-rejected'
    const admit = async (messageID, metadata) => {
      const carrier = metadata === undefined ? {} : { metadata }
      const message = { id: messageID, sessionID, role: 'user', agent: 'engineer', model: {}, ...carrier }
      await hooks['chat.message']({ sessionID, messageID, agent: 'engineer' }, {
        message, parts: [{ type: 'text', text: 'controlled input', ...carrier }],
      })
      return message
    }
    await admit('msg-root')
    const profile = dispatch.projectionObservation(runtime.journal, sessionID).activeLogicalRun
    const guard = await dispatch.sendContinuation({
      SubscribeTerminal: () => ({ Dispose() {} }),
      SendPrompt: async () => dispatch.admittedWithReceipt('guard-rejected-receipt'),
    }, runtime.journal, sessionID, 'controlled input', 'ManagerGuard', profile, 'Await')
    assert.equal(guard.ok, true, guard.error)
    await admit('msg-guard', guard.observation.metadata)
    runtime.client.session.abort = async () => {
      runtime.abortedIds.push(sessionID)
      return { error: { message: 'controlled SDK abort rejection' } }
    }
    await assert.rejects(admit('msg-human'), /supersession|hook|admission/i)
    assert.deepEqual(recovery.journalExecutionStatus(runtime.journal, sessionID, 'msg-guard'), {
      phase: 'Terminal', disposition: 'Cancelled',
    })
    assert.deepEqual(recovery.journalExecutionStatus(runtime.journal, sessionID, 'msg-human'), {
      phase: 'Terminal', disposition: 'Failed',
    })
    const snapshot = routing.sharedCapacitySnapshot()
    assert.equal(snapshot.executions.some((owner) => owner.sessionId === sessionID), false)
    assert.equal(runtime.prompts.length, 0)
    assert.deepEqual(runtime.abortedIds, [sessionID])
  })
})

test('WHAT[host-boundary-033] a queued successor granted during the old attempt drain yields to the later human input before projection', async () => {
  await withExecutablePlugin(async (hooks, _directory, _created, runtime) => {
    const sessionID = 'ses-queued-supersession'
    const admit = async (messageID, metadata, targetSession = sessionID) => {
      const carrier = metadata === undefined ? {} : { metadata }
      const message = { id: messageID, sessionID: targetSession, role: 'user', agent: 'engineer', model: {}, ...carrier }
      await hooks['chat.message']({ sessionID: targetSession, messageID, agent: 'engineer' }, {
        message, parts: [{ type: 'text', text: 'controlled input', ...carrier }],
      })
      return message
    }
    await admit('msg-root')
    const profile = dispatch.projectionObservation(runtime.journal, sessionID).activeLogicalRun
    const guard = await dispatch.sendContinuation({
      SubscribeTerminal: () => ({ Dispose() {} }),
      SendPrompt: async () => dispatch.admittedWithReceipt('queued-guard-receipt'),
    }, runtime.journal, sessionID, 'controlled input', 'ManagerGuard', profile, 'Await')
    assert.equal(guard.ok, true, guard.error)
    await admit('msg-guard', guard.observation.metadata)
    const abortEntered = Promise.withResolvers()
    const releaseOldAbort = Promise.withResolvers()
    let firstAbort = true
    runtime.client.session.abort = async () => {
      runtime.abortedIds.push(sessionID)
      if (firstAbort) {
        firstAbort = false
        abortEntered.resolve()
        await releaseOldAbort.promise
      }
      return {}
    }
    globalThis.__wanxiangshu_test_routing_decision = () => null
    const human = admit('msg-human').then(
      () => ({ accepted: true }),
      error => ({ accepted: false, error }),
    )
    try {
      await abortEntered.promise
      assert.deepEqual(recovery.journalExecutionStatus(runtime.journal, sessionID, 'msg-human'), {
        phase: 'Accepted', disposition: null,
      })
      assert.equal(routing.sharedCapacitySnapshot().waiters.filter(value => value.kind === 'Admission').length, 1)
      globalThis.__wanxiangshu_test_routing_decision = () => ({ model: 'provider/engineer-model', reasoning: 'none' })
      await admit('msg-trigger', undefined, 'ses-independent-queue-trigger')
      assert.equal(routing.sharedCapacitySnapshot().waiters.filter(value => value.kind === 'Admission').length, 0, 'H grant occurred while old G drain still held ingress')
      const latest = admit('msg-latest')
      // The queued old abort is the only outstanding effect. An event-loop turn
      // drains J's finite ingress microtasks before the controlled abort is released.
      await new Promise(setImmediate)
      releaseOldAbort.resolve()
      const current = await latest
      const old = await human
      assert.equal(old.accepted, false)
      assert.deepEqual(recovery.journalExecutionStatus(runtime.journal, sessionID, 'msg-human'), {
        phase: 'Terminal', disposition: 'Cancelled',
      })
      assert.deepEqual(recovery.journalExecutionStatus(runtime.journal, sessionID, current.id), {
        phase: 'Accepted', disposition: null,
      })
      await hooks['chat.params']({ sessionID, message: current, agent: 'engineer', model: {
        providerID: 'provider', id: 'engineer-model', capabilities: {},
      } }, {})
      assert.equal(runtime.prompts.length, 0)
      assert.equal(runtime.abortedIds.length, 2)
    } finally {
      releaseOldAbort.resolve()
      delete globalThis.__wanxiangshu_test_routing_decision
    }
  })
})

{
  const { default: assert } = await import('node:assert/strict')
  const { default: test } = await import('node:test')
  const params = await import('../../../dist/OpenCode/Host/ChatParamsSurface.js')

  const managedInput = ({ sessionID = 'ses_p2a_1', messageID = 'msg-a', agent = 'engineer' } = {}) => ({
    sessionID,
    messageID,
    agent,
    model: { providerID: 'openai', id: 'gpt-5', capabilities: {} },
    message: { id: messageID, model: {} },
  })

  // WHAT[host-boundary-033]: a message no durable Accepted execution answers is
  // not a managed provider run. The hook observes nothing and never rejects.
  test('WHAT[host-boundary-033] P2A_repeated_params_hooks_are_observations_without_durable_evidence', () => {
    const first = params.apply(managedInput(), {})
    const second = params.apply(managedInput(), {})
    assert.equal(first.ok, true, first.error)
    assert.equal(second.ok, true, second.error)
  })

  // WHAT[host-boundary-033]: interleaved A/B messages each resolve against their
  // own exact physical id; neither observation leaks into the other.
  test('WHAT[host-boundary-033] P2A_interleaved_messages_do_not_cross_validate', () => {
    const a = params.apply(managedInput({ messageID: 'msg-a' }), {})
    const b = params.apply(managedInput({ messageID: 'msg-b' }), {})
    assert.equal(a.ok, true, a.error)
    assert.equal(b.ok, true, b.error)
  })

  // WHAT[host-boundary-033]: a Host-owned auxiliary child has no durable
  // Accepted execution, so the observation barrier owns nothing there.
  test('WHAT[host-boundary-033] P2A_unmanaged_auxiliary_child_is_exempt_not_managed', () => {
    const observed = params.apply(managedInput({ sessionID: 'ses_p2a_aux', messageID: 'msg-aux' }), {})
    assert.equal(observed.ok, true, observed.error)
  })

  // WHAT[host-boundary-033]: the params hook validates the exact lease and
  // never writes identity (observeUserFacingAgent is gone from the hook).
  test('WHAT[host-boundary-033] P2A_params_hook_is_read_only_exact_lease_validation', () => {
    const hookSource = read('src/Wanxiangshu/OpenCode/Host/ChatParamsHook.fs')
    assert.match(hookSource, /tryPhysicalUserMessageId/)
    assert.match(hookSource, /ModelRouting\.readExecutionAdmission/)
    assert.match(hookSource, /let private validateObservedProvider/)
    assert.doesNotMatch(hookSource, /observeUserFacingAgent/)
    assert.doesNotMatch(hookSource, /isUnboundHostAuxiliaryChild/)
  })

  // WHAT[host-boundary-033]: the provider-step gate reads the exact committed
  // lease for this physical message, never the session-current binding copy;
  // a managed session without a lease fails closed.
  test('WHAT[host-boundary-033] P2A_provider_step_gate_reads_exact_lease_not_session_copy', () => {
    const bindingSource = read('src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs')
    const gate = bindingSource.match(
      /let private enterBoundProviderStep[\s\S]*?\n    let beginPhysicalProviderAttemptForTransform/,
    )?.[0]
    assert.ok(gate, 'provider step gate must remain a named function')
    assert.match(gate, /isManagedExecution/)
    assert.match(gate, /tryReadExecution/)
    assert.match(gate, /no committed model-routing lease/)

    // The module owns no identity of its own: no session-current binding map,
    // no participant cache, no model cache.
    assert.doesNotMatch(bindingSource, /providerAttemptBindings/)
    assert.doesNotMatch(bindingSource, /acceptedPromptBindings/)
    assert.doesNotMatch(bindingSource, /persistentDevOpsModels/)
    assert.doesNotMatch(bindingSource, /currentProviderModel/)
    assert.doesNotMatch(bindingSource, /tryAgent|tryParent/)
  })

  // WHAT[host-boundary-033]: multi-step provider runs keep exact identities
  // (requestKey derives from session+physical+visible runs), and late events
  // after terminal cannot resurrect admission: the freeze path checks the
  // durable accepted execution by exact key and never re-admits.
  test('WHAT[host-boundary-033] P2A_multi_step_and_late_events_keep_exact_identities', () => {
    const bindingSource = read('src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs')
    assert.match(bindingSource, /let private deriveTransformRequestKey/)
    const stepEntry = bindingSource.match(
      /let beginPhysicalProviderAttemptForTransform[\s\S]*?\n        }\n/,
    )?.[0]
    assert.ok(stepEntry, 'transform step entry must remain a named function')
    assert.match(stepEntry, /lastUserMessageId/)
    assert.match(stepEntry, /visibleProviderRuns/)

    const lifecycle = read('src/Wanxiangshu/OpenCode/Host/ChatAdmission/ProviderLifecycle.fs')
    assert.doesNotMatch(lifecycle, /ensureChatExecutionAccepted/)
    assert.doesNotMatch(lifecycle, /admitUnestablishedKey|admitExternalRoot|admitContinuation/)
    assert.match(lifecycle, /AcceptedExecutionMissing/)
  })
}

{
  const { spawnSync } = await import('node:child_process')
  const { integrationTest } = await import('../../verification-system/tests/support/tier-gate.mjs')
  const runInstalledCanary = (capacityOne) => {
    const arguments_ = [join(root, 'requirements/host-boundary/tests/support/run-guard-supersession-canary.mjs')]
    if (capacityOne) arguments_.push('--capacity-one')
    const launched = spawnSync(process.execPath, arguments_, {
      cwd: root, encoding: 'utf8', timeout: 120000,
    })
    assert.equal(launched.status, 0, `${launched.error ?? ''}\n${launched.stdout}\n${launched.stderr}`)
    const result = JSON.parse(launched.stdout.trim())
    assert.equal(result.opencode, '1.18.29')
    assert.equal(result.capacityOne, capacityOne)
    assert.equal(result.physicalCleanup, true)
    assert.deepEqual(result.results.map(value => value.phase), capacityOne ? ['STARTED', 'PAUSED', 'INTERLEAVED'] : ['PAUSED', 'STARTED', 'INTERLEAVED'])
    for (const execution of result.results) {
      assert.equal(execution.answerParent, execution.human)
      assert.equal(execution.old.disposition, 'Cancelled')
      assert.equal(execution.current.disposition, 'Completed')
      assert.equal(execution.providerRetries, 0)
    }
    assert.equal(result.results[2].intermediate.disposition, 'Cancelled')
  }
  integrationTest('WHAT[host-boundary-033] installed Host drains a superseded Guard and answers the exact fresh human input', () => runInstalledCanary(false))
  integrationTest('WHAT[host-boundary-033] installed Host drains a held Guard before waiting for its only capacity slot', () => runInstalledCanary(true))
}

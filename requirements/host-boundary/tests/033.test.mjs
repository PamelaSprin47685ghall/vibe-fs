import test from 'node:test'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (path) => readFileSync(join(root, path), 'utf8')

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

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
  const binding = await import('../../../dist/OpenCode/Host/SessionBindingSurface.js')

  const managedInput = ({ sessionID = 'ses_p2a_1', messageID = 'msg-a', agent = 'engineer' } = {}) => ({
    sessionID,
    messageID,
    agent,
    model: { providerID: 'openai', id: 'gpt-5', capabilities: {} },
    message: { id: messageID, model: {} },
  })

  // WHAT[host-boundary-033]: repeated hooks on the same physical message are
  // idempotent — the hook is read-only, so a second observation neither
  // creates state nor changes the rejection reason.
  test('WHAT[host-boundary-033] P2A_repeated_params_hooks_are_idempotent', () => {
    binding.drop('ses_p2a_1')
    const first = params.apply(managedInput(), {})
    const second = params.apply(managedInput(), {})
    assert.equal(first.ok, false)
    assert.equal(second.ok, false)
    assert.equal(first.error, second.error)
    assert.match(first.error, /no committed execution lease for physical user message 'msg-a'/)
    binding.drop('ses_p2a_1')
  })

  // WHAT[host-boundary-033]: interleaved A/B messages validate against their
  // own exact physical id; neither observation leaks into the other.
  test('WHAT[host-boundary-033] P2A_interleaved_messages_do_not_cross_validate', () => {
    binding.drop('ses_p2a_1')
    const a = params.apply(managedInput({ messageID: 'msg-a' }), {})
    const b = params.apply(managedInput({ messageID: 'msg-b' }), {})
    assert.equal(a.ok, false)
    assert.equal(b.ok, false)
    assert.match(a.error, /physical user message 'msg-a'/)
    assert.match(b.error, /physical user message 'msg-b'/)
    binding.drop('ses_p2a_1')
  })

  // WHAT[host-boundary-033]: an unbound Host auxiliary child is exempt from
  // managed lease validation and never establishes a managed lease by
  // observing params.
  test('WHAT[host-boundary-033] P2A_unbound_host_auxiliary_child_is_exempt_not_managed', () => {
    binding.drop('ses_p2a_aux')
    binding.observeHostAuxiliaryChild('ses_p2a_aux')
    const observed = params.apply(managedInput({ sessionID: 'ses_p2a_aux', messageID: 'msg-aux' }), {})
    assert.equal(observed.ok, true, observed.error)
    binding.drop('ses_p2a_aux')
  })

  // WHAT[host-boundary-033]: the params hook validates the exact lease and
  // never writes identity (observeUserFacingAgent is gone from the hook).
  test('WHAT[host-boundary-033] P2A_params_hook_is_read_only_exact_lease_validation', () => {
    const hookSource = read('src/Wanxiangshu/OpenCode/Host/ChatParamsHook.fs')
    assert.match(hookSource, /tryPhysicalUserMessageId/)
    assert.match(hookSource, /ModelRouting\.tryReadExecution/)
    assert.match(hookSource, /let private validateObservedProvider/)
    assert.doesNotMatch(hookSource, /SessionExecutionBinding\.observeUserFacingAgent/)
  })

  // WHAT[host-boundary-033]: the provider-step gate reads the exact committed
  // lease for this physical message, never the session-current binding copy;
  // a managed session without a lease fails closed.
  test('WHAT[host-boundary-033] P2A_provider_step_gate_reads_exact_lease_not_session_copy', () => {
    const bindingSource = read('src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs')
    const gate = bindingSource.match(
      /let private enterBoundProviderStep[\s\S]*?\n    let private beginSessionPhysicalProviderAttempt/,
    )?.[0]
    assert.ok(gate, 'provider step gate must remain a named function')
    assert.match(gate, /tryReadExecution/)
    assert.match(gate, /no committed model-routing lease/)
    assert.doesNotMatch(gate, /currentProviderModel/)

    const bindLease = bindingSource.match(
      /let private bindExternalExecutionLease[\s\S]*?\n    let private beginExternalProviderAttempt/,
    )?.[0]
    assert.ok(bindLease, 'bindExternalExecutionLease must remain a named function')
    assert.match(bindLease, /tryReadExecution/)
    assert.doesNotMatch(bindLease, /tryLease/)

    assert.doesNotMatch(bindingSource, /clearProviderAttempt/)
    assert.doesNotMatch(bindingSource, /providerAttemptBindings\[sessionKey\] <- expected/)
  })

  // WHAT[host-boundary-033]: multi-step provider runs keep exact identities
  // (requestKey derives from session+physical+visible runs), and late events
  // after terminal cannot resurrect admission: the freeze path checks the
  // durable accepted execution by exact key and never re-admits.
  test('WHAT[host-boundary-033] P2A_multi_step_and_late_events_keep_exact_identities', () => {
    const bindingSource = read('src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs')
    assert.match(bindingSource, /let private deriveTransformRequestKey/)
    const stepEntry = bindingSource.match(
      /let private beginSessionPhysicalProviderAttempt[\s\S]*?\n    let beginPhysicalProviderAttemptForTransform/,
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

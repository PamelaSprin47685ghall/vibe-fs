import * as owner from '../../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

export const current = () => owner.providerFailureProjection.forAuthority('logical-retry', 'root-retry')
export const input = (failure = 'ProviderTransient', requestKind = 'WorkMain') => ({
  failure,
  phase: 'ProviderStarted',
  executionKey: { sessionId: 'session-retry', physicalUserMessageId: 'physical-failed' },
  capacityFence: null,
  provider: {
    logicalRun: 'logical-retry', providerRun: 'provider-failed', requestKind,
    retryBudget: 'Available', breaker: 'Closed',
  },
})

// Retry.attempt itself supplies the phase, capacity and policy budget from its
// current projection. This adapter reuses the existing typed-input decoder.
export const run = (failure, state, admission = 'RetryAuthorized', requestKind = 'WorkMain') => {
  const events = []
  const completed = owner.retryAttempt(input(failure, requestKind), state,
    async authorization => { events.push({ action: 'admit', authorization }); return admission },
    async authorization => { events.push({ action: 'redispatch', authorization }) })
  return { events, completed }
}

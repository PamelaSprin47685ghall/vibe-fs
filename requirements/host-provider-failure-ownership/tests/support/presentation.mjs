import * as presentation from '../../../../dist/OpenCode/Host/ProviderFailurePresentation.js'

// This classifier is not wired to a production presentation emitter.
export const input = (failure, change = {}) => ({
  failure,
  phase: 'ProviderStarted',
  executionKey: { sessionId: 'session-presentation', physicalUserMessageId: 'physical-presentation' },
  capacityFence: null,
  provider: {
    logicalRun: 'logical-presentation', providerRun: 'provider-presentation',
    requestKind: 'WorkMain', retryBudget: 'Available', breaker: 'Closed',
  },
  ...change,
})
export const classify = (failure, episode, change) => presentation.classifyPolicyInput(input(failure, change), episode)

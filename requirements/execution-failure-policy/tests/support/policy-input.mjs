export const executionKey = { sessionId: 'ses-failure-policy', physicalUserMessageId: 'msg-failure-policy' }
export const capacityFence = { reference: 'fence-failure-policy' }
export const provider = {
  logicalRun: 'logical-failure-policy', providerRun: 'provider-failure-policy',
  requestKind: 'WorkMain', retryBudget: 'Available', breaker: 'Closed',
}
export const input = (change = {}) => ({
  failure: 'ProtocolRejection', phase: 'ProviderStarted', executionKey, capacityFence, provider, ...change,
})
export const failures = [
  'LocalInvariant', 'ProtocolRejection', 'AuthorizationDenied', 'UserCancelled', 'Superseded',
  'CapacityQueueFull', 'ProviderTransient', 'ProviderPermanent', 'AcceptanceUnknown',
  'StreamInterruptedAfterFirstToken',
  { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
  { kind: 'PersistenceFailure', commitment: 'Committed' },
  { kind: 'PersistenceFailure', commitment: 'Unknown' },
]
export const phases = ['NoAcceptedFact', 'AcceptedBeforeProvider', 'ProviderStarted', 'Terminal']

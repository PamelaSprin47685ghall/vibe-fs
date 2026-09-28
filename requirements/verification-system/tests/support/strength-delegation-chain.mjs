// The chain's ownerSessionId must equal the Prepared fact's owner: the fold
// rejects a mismatched owner with PreparedBindingMismatch (semantic cut).
// A Strength Prepared fact carries a deterministic parent edge to its DelegationBound
// fact (parentsFor in src/Wanxiangshu/Strength/Persistence/Store.fs), so the store
// rejects a lone Prepared append with StorageInvalid.MissingParent. Callers must
// append DelegationRequested -> DelegationBound for the same decision first, and
// must derive the Bound EventId with the same digest that will append Prepared:
// tests appending through strength.storeAppend pass their own sha256, tests
// publishing through the durability port must append the chain with
// strength.durabilityAppend (host digest), never with a test-local hash.

export const strengthDelegationChainEvents = (strength, ids) => ({
  requested: strength.eventRequested({
    decisionId: ids.decisionId,
    ownerSessionId: ids.ownerSessionId,
    ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
    sourcePhysicalUserMessageId: 'user-1',
    sourceProviderRun: ids.targetProviderRun,
    sourceToolCallIds: ['call-1'],
    requestedRounds: 1,
    contractRevision: 1,
  }),
  bound: strength.eventBound(ids.decisionId, ids.targetProviderRun, ids.replicaSessionId, ids.anchorDigest),
})

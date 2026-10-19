namespace Wanxiangshu.Enforcer.Cycle

module EnforcerCycleCommit =

    [<RequireQualifiedAccess>]
    type CycleCommitOutcome =
        | KnownCommitted
        | KnownNotCommitted of reason: string
        | CommitUnknown of reason: string
        | NoNewWriteReleaseFailed of Wanxiangshu.Persistence.Journal.JournalOutcome.JournalAppendFailure

    val commitCycle:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        Wanxiangshu.Foundation.Identity.ToolCallId list ->
        Wanxiangshu.Enforcer.Cycle.EnforcerCycle.CanonicalCycle ->
        Wanxiangshu.Context.Companion.Blogger.BloggerMainRequestContext option ->
            System.Threading.Tasks.Task<CycleCommitOutcome>

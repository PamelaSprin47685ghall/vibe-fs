namespace Wanxiangshu.Enforcer.Cycle

module EnforcerCycleCommit =

    [<RequireQualifiedAccess>]
    type CycleCommitOutcome =
        | KnownCommitted
        | KnownNotCommitted of reason: string
        | CommitUnknown of reason: string

    val commitCycle:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        Wanxiangshu.Foundation.Identity.ToolCallId list ->
        Wanxiangshu.Enforcer.Cycle.EnforcerCycle.CanonicalCycle ->
        Wanxiangshu.Context.Companion.Blogger.BloggerMainRequestContext option ->
            System.Threading.Tasks.Task<CycleCommitOutcome>

    val commitSquash:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        Wanxiangshu.Context.Companion.Blogger.BloggerSquashRequestContext ->
        string ->
            System.Threading.Tasks.Task<CycleCommitOutcome>

namespace Wanxiangshu.Enforcer.Cycle

module BloggerRecoveryProbe =

    [<Literal>]
    val BloggerMissingToolRepairKind: string = "blogger-missing-tool"

    [<Literal>]
    val BloggerAabbRepairKind: string = "blogger-aabb"

    val repairClaimedForKind:
        journal: Wanxiangshu.Persistence.Journal.AgentJournal ->
        bloggerSessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        requestId: Wanxiangshu.Foundation.Identity.BloggerRequestId ->
        terminalRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        repairKind: string ->
            bool

    val repairClaimedFor:
        journal: Wanxiangshu.Persistence.Journal.AgentJournal ->
        bloggerSessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        requestId: Wanxiangshu.Foundation.Identity.BloggerRequestId ->
        terminalRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
            bool

    val repairDispatchExists:
        projections: Wanxiangshu.Composition.Durable.AgentProjectionSet ->
        bloggerSessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        requestId: Wanxiangshu.Foundation.Identity.BloggerRequestId ->
        terminalRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        repairKind: string ->
            bool

    val repairIssuedForKind:
        journal: Wanxiangshu.Persistence.Journal.AgentJournal ->
        bloggerSessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        requestId: Wanxiangshu.Foundation.Identity.BloggerRequestId ->
        terminalRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        repairKind: string ->
            bool

    val terminalRequestOwnershipForPhysicalMessage:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext ->
        Wanxiangshu.Foundation.Identity.PhysicalUserMessageId ->
            Wanxiangshu.Context.Companion.Blogger.BloggerTerminalRequestOwnership

    /// The physical message landed as a protocol repair scoped to this request.
    val isRequestScopedRepairPrompt:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext ->
        Wanxiangshu.Foundation.Identity.PhysicalUserMessageId ->
            bool

    val terminalRequestOwnershipForProviderRun:
        tryPhysicalParent:
            (Wanxiangshu.Foundation.Identity.ProviderRunIdentity
                -> obj list
                -> Wanxiangshu.Foundation.Identity.PhysicalUserMessageId option) ->
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext ->
        Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        obj list ->
            Wanxiangshu.Context.Companion.Blogger.BloggerTerminalRequestOwnership

    val isCompletedChronicle: part: Wanxiangshu.OpenCode.SessionToolPart -> bool

    val hasExactlyOneCompletedChronicle: parts: Wanxiangshu.OpenCode.SessionToolPart array -> bool

    val completedAssistantEvidence: messages: Wanxiangshu.OpenCode.SessionMessage list -> (string * bool) list

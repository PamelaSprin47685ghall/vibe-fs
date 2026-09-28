namespace Wanxiangshu.Enforcer

module EnforcerRepair =

    val RepairInstruction: string

    val tryOpenByBlogger:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.SessionId ->
            Wanxiangshu.Context.Companion.Blogger.Runtime.OpenBloggerRequest option

    val chronicleCallCount: Wanxiangshu.Enforcer.Cycle.EnforcerCycleDecode.AssistantStep -> int
    val hasIncompleteBlogTool: Wanxiangshu.Enforcer.Cycle.EnforcerCycleDecode.AssistantStep -> bool
    val hasCompletedBlogTool: Wanxiangshu.Enforcer.Cycle.EnforcerCycleDecode.AssistantStep -> bool
    val hasAnyBlogToolPart: Wanxiangshu.Enforcer.Cycle.EnforcerCycleDecode.AssistantStep -> bool
    val hasAbortedBlogAttempt: Wanxiangshu.Enforcer.Cycle.EnforcerCycleDecode.AssistantStep -> bool
    val hasErroredBlogAttempt: Wanxiangshu.Enforcer.Cycle.EnforcerCycleDecode.AssistantStep -> bool
    val withRepairInstruction: obj list -> string -> Wanxiangshu.Foundation.Identity.ProviderRunIdentity -> obj list

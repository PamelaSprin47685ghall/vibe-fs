namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open System.Threading.Tasks
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module BloggerCoordinator =
    val observeTransformRepair:
        scope: IBloggerRuntimeHost ->
        journal: AgentJournal option ->
        request: BloggerRequestContext ->
        terminalRun: ProviderRunIdentity ->
        rawMessages: obj list ->
            Task<BloggerRepairOutcome>

    val observeIdleRepair:
        scope: IBloggerRuntimeHost ->
        journal: AgentJournal option ->
        request: BloggerRequestContext ->
        quiescence: Wanxiangshu.OpenCode.ISessionQuiescenceGate ->
        context: Wanxiangshu.Composition.Turn.ReconciledTurnContext ->
        sessionPort: Wanxiangshu.OpenCode.ISessionHostPort ->
        rootWorkspace: Wanxiangshu.OpenCode.IRootWorkspaceReader ->
        eventPort: Wanxiangshu.OpenCode.IEventObservationPort ->
            Task<BloggerRepairOutcome>

    [<RequireQualifiedAccess>]
    type DecisionEffect =
        | Started
        | StartedSquash
        | SkippedInFlight
        | OfferedParked
        | NoMaterial
        | Sealed
        | StartFailed of string
        | MaterializeFailed of string

    val materializeContinuationContext:
        scope: IBloggerRuntimeHost -> journal: AgentJournal -> ctx: BloggerRequestContext -> Task<Result<unit, string>>

    val claimFlightLease:
        scope: IBloggerRuntimeHost -> ctx: BloggerRequestContext -> Result<IBloggerFlightLease, string>

    val bindContinuationContext:
        scope: IBloggerRuntimeHost ->
        journal: AgentJournal ->
        ctx: BloggerRequestContext ->
        promptKey: PromptKey ->
            Task<Result<unit, string>>

    val abandonContinuationContext:
        scope: IBloggerRuntimeHost -> journal: AgentJournal -> ctx: BloggerRequestContext -> reason: string -> Task

    val onMainContext:
        scope: IBloggerRuntimeHost ->
        host: CompanionHost ->
        journal: AgentJournal option ->
        ctx: BloggerRequestContext ->
            Task<DecisionEffect>

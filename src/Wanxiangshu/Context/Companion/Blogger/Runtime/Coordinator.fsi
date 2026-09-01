namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open System.Threading.Tasks
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module BloggerCoordinator =
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

    val bindContinuationContext:
        scope: IBloggerRuntimeHost ->
        journal: AgentJournal ->
        ctx: BloggerRequestContext ->
        promptKey: PromptKey ->
            Task<Result<unit, string>>

    val abandonContinuationContext:
        scope: IBloggerRuntimeHost -> journal: AgentJournal -> ctx: BloggerRequestContext -> reason: string -> Task

    val reactivateAfterNewRoot: (IBloggerRuntimeHost -> SessionId -> AuthorityRootUserMessageId -> unit)

    val onMainContext:
        scope: IBloggerRuntimeHost ->
        host: CompanionHost ->
        journal: AgentJournal option ->
        ctx: BloggerRequestContext ->
            Task<DecisionEffect>

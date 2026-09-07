namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module BloggerRuntimeHost =
    [<RequireQualifiedAccess>]
    type ProducerSettlement =
        | NoOpenProducer
        | Committed
        | Abandoned
        | Cancelled

    val hasOpenProducer: journal: AgentJournal option -> mainSessionId: SessionId -> bloggerSessionId: SessionId -> bool

    val awaitOpenProducerSettlement:
        cancellation: CancellationToken -> journal: AgentJournal -> mainSessionId: SessionId -> Task<ProducerSettlement>

    val claimCurrentRequest:
        scope: IBloggerRuntimeHost -> bloggerKey: string -> context: BloggerRequestContext -> Result<unit, string>

    val requireCurrentRequest:
        scope: IBloggerRuntimeHost -> bloggerKey: string -> context: BloggerRequestContext -> unit

    val claimFlight:
        scope: IBloggerRuntimeHost ->
        bloggerKey: string ->
        context: BloggerRequestContext ->
            Result<IBloggerFlightLease, string>

    val releaseCurrentRequest:
        scope: IBloggerRuntimeHost -> bloggerKey: string -> context: BloggerRequestContext -> Result<unit, string>

    val durableSealed: journal: AgentJournal option -> mainSessionId: SessionId -> bool

    val blocksNew: journal: AgentJournal option -> mainSessionId: SessionId -> bool

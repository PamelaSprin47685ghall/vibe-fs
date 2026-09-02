namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type BloggerToolRecovery =
    | NoRecovery
    | InteractionNudgeIssued of ProviderRunIdentity
    | AabbRepairIssued of ProviderRunIdentity

type DrainPermit = private DrainPermit of AuthorityRootUserMessageId

[<RequireQualifiedAccess>]
type DrainWindow =
    | Closed
    | Open of DrainPermit

[<RequireQualifiedAccess>]
module BloggerRuntime =
    type Decision =
        | Start of BloggerRequestContext
        | Skip
        | Offer of BloggerRequestContext

    val openDrain: root: AuthorityRootUserMessageId -> DrainWindow

    val decideMaterial:
        hasOpenProducer: bool -> hasParked: bool -> hasFlight: bool -> ctx: BloggerRequestContext -> Decision

    val blocksNewRequest: durableHandleSealed: bool -> hasFlight: bool -> drainOpen: bool -> bool

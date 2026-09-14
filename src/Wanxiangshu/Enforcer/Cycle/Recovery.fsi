namespace Wanxiangshu.Enforcer.Cycle

open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module EnforcerFrameRecovery =

    type FrameLoadError =
        | MissingAssociation
        | MissingBlogSession
        | MissingFrameBlob of digest: string
        | DigestMismatch of digest: string
        | EpochMismatch

    [<RequireQualifiedAccess>]
    type CycleContextReloadRejection =
        | BlobUnreadable of reason: string
        | BlobCorrupt of reason: string
        | UnsupportedRequestKind of kind: string
        | ItemsUndecodable of reason: string
        | InvariantViolated of Wanxiangshu.Context.Companion.Blogger.BloggerRequestRejection

    val reloadRejectionLabel: rejection: CycleContextReloadRejection -> string

    val loadEffectiveFrames:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
            System.Threading.Tasks.Task<
                Result<
                    ((Wanxiangshu.Foundation.Identity.BlobDigest * string) list *
                    Wanxiangshu.Foundation.Identity.FrameEpochId),
                    FrameLoadError
                 >
             >

    val tryRebuildFromContext:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext ->
            System.Threading.Tasks.Task<obj list option>

    val rebuildFromContext: AgentJournal -> SessionId -> BloggerRequestContext -> obj list -> Task<obj list>

    val tryReloadRequestContext:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Context.Companion.Blogger.Runtime.OpenBloggerRequest ->
            System.Threading.Tasks.Task<Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext option>

    val tryReloadRequestContextDetailed:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Context.Companion.Blogger.Runtime.OpenBloggerRequest ->
            System.Threading.Tasks.Task<
                Result<
                    Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext,
                    CycleContextReloadRejection
                 >
             >

    val tryLiveCycleContext:
        Wanxiangshu.Context.Companion.Blogger.Runtime.IBloggerRuntimeHost ->
        Wanxiangshu.Foundation.Identity.SessionId ->
            Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext option

    val resolveCycleContext:
        Wanxiangshu.Context.Companion.Blogger.Runtime.IBloggerRuntimeHost ->
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.SessionId ->
            System.Threading.Tasks.Task<Wanxiangshu.Context.Companion.Blogger.BloggerRequestContext option>

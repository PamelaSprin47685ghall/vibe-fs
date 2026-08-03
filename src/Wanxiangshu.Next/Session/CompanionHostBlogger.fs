namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Tools
open Wanxiangshu.Next.Kernel.Identity
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection
open Wanxiangshu.Next.Host

module internal CompanionHostBlogger =

    type BloggerDeps =
        { Sessions: ISessionHostPort
          PrimaryId: SessionId
          Durable: ICompanionDurablePort option
          EnsureBlogger: unit -> Task<SessionId>
          Gate: obj
          Companion: Companion
          RequestKind: ProviderRequestKind ref
          SquashFrameCount: int option ref
          Journal: AgentJournal option
          EffectiveAgent: string
          RecordSquashPlan: SessionId -> ProviderRunIdentity -> unit
          StageBloggerContext: SessionId -> BloggerRequestContext -> unit }

    /// CTX-012: covered frame count = ceil(m / 2).
    let coveredFrameCount (frameCount: int) : int =
        if frameCount <= 0 then 0 else (frameCount + 1) / 2

    /// Build typed Squash context from durable frames (exact digests, fail closed if short).
    /// Freezes RequestId + ObservedPrefixEpochId at materialization (C5).
    let tryBuildSquashContext
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (observedEpoch: PrefixEpochId)
        (blog: BlogProjectionState)
        : BloggerRequestContext option =
        let m = List.length blog.Frames
        let k = coveredFrameCount m

        if k < 1 then
            None
        else
            let selected = List.truncate k blog.Frames

            if List.length selected <> k then
                None
            else
                let digests = selected |> List.map (fun f -> f.Digest)

                let requestId =
                    BloggerRequestId.create (
                        HostDigest.sha256Hex (
                            String.concat
                                "|"
                                [ SessionId.value mainSessionId
                                  SessionId.value bloggerSessionId
                                  "squash"
                                  string (FrameEpochId.value blog.FrameEpochId)
                                  string k
                                  (digests |> List.map BlobDigest.value |> String.concat ",") ]
                        )
                    )

                Some(
                    BloggerRequestContext.Squash
                        { RequestId = requestId
                          MainSessionId = mainSessionId
                          BloggerSessionId = bloggerSessionId
                          FrameEpochId = blog.FrameEpochId
                          CoveredFrameCount = k
                          FrameDigests = digests
                          ObservedPrefixEpochId = observedEpoch }
                )

    let private sendBloggerPrompt
        (deps: BloggerDeps)
        (childId: SessionId)
        (prompt: string)
        : Task<Result<PromptKey, string>> =
        task {
            match deps.Journal with
            | None -> return Error "No journal: a Blogger prompt cannot be claimed"
            | Some journal ->
                let dispatcher = PromptDispatcher.forJournal journal

                return! dispatcher.SendAgentOwnerRoot deps.Sessions childId prompt deps.EffectiveAgent None None
        }

    /// Physical send from frozen typed context (Main or Squash). No terminal wait.
    /// Transform rebuilds the provider view from durable frames + this context.
    let startFromContext (deps: BloggerDeps) (ctx: BloggerRequestContext) : Task<Result<PromptKey, string>> =
        task {
            let! childId = deps.EnsureBlogger()

            match ctx with
            | BloggerRequestContext.Main _ ->
                deps.RequestKind.Value <- ProviderRequestKind.BloggerMain
                deps.SquashFrameCount.Value <- None
                // Physical claim is the stable normal instruction only. Delta Toml lives in
                // CurrentRequest / durable materialize; transform rebuilds Working Record +
                // New Work + instruction. Sending raw Toml as the claim body fails mock
                // matching after restart when rebuild cannot run yet.
                return! sendBloggerPrompt deps childId CompanionPrompt.NormalInstruction
            | BloggerRequestContext.Squash squash ->
                deps.RequestKind.Value <- ProviderRequestKind.BloggerSquash
                deps.SquashFrameCount.Value <- Some squash.CoveredFrameCount
                // Physical claim body is the stable squash instruction only.
                // Frames arrive via rebuildFromContext on the transform (no raw transcript).
                return! sendBloggerPrompt deps childId CompanionPrompt.SquashInstruction
        }

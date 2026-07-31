namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Tools
open Wanxiangshu.Next.Kernel.Identity
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection

type CompanionHost
    (
        primaryId: SessionId,
        sessions: ISessionHostPort,
        ?durable: ICompanionDurablePort,
        ?onBloggerCreated: SessionId -> unit,
        ?restoredBloggerId: string,
        ?journal: AgentJournal
    ) =
    let companion = Companion(?durable = durable, ?sessionId = Some primaryId)
    let gate = obj ()
    let bloggerCreated = defaultArg onBloggerCreated (fun _ -> ())

    let bloggerEffectiveAgent = ManagedAgent.nameOf AgentTier.Fast Role.Blogger

    let mutable bloggerTask: Task<SessionId> option = None
    let mutable bloggerId: SessionId option = None
    let mutable bloggerFailed = false
    let bloggerNeedsReset = ref false
    let mutable restoredBloggerIdOpt = restoredBloggerId

    let ensureBlogger () =
        lock gate (fun () ->
            match bloggerTask with
            | Some _ when bloggerFailed ->
                bloggerTask <- None
                bloggerId <- None
                bloggerFailed <- false
            | _ -> ()

            match bloggerTask with
            | Some task -> task
            | None ->
                // Try restored blogger ID first (one-shot, avoids infinite retry
                // on stale sessions). If the restored session is dead, the blog
                // will fail with SendPrompt error. On the next request,
                // restoredBloggerIdOpt is None so we fall through to create a new
                // child with a reset frame.
                match restoredBloggerIdOpt, bloggerId with
                | Some id, None ->
                    let sid = SessionId.create id
                    bloggerId <- Some sid
                    bloggerFailed <- false
                    // Register the blogger role synchronously.
                    bloggerCreated sid
                    // One-shot: clear the restore opt so a failure creates new.
                    restoredBloggerIdOpt <- None
                    bloggerNeedsReset.Value <- companion.Memory.LatestB.IsSome
                    let t = Task.FromResult(sid)
                    bloggerTask <- Some t
                    t
                | _ ->
                    if companion.Memory.LatestB.IsSome then
                        bloggerNeedsReset.Value <- true

                    let task =
                        task {
                            try
                                let! created =
                                    sessions.CreateChildSession(
                                        primaryId,
                                        { Title = Some bloggerEffectiveAgent
                                          Agent = Some bloggerEffectiveAgent
                                          Directory = None }
                                    )

                                match created with
                                | Ok id ->
                                    bloggerId <- Some id
                                    bloggerFailed <- false

                                    bloggerCreated id

                                    durable
                                    |> Option.iter (fun port ->
                                        port.LinkBlogger(primaryId, id, bloggerEffectiveAgent) |> ignore)

                                    companion.RecordBloggerLinked id

                                    return id
                                | Error error -> return raise (InvalidOperationException error)
                            with ex ->
                                bloggerFailed <- true
                                bloggerId <- None
                                return raise ex
                        }

                    bloggerTask <- Some task
                    task)

    member private this.BloggerDeps: CompanionHostBlogger.BloggerDeps =
        { Sessions = sessions
          EnsureBlogger = ensureBlogger
          Gate = gate
          Companion = companion
          BloggerNeedsReset = bloggerNeedsReset
          Journal = journal
          EffectiveAgent = bloggerEffectiveAgent }

    member this.SubmitProjection(projection: ProjectionSnapshot) : CompanionOutcome =
        let deps = this.BloggerDeps
        companion.Submit(projection, (fun current chunk -> CompanionHostBlogger.blog deps current chunk))

    /// Exposes the canonical CompanionFlow calculation for adapters and tests;
    /// SubmitProjection remains the non-blocking side-effecting operation.
    member _.PreviewDelta(projection: ProjectionSnapshot) =
        CompanionProgram.runCompanionFlow
            { SessionId = SessionId.value primaryId }
            System.Threading.CancellationToken.None
            (CompanionProgram.buildDelta
                companion.Memory.Blog.Coverage.IngestCursor
                companion.Memory.Blog.Coverage.CoverableTurnCutoffExclusive
                projection)

    member _.Memory = companion.Memory

    member _.WaitInFlightAsync() = companion.WaitInFlightAsync()

    /// COMPANION-005: hand the raw history to the Y as a projection and give the
    /// Host back exactly what it passed in.
    ///
    /// This used to be where X's prefix got replaced: a watermark diff against the
    /// last projection, a `FreezeEpoch` on first sight of a B, a coverage digest
    /// re-checked on every later turn, and a synthetic B-head message spliced over
    /// the deleted prefix. It ran on every single transform, before any failure, and
    /// the epoch it consumed was written from a context-window estimate.
    ///
    /// CTX-002 puts prefix replacement behind a real failed attempt, and CTX-012
    /// behind a probe the Host actually accepted, so the decision cannot be made
    /// here — this hook has no attempt outcome to look at. Until an attempt fails,
    /// SSOT/12 says X sees raw history, which is what returning `messages` means.
    member this.TransformRaw(messages: obj list) : obj list =
        let current = Projection.decodeMessageView messages |> ProviderProjection.toSemantic
        let deps = this.BloggerDeps

        companion.Submit(current, (fun projection chunk -> CompanionHostBlogger.blog deps projection chunk))
        |> ignore

        messages

    member _.BloggerSession = lock gate (fun () -> bloggerId)

    member _.PrimarySessionId = primaryId

    /// Tear down the Blogger child and record the durable unlink on the same
    /// session stream so a restart never mistakes a dead child for a live link.
    member this.CloseBloggerAsync() : Task =
        task {
            let taskOpt = lock gate (fun () -> bloggerTask)

            match taskOpt with
            | Some task ->
                let! childId = task
                let! aborted = sessions.AbortSession(childId)

                match aborted with
                | Ok() -> ()
                | Error error -> raise (InvalidOperationException error)

                durable |> Option.iter (fun port -> port.CloseBlogger primaryId |> ignore)
                companion.RecordBloggerClosed()
            | None -> ()
        }

    interface IDisposable with
        member this.Dispose() =
            // Best-effort teardown: drop the blogger child and record the
            // durable unlink on the same session stream.
            if bloggerTask.IsSome then
                this.CloseBloggerAsync() |> ignore

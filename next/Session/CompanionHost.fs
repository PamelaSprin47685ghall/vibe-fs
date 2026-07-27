namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Tools
open Wanxiangshu.Next.Kernel.Identity
open Fable.Core.JsInterop

type CompanionHost
    (
        primaryId: SessionId,
        sessions: ISessionHostPort,
        ?durable: ICompanionDurablePort,
        ?onBloggerCreated: SessionId -> unit,
        ?bloggerModel: Result<OpencodeModel, string>,
        ?outputBoundary: IEventOutputBoundaryPort,
        ?restoredBloggerId: string
    ) =
    let companion = Companion(?durable = durable, ?sessionId = Some primaryId)
    let gate = obj ()
    let bloggerCreated = defaultArg onBloggerCreated (fun _ -> ())

    let configuredBloggerModel =
        defaultArg bloggerModel (Error "WANXIANGSHU_BLOGGER_MODEL is not configured")

    let outputWatermark (sessionId: SessionId) =
        match outputBoundary with
        | Some boundary -> boundary.GetSessionOutputWatermark sessionId
        | None -> sessions.GetSessionOutput sessionId |> List.length

    let assistantOutput (childId: SessionId) (watermark: int) =
        let output =
            match outputBoundary with
            | Some boundary -> boundary.GetSessionOutputSince(childId, watermark)
            | None ->
                sessions.GetSessionOutput childId
                |> List.skip (min watermark (sessions.GetSessionOutput childId).Length)

        output
        |> List.filter (fun line -> not (line.StartsWith("Prompt: ")) && not (line.StartsWith("ChildPrompt: ")))
        |> String.concat "\n"

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
                    // One-shot: clear the restore opt so a failure creates new
                    restoredBloggerIdOpt <- None
                    // No reset frame needed — session is ongoing
                    bloggerNeedsReset.Value <- false
                    let t = Task.FromResult(sid)
                    bloggerTask <- Some t
                    t
                | _ ->
                    // A restored companion already holds B; the freshly created
                    // blogger child must be re-anchored on the full context so it
                    // does not delude itself with a lost baseline.
                    if companion.Memory.LatestB.IsSome then
                        bloggerNeedsReset.Value <- true

                    let task =
                        task {
                            try
                                let! created =
                                    sessions.CreateChildSession(
                                        primaryId,
                                        { Title = Some "blogger"
                                          Agent = Some "blogger"
                                          Directory = None }
                                    )

                                match created with
                                | Ok id ->
                                    bloggerId <- Some id
                                    bloggerFailed <- false

                                    bloggerCreated id

                                    durable
                                    |> Option.iter (fun port ->
                                        port.AppendLink(
                                            primaryId,
                                            ChildId.create (SessionId.value id),
                                            "blogger",
                                            Some "blogger"
                                        )
                                        |> ignore)

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
          Model = configuredBloggerModel
          EnsureBlogger = ensureBlogger
          Gate = gate
          BloggerNeedsReset = bloggerNeedsReset
          Companion = companion
          OutputWatermark = outputWatermark
          AssistantOutput = assistantOutput }

    member this.SubmitProjection(projection: ProjectionSnapshot) : CompanionOutcome =
        let deps = this.BloggerDeps
        companion.Submit(projection, (fun delta -> CompanionHostBlogger.blog deps projection delta))

    member _.EnablePrefixReplacement() : bool = companion.TryEnableReplacement()

    /// Real Y self-rebase: ask the Blogger child to condense the FULL current B
    /// into B' and durably persist (CompanionAdvanced with the EXISTING baseline,
    /// so the projection baseline is NOT advanced — only B is replaced). The
    /// Blogger sees only the old B when condensing and never processes the
    /// P0→P1 delta, so advancing the baseline here would lose those messages.
    /// Fire-and-forget; the underlying async rebase returns false (SkippedBusy)
    /// when the Blogger is busy, and leaves B unchanged on failure.
    member this.SelfRebase() : CompanionOutcome =
        let before = companion.Memory

        // Y self-rebase is independent of X prefix replacement: trigger once B
        // exists. The 0.8 budget gate lives in CompanionTransform against the
        // blogger child's own (usually smaller) model limit.
        if before.LatestB.IsNone then
            Submitted
        else
            let currentB = before.LatestB.Value
            let deps = this.BloggerDeps

            let started =
                companion.TrySelfRebase(fun () -> CompanionHostBlogger.selfRebaseBlog deps currentB)

            if started then Submitted else SkippedBusy

    member _.Memory = companion.Memory

    member _.WaitInFlightAsync() = companion.WaitInFlightAsync()

    member this.TransformRaw(messages: obj list) : obj list =
        let current = CompanionDelta.jsonOfMessages Projection.canonicalJson messages
        let before = companion.Memory

        let watermark =
            match before.LastSuccessfulProjection with
            | Some previous ->
                CompanionDelta.prefixLength
                    Projection.messageId
                    Projection.sameCanonicalMessage
                    previous
                    current
                    (List.length messages)
            | None -> 0

        let deps = this.BloggerDeps

        companion.Submit(current, (fun delta -> CompanionHostBlogger.blog deps current delta))
        |> ignore

        match before.ReplacementActive, before.ActivePrefixEpoch, before.LastSuccessfulProjection with
        | true, Some epoch, Some _ when watermark > 0 ->
            // X-side prefix replacement: use the frozen B from ActivePrefixEpoch.
            // This is the key cache fix — FrozenB remains stable across Blogger
            // updates, unlike the old CurrentB which changed every blog and
            // corrupted the provider KV-cache prefix.
            let synthetic =
                createObj
                    [ "info", box (createObj [ "id", box "companion-b-head"; "role", box "user" ])
                      "parts", box [| createObj [ "type", box "text"; "text", box epoch.FrozenB ] |] ]

            synthetic :: (messages |> List.skip watermark)
        | true, None, Some _ when watermark > 0 ->
            // Replacement active but no epoch yet: freeze LatestB as the first epoch.
            // This is an implicit cold-cache boundary (first and only unavoidable one
            // for this prefix-replacement session).
            match before.LatestB with
            | Some b ->
                companion.FreezeEpoch() |> ignore
                let epoch = companion.Memory.ActivePrefixEpoch
                match epoch with
                | Some epoch ->
                    let synthetic =
                        createObj
                            [ "info", box (createObj [ "id", box "companion-b-head"; "role", box "user" ])
                              "parts", box [| createObj [ "type", box "text"; "text", box epoch.FrozenB ] |] ]

                    synthetic :: (messages |> List.skip watermark)
                | None -> messages
            | None -> messages
        | _ -> messages

    member _.ReplacePrefix(messages: HostMessage list, watermarkIndex: int) =
        Companion.compressPrefix messages companion.Memory.LatestB watermarkIndex

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
                do! sessions.AbortChildren(primaryId)

                durable
                |> Option.iter (fun port ->
                    port.AppendUnlink(primaryId, ChildId.create (SessionId.value childId)) |> ignore)
            | None -> ()
        }

    interface IDisposable with
        member this.Dispose() =
            // Best-effort teardown: drop the blogger child and record the
            // durable unlink on the same session stream.
            if bloggerTask.IsSome then
                this.CloseBloggerAsync() |> ignore

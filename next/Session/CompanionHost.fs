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

type AgentJournalCompanionPort(journal: AgentJournal) =
    let append (sessionId: SessionId) (fact: AgentFact) =
        match AgentJournal.appendAgent (StreamId.Session sessionId) None fact journal with
        | Ok _ -> Ok()
        | Error failure -> Error(sprintf "%A" failure.Failure)

    interface ICompanionDurablePort with
        member _.Load(sessionId: SessionId) : CompanionMemory option =
            let projection = AgentJournal.snapshot journal

            projection.AgentProjections.Sessions
            |> Map.tryFind sessionId
            |> Option.bind (fun session ->
                session.Companion
                |> Option.map (fun companion ->
                    { LastSuccessfulProjection = companion.LastSuccessfulProjection
                      CurrentB = companion.CurrentB
                      BloggerBusy = false
                      ReplacementActive = companion.ReplacementActive }))

        member _.AppendSuccessful(sessionId, projection, content) =
            append
                sessionId
                (AgentFact.CompanionAdvanced
                    {| SessionId = sessionId
                       Projection = projection
                       Content = content |})

        member _.EnableReplacement(sessionId) =
            append
                sessionId
                (AgentFact.CompanionReplacementActiveSet
                    {| SessionId = sessionId
                       Active = true |})

        member _.AppendLink(sessionId, childId, targetAgent, role) =
            append
                sessionId
                (AgentFact.AgentLinked
                    {| ParentId = sessionId
                       ChildId = childId
                       TargetAgent = targetAgent
                       Role = role |})

        member _.AppendUnlink(sessionId, childId) =
            append
                sessionId
                (AgentFact.AgentUnlinked
                    {| ParentId = sessionId
                       ChildId = childId |})

type CompanionHost
    (
        primaryId: SessionId,
        sessions: ISessionHostPort,
        ?durable: ICompanionDurablePort,
        ?onBloggerCreated: SessionId -> unit,
        ?bloggerModel: Result<OpencodeModel, string>
    ) =
    let companion = Companion(?durable = durable, ?sessionId = Some primaryId)
    let gate = obj ()
    let bloggerCreated = defaultArg onBloggerCreated (fun _ -> ())

    let configuredBloggerModel =
        defaultArg bloggerModel (Error "WANXIANGSHU_BLOGGER_MODEL is not configured")

    let mutable bloggerTask: Task<SessionId> option = None
    let bloggerNeedsReset = ref false

    let ensureBlogger () =
        lock gate (fun () ->
            match bloggerTask with
            | Some task -> task
            | None ->
                // A restored companion already holds B; the freshly created
                // blogger child must be re-anchored on the full context so it
                // does not delude itself with a lost baseline.
                // ponytail: re-association to the SAME pre-restart child session
                // is gated on child-session liveness, which this runtime does not
                // preserve across restart; the durable AgentLinked fact is now
                // persisted and the reset-frame below covers the not-recoverable
                // case (new child re-anchored on full B + projection).
                if companion.Memory.CurrentB.IsSome then
                    bloggerNeedsReset.Value <- true

                let task =
                    task {
                        let! created =
                            sessions.CreateChildSession(
                                primaryId,
                                { Title = Some "blogger"
                                  Agent = Some "blogger"
                                  Directory = None }
                            )

                        match created with
                        | Ok id ->
                            // Register the blogger role synchronously so the
                            // companion gate can never recurse into blogger
                            // sessions, not even before its first event.
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
          AssistantOutput =
            (fun childId watermark ->
                CompanionDelta.assistantOutput (fun id -> sessions.GetSessionOutput id) childId watermark) }

    member this.SubmitProjection(projection: ProjectionSnapshot) : CompanionOutcome =
        let deps = this.BloggerDeps
        companion.Submit(projection, (fun delta -> CompanionHostBlogger.blog deps delta))

    member _.EnablePrefixReplacement() : bool = companion.TryEnableReplacement()

    /// Real Y self-rebase: ask the Blogger child to condense the FULL current B
    /// into B' and durably persist (CompanionAdvanced). Fire-and-forget; the
    /// underlying async rebase returns false (SkippedBusy) when the Blogger is
    /// busy, and leaves B unchanged on failure.
    member this.SelfRebase(currentProjection: ProjectionSnapshot) : CompanionOutcome =
        let before = companion.Memory

        if not before.ReplacementActive || before.CurrentB.IsNone then
            Submitted
        else
            let currentB = before.CurrentB.Value
            let deps = this.BloggerDeps

            let started =
                companion.TryRebase(fun () ->
                    async {
                        let! bPrime = CompanionHostBlogger.selfRebaseBlog deps currentB |> Async.AwaitTask
                        return (bPrime, currentProjection)
                    })

            if started then Submitted else SkippedBusy

    member _.TryRebase(newB: string, newBaseline: string) : bool = companion.TryRebase(newB, newBaseline)

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

        companion.Submit(current, (fun delta -> CompanionHostBlogger.blog deps delta))
        |> ignore

        match before.ReplacementActive, before.CurrentB, before.LastSuccessfulProjection with
        | true, Some b, Some _ when watermark > 0 ->
            // MessageV2 WithParts shape (see CompanionTransform): the B head
            // travels as a user-role synthetic with a stable id so prefix
            // comparison keeps recognising it across projections.
            let synthetic =
                createObj
                    [ "info", box (createObj [ "id", box "companion-b-head"; "role", box "user" ])
                      "parts", box [| createObj [ "type", box "text"; "text", box b ] |] ]

            synthetic :: (messages |> List.skip watermark)
        | _ -> messages

    member _.ReplacePrefix(messages: HostMessage list, watermarkIndex: int) =
        Companion.compressPrefix messages companion.Memory.CurrentB watermarkIndex

    member _.BloggerSession =
        lock gate (fun () -> bloggerTask |> Option.map (fun task -> task.Result))

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

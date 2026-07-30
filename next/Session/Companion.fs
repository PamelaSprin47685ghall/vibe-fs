namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Journal

/// Companion state wrapper with a single mutable in-flight Task gate.
type Companion(?initialMemory: CompanionMemory, ?durable: ICompanionDurablePort, ?sessionId: SessionId) =
    let lockObj = obj ()

    let restoredMemory =
        match initialMemory with
        | Some memory -> Some memory
        | None ->
            match durable, sessionId with
            | Some port, Some sid -> port.Load sid
            | _ -> None

    let mutable lastSuccessfulProjection: ProjectionSnapshot option =
        restoredMemory |> Option.bind (fun m -> m.LastSuccessfulProjection)

    let mutable latestB: BlogText option =
        restoredMemory |> Option.bind (fun m -> m.LatestB)

    /// COMPANION-002: the companion Blogger Session Y for this X.
    ///
    /// A runtime mirror of the durable link, not a second writer: `CompanionHost`
    /// owns the create/abort decision and writes the fact through
    /// `ICompanionDurablePort`, then tells this cache. Restored on construction so a
    /// restart reuses the same Y instead of creating a second one.
    let mutable bloggerSessionId: SessionId option =
        restoredMemory |> Option.bind (fun m -> m.BloggerSessionId)

    // Fable Tasks do not expose IsCompleted; this bit belongs to the
    // single in-flight completion cell and is not a second blogger state.
    let mutable inFlightTask: Task<unit> option = None
    let mutable inFlightCompleted = true

    let persistSuccessful (projection: ProjectionSnapshot) (content: BlogText) =
        match durable, sessionId with
        | Some port, Some sid ->
            match port.AppendSuccessful(sid, projection, content) with
            | Ok() -> ()
            | Error error -> raise (InvalidOperationException error)
        | _ -> ()

    let startAsTask (work: Async<unit>) : Task<unit> =
        let completion = TaskCompletionSource<unit>()
        inFlightCompleted <- false

        Async.StartImmediate(
            async {
                try
                    do! work
                finally
                    inFlightCompleted <- true
                    completion.SetResult(())
            }
        )

        completion.Task

    let isBusyUnlocked () =
        match inFlightTask with
        | Some _ when not inFlightCompleted -> true
        | _ -> false

    member _.Memory: CompanionMemory =
        lock lockObj (fun () ->
            { LastSuccessfulProjection = lastSuccessfulProjection
              LatestB = latestB
              BloggerSessionId = bloggerSessionId })

    /// Mirror a durable Blogger link that `CompanionHost` already recorded.
    member _.RecordBloggerLinked(bloggerId: SessionId) : unit =
        lock lockObj (fun () -> bloggerSessionId <- Some bloggerId)

    /// Mirror a durable Blogger unlink. `None` again, so the next transform creates
    /// a fresh Y rather than prompting an aborted session forever.
    member _.RecordBloggerClosed() : unit =
        lock lockObj (fun () -> bloggerSessionId <- None)

    member this.Snapshot: CompanionMemory = this.Memory
    member this.GetMemory() : CompanionMemory = this.Memory
    member _.IsBusy: bool = lock lockObj isBusyUnlocked
    member _.InFlightTask: Task<unit> option = lock lockObj (fun () -> inFlightTask)

    member this.WaitInFlightAsync() : Task =
        let tOpt = lock lockObj (fun () -> inFlightTask)

        match tOpt with
        | Some t -> t :> Task
        | None -> Task.FromResult(()) :> Task

    member this.Submit
        (currentProjection: ProjectionSnapshot, blogFn: ProjectionSnapshot -> Async<BlogText>)
        : CompanionOutcome =
        lock lockObj (fun () ->
            if isBusyUnlocked () then
                SkippedBusy
            else
                match Companion.jsonDelta lastSuccessfulProjection currentProjection with
                | None -> Submitted
                | Some delta ->
                    let t =
                        async {
                            try
                                let! content = blogFn delta

                                let nextB =
                                    match latestB with
                                    | None -> content
                                    | Some old -> old + "\n\n" + content

                                persistSuccessful currentProjection nextB

                                lock lockObj (fun () ->
                                    latestB <- Some nextB
                                    lastSuccessfulProjection <- Some currentProjection)
                            with _ ->
                                ()
                        }
                        |> startAsTask

                    inFlightTask <- Some t
                    Submitted)

    member this.Submit
        (currentProjection: ProjectionSnapshot, blogFn: ProjectionSnapshot -> Task<BlogText>)
        : CompanionOutcome =
        this.Submit(currentProjection, (fun (delta: ProjectionSnapshot) -> blogFn delta |> Async.AwaitTask))

    member this.Submit(currentProjection: ProjectionSnapshot, blogFn: unit -> Async<BlogText>) : CompanionOutcome =
        this.Submit(currentProjection, (fun (_: ProjectionSnapshot) -> blogFn ()))

    member this.Submit(currentProjection: ProjectionSnapshot, blogFn: unit -> Task<BlogText>) : CompanionOutcome =
        this.Submit(currentProjection, (fun (_: ProjectionSnapshot) -> blogFn () |> Async.AwaitTask))

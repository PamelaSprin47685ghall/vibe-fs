namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Tools

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

    let mutable activePrefixEpoch: ActivePrefixEpoch option =
        restoredMemory |> Option.bind (fun m -> m.ActivePrefixEpoch)

    let mutable replacementActive: bool =
        restoredMemory
        |> Option.map (fun m -> m.ReplacementActive)
        |> Option.defaultValue false

    let mutable inFlightTask: Task<unit> option = None
    let mutable busy = false

    let persistSuccessful (projection: ProjectionSnapshot) (content: BlogText) =
        match durable, sessionId with
        | Some port, Some sid ->
            match port.AppendSuccessful(sid, projection, content) with
            | Ok() -> ()
            | Error error -> raise (InvalidOperationException error)
        | _ -> ()

    let persistEpochSwitched (epoch: ActivePrefixEpoch) =
        match durable, sessionId with
        | Some port, Some sid ->
            match port.AppendEpochSwitched(sid, epoch) with
            | Ok() -> ()
            | Error error -> raise (InvalidOperationException error)
        | _ -> ()

    let startAsTask (work: Async<unit>) : Task<unit> =
        let completion = TaskCompletionSource<unit>()

        Async.StartImmediate(
            async {
                try
                    do! work
                finally
                    busy <- false
                    completion.SetResult(())
            }
        )

        completion.Task

    let isBusyUnlocked () = busy

    /// Returns current CompanionMemory state.
    member _.Memory: CompanionMemory =
        lock lockObj (fun () ->
            { LastSuccessfulProjection = lastSuccessfulProjection
              LatestB = latestB
              ActivePrefixEpoch = activePrefixEpoch
              BloggerBusy = isBusyUnlocked ()
              ReplacementActive = replacementActive })

    /// Alias for Memory to satisfy Snapshot access.
    member this.Snapshot: CompanionMemory = this.Memory

    member this.GetMemory() : CompanionMemory = this.Memory

    /// Returns true if an async blog operation is currently in-flight.
    member _.IsBusy: bool = lock lockObj isBusyUnlocked

    /// Current in-flight task, if any.
    member _.InFlightTask: Task<unit> option = lock lockObj (fun () -> inFlightTask)

    /// Awaits the current in-flight task if running.
    member this.WaitInFlightAsync() : Task =
        let tOpt = lock lockObj (fun () -> inFlightTask)
        match tOpt with
        | Some t -> t :> Task
        | None -> Task.FromResult(()) :> Task

    member _.ReplacementActive
        with get () = lock lockObj (fun () -> replacementActive)
        and set value = lock lockObj (fun () -> replacementActive <- value)

    member this.TryEnableReplacement() : bool =
        lock lockObj (fun () ->
            if replacementActive then true
            else
                match durable, sessionId with
                | Some port, Some sid ->
                    match port.EnableReplacement sid with
                    | Ok() -> replacementActive <- true; true
                    | Error _ -> false
                | _ -> replacementActive <- true; true)

    member this.TryRebase(rebaseFn: unit -> Async<BlogText * ProjectionSnapshot>) : bool =
        lock lockObj (fun () ->
            if isBusyUnlocked () then false
            else
                busy <- true
                let t =
                    async {
                        try
                            let! (b, proj) = rebaseFn ()
                            persistSuccessful proj b
                            // LatestB only. ActivePrefixEpoch.FrozenB stays frozen until
                            // an explicit SwitchEpoch cold boundary.
                            lock lockObj (fun () ->
                                latestB <- Some b
                                lastSuccessfulProjection <- Some proj)
                        with _ -> ()
                    } |> startAsTask
                inFlightTask <- Some t
                true)

    member this.TryRebase(rebaseFn: unit -> Task<BlogText * ProjectionSnapshot>) : bool =
        this.TryRebase(fun () -> rebaseFn () |> Async.AwaitTask)

    /// Y self-rebase: persist ONLY B', keep projection baseline UNCHANGED.
    /// Never rewrites ActivePrefixEpoch.FrozenB — that would bust X's provider
    /// prefix cache. Epoch switches are explicit cold boundaries only.
    member this.TrySelfRebase(rebaseFn: unit -> Async<BlogText>) : bool =
        lock lockObj (fun () ->
            if isBusyUnlocked () then false
            else
                busy <- true
                let t =
                    async {
                        try
                            let! b = rebaseFn ()
                            lastSuccessfulProjection |> Option.iter (fun proj -> persistSuccessful proj b)
                            lock lockObj (fun () -> latestB <- Some b)
                        with _ -> ()
                    } |> startAsTask
                inFlightTask <- Some t
                true)

    member this.TrySelfRebase(rebaseFn: unit -> Task<BlogText>) : bool =
        this.TrySelfRebase(fun () -> rebaseFn () |> Async.AwaitTask)

    /// Freeze LatestB as the first ActivePrefixEpoch. No-op if epoch already exists or no LatestB.
    member this.FreezeEpoch() : bool =
        lock lockObj (fun () ->
            match activePrefixEpoch, latestB with
            | Some _, _ | _, None -> false
            | None, Some b ->
                let sessionStr = sessionId |> Option.map SessionId.value |> Option.defaultValue ""
                let epoch: ActivePrefixEpoch =
                    { EpochId = sprintf "%s|0|%d" sessionStr (String.length b)
                      FrozenB = b; CutoffMessageIndex = 0; CoveredPrefixDigest = string (String.length b) }
                persistEpochSwitched epoch
                activePrefixEpoch <- Some epoch; true)

    /// Switch to a new epoch from current LatestB. Cold-cache boundary.
    member this.SwitchEpoch(cutoffMessageIndex: int) : bool =
        lock lockObj (fun () ->
            match latestB with
            | None -> false
            | Some b ->
                let sessionStr = sessionId |> Option.map SessionId.value |> Option.defaultValue ""
                let epoch: ActivePrefixEpoch =
                    { EpochId = sprintf "%s|%d|%d" sessionStr cutoffMessageIndex (String.length b)
                      FrozenB = b; CutoffMessageIndex = cutoffMessageIndex; CoveredPrefixDigest = string (String.length b) }
                persistEpochSwitched epoch
                activePrefixEpoch <- Some epoch; true)

    /// Submit: blog delta -> update latestB + projection. Never modifies ActivePrefixEpoch.
    member this.Submit (currentProjection: ProjectionSnapshot, blogFn: ProjectionSnapshot -> Async<BlogText>) : CompanionOutcome =
        lock lockObj (fun () ->
            if isBusyUnlocked () then SkippedBusy
            else
                match Companion.jsonDelta lastSuccessfulProjection currentProjection with
                | None -> Submitted
                | Some delta ->
                    busy <- true
                    let t =
                        async {
                            try
                                let! content = blogFn delta
                                let nextB = match latestB with None -> content | Some old -> old + "\n\n" + content
                                persistSuccessful currentProjection nextB
                                lock lockObj (fun () -> latestB <- Some nextB; lastSuccessfulProjection <- Some currentProjection)
                            with _ -> ()
                        } |> startAsTask
                    inFlightTask <- Some t
                    Submitted)

    member this.Submit (currentProjection: ProjectionSnapshot, blogFn: ProjectionSnapshot -> Task<BlogText>) : CompanionOutcome =
        this.Submit(currentProjection, (fun (delta: ProjectionSnapshot) -> blogFn delta |> Async.AwaitTask))

    member this.Submit(currentProjection: ProjectionSnapshot, blogFn: unit -> Async<BlogText>) : CompanionOutcome =
        this.Submit(currentProjection, (fun (_: ProjectionSnapshot) -> blogFn ()))

    member this.Submit(currentProjection: ProjectionSnapshot, blogFn: unit -> Task<BlogText>) : CompanionOutcome =
        this.Submit(currentProjection, (fun (_: ProjectionSnapshot) -> blogFn () |> Async.AwaitTask))

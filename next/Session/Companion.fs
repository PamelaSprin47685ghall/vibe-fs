namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Tools

type ProjectionSnapshot = string
type BlogText = string

type CompanionOutcome =
    | Submitted
    | SkippedBusy

type CompanionMemory =
    { LastSuccessfulProjection: ProjectionSnapshot option
      CurrentB: BlogText option
      BloggerBusy: bool
      ReplacementActive: bool }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Companion =

    /// Pure jsonDelta: returns None when previous and current are equal canonical JSON;
    /// otherwise returns a JSON object string containing top-level fields changed in current.
    /// Absent previous returns current.
    let jsonDelta (previous: ProjectionSnapshot option) (current: ProjectionSnapshot) : ProjectionSnapshot option =
        match previous with
        | None -> Some current
        | Some prev ->
            if prev = current then
                None
            else
                try
                    let prevObj = Fable.Core.JS.JSON.parse prev
                    let currObj = Fable.Core.JS.JSON.parse current

                    let isObject (value: obj) =
                        Fable.Core.JsInterop.emitJsExpr value "typeof $0 === 'object' && $0 !== null"

                    let stringify (value: obj) : string =
                        Fable.Core.JsInterop.emitJsExpr value "JSON.stringify($0)"

                    if isObject prevObj && isObject currObj then
                        let currKeys: string array =
                            Fable.Core.JsInterop.emitJsExpr currObj "Object.keys($0)"

                        let diffObj: obj = Fable.Core.JsInterop.createEmpty
                        let mutable changed = false

                        for key in currKeys do
                            let valCurr: obj = Fable.Core.JsInterop.emitJsExpr (currObj, key) "$0[$1]"
                            let valPrev: obj = Fable.Core.JsInterop.emitJsExpr (prevObj, key) "$0[$1]"

                            if stringify valCurr <> stringify valPrev then
                                changed <- true
                                Fable.Core.JsInterop.emitJsExpr (diffObj, key, valCurr) "$0[$1] = $2" |> ignore

                        if changed then Some(stringify diffObj) else None
                    else
                        Some current
                with _ ->
                    Some current

    /// Pure compressPrefix: delegates to MessageTransform.replacePrefix using currentB and explicit watermark index.
    let compressPrefix
        (messages: HostMessage list)
        (currentB: BlogText option)
        (watermarkIndex: int)
        : HostMessage list =
        match currentB with
        | None -> messages
        | Some b -> MessageTransform.replacePrefix messages b (Index watermarkIndex)

    let compressPrefixText (messages: HostMessage list) (currentB: BlogText) (watermarkIndex: int) : HostMessage list =
        compressPrefix messages (Some currentB) watermarkIndex

/// Companion state wrapper with a single mutable in-flight Task gate.
type Companion(?initialMemory: CompanionMemory) =
    let lockObj = obj ()

    let mutable lastSuccessfulProjection: ProjectionSnapshot option =
        initialMemory |> Option.bind (fun m -> m.LastSuccessfulProjection)

    let mutable currentB: BlogText option =
        initialMemory |> Option.bind (fun m -> m.CurrentB)

    let mutable replacementActive: bool =
        initialMemory
        |> Option.map (fun m -> m.ReplacementActive)
        |> Option.defaultValue false

    let mutable inFlightTask: Task<unit> option = None
    let mutable busy = false

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
              CurrentB = currentB
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

    /// Blogger context reset (self-rebase): updates B and baseline if idle, returns true; returns false if busy.
    member this.TryRebase(newB: BlogText option, newBaseline: ProjectionSnapshot option) : bool =
        lock lockObj (fun () ->
            if isBusyUnlocked () then
                false
            else
                currentB <- newB
                lastSuccessfulProjection <- newBaseline
                true)

    member this.TryRebase(newB: BlogText, newBaseline: ProjectionSnapshot) : bool =
        this.TryRebase(Some newB, Some newBaseline)

    member this.TryRebase(rebaseFn: unit -> Async<BlogText * ProjectionSnapshot>) : bool =
        lock lockObj (fun () ->
            if isBusyUnlocked () then
                false
            else
                busy <- true

                let t =
                    async {
                        try
                            let! (b, proj) = rebaseFn ()

                            lock lockObj (fun () ->
                                currentB <- Some b
                                lastSuccessfulProjection <- Some proj)
                        with _ ->
                            ()
                    }
                    |> startAsTask

                inFlightTask <- Some t
                true)

    member this.TryRebase(rebaseFn: unit -> Task<BlogText * ProjectionSnapshot>) : bool =
        this.TryRebase(fun () -> rebaseFn () |> Async.AwaitTask)

    /// Submit: starts async blog function only when idle, returns SkippedBusy when busy, never queues.
    /// Completion atomically updates currentB and lastSuccessfulProjection on success; failure leaves both unchanged.
    member this.Submit
        (currentProjection: ProjectionSnapshot, blogFn: ProjectionSnapshot -> Async<BlogText>)
        : CompanionOutcome =
        lock lockObj (fun () ->
            if isBusyUnlocked () then
                SkippedBusy
            else
                let deltaOpt = Companion.jsonDelta lastSuccessfulProjection currentProjection

                match deltaOpt with
                | None -> Submitted
                | Some delta ->
                    busy <- true

                    let t =
                        async {
                            try
                                let! content = blogFn delta

                                lock lockObj (fun () ->
                                    currentB <- Some content
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

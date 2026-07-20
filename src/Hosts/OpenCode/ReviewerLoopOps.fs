module Wanxiangshu.Hosts.Opencode.ReviewerLoopOps

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.ReviewPrompts
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewSession.StateMachine
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Hosts.Opencode.SessionIo
open Wanxiangshu.Hosts.Opencode.SubagentSpawnTransport
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.SubagentSpawn
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Hosts.Opencode.SubagentSpawnTransport

let private maxNudges = 3

let textParts (parts: string list) : obj array =
    parts
    |> List.map (fun text -> box {| ``type`` = "text"; text = text |})
    |> Array.ofList

let runRound
    (client: obj)
    (childID: string)
    (childSignal: obj)
    (verdict: ReviewResult option ref)
    (gate: PromptAbortGate)
    (parts: string list)
    : JS.Promise<RoundOutcome> =
    promise {
        let promptBody =
            box
                {| path = box {| id = childID |}
                   body =
                    box
                        {| agent = "reviewer"
                           parts = textParts parts
                           tools = box (createObj [ "return_reviewer", box true ]) |} |}

        try
            bumpPromptAbortEpoch gate |> ignore
            do! promptWithAbortOwned client promptBody childSignal (Some gate)

            match verdict.Value with
            | Some v -> return Resolved v
            | None -> return NoResult
        with err ->
            match verdict.Value, translateJsError err with
            | Some v, (MessageAborted | ClientCancellation _) -> return Resolved v
            | _, (MessageAborted | ClientCancellation _) -> return! Promise.reject err
            | _ -> return PromptFailed
    }

let rec loop
    (client: obj)
    (childID: string)
    (childSignal: obj)
    (verdict: ReviewResult option ref)
    (reviewStore: ReviewStore)
    (gate: PromptAbortGate)
    (initialParts: string list)
    (nudgeCount: int)
    : JS.Promise<ReviewResult> =
    promise {
        reviewStore.setPendingReview (childID, (fun r -> verdict.Value <- Some r))

        let parts = promptParts nudgeCount initialParts reviewerNudgePrompt
        let! outcome = runRound client childID childSignal verdict gate parts

        match decideAfterRound nudgeCount outcome maxNudges with
        | Finish result -> return result
        | Nudge next -> return! loop client childID childSignal verdict reviewStore gate initialParts next
    }

let performCleanup
    (cleanedUp: bool ref)
    (registry: ChildAgentRegistry)
    (client: obj)
    (reviewStore: ReviewStore)
    (directory: string)
    (childID: string)
    (abortSignal: obj)
    (parentAbortHandler: (unit -> unit) option ref)
    (gate: PromptAbortGate)
    (childAbort: AbortController)
    : JS.Promise<unit> =
    promise {
        if not cleanedUp.Value then
            cleanedUp.Value <- true
            closePromptAbortGate gate

            match parentAbortHandler.Value with
            | Some h when not (Dyn.isNullish abortSignal) ->
                try
                    abortSignal?removeEventListener ("abort", h) |> ignore
                with _ ->
                    ()
            | _ -> ()

            try
                childAbort.abort ()
            with _ ->
                ()

            try
                reviewStore.unlockReview childID
            with _ ->
                ()

            try
                reviewStore.CleanupSession childID
            with _ ->
                ()

            try
                do! Wanxiangshu.Hosts.Opencode.SubagentIoCleanup.abortAndUnregister registry client directory childID
            with _ ->
                ()
    }

let runLoopWithCleanup
    (registry: ChildAgentRegistry)
    (client: obj)
    (reviewStore: ReviewStore)
    (directory: string)
    (childID: string)
    (initialParts: string list)
    (abortSignal: obj)
    : JS.Promise<ReviewResult> =
    promise {
        let verdict = ref None
        let childAbort = AbortController()
        let gate = createPromptAbortGate ()

        reviewStore.setAbortSuppressor (childID, (fun () -> childAbort.abort ()))
        reviewStore.setPendingReview (childID, (fun r -> verdict.Value <- Some r))
        reviewStore.tryLockReview childID |> ignore

        let parentAbortHandler = ref None
        let cleanedUp = ref false

        if not (Dyn.isNullish abortSignal) then
            if Dyn.truthy (Dyn.get abortSignal "aborted") then
                childAbort.abort ()
            else
                let handler = fun () -> childAbort.abort ()
                parentAbortHandler.Value <- Some handler
                abortSignal?addEventListener ("abort", handler) |> ignore

        let mutable loopError = None
        let mutable result = Terminated

        try
            let! r = loop client childID childAbort.signal verdict reviewStore gate initialParts 0
            result <- r
        with err ->
            loopError <- Some err

        do!
            performCleanup
                cleanedUp
                registry
                client
                reviewStore
                directory
                childID
                abortSignal
                parentAbortHandler
                gate
                childAbort

        match loopError with
        | Some err -> return! Promise.reject err
        | None -> return result
    }

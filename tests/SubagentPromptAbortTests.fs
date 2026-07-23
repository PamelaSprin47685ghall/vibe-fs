module Wanxiangshu.Tests.SubagentPromptAbortTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush

let createMockAbortSignal () =
    let handlers = ResizeArray<unit -> unit>()

    let signal =
        createObj
            [ "aborted", box false
              "addEventListener",
              box (
                  System.Action<string, unit -> unit>(fun event handler ->
                      if event = "abort" then
                          handlers.Add(handler) |> ignore)
              )
              "removeEventListener",
              box (
                  System.Action<string, unit -> unit>(fun event handler ->
                      if event = "abort" then
                          handlers.Remove(handler) |> ignore)
              ) ]

    let trigger () =
        signal?aborted <- true

        for h in handlers do
            h ()

    signal, trigger

let promptWithAbort_abortsHostWhenSignalWins () =
    promise {
        let mutable abortCalled = false
        let mutable abortPath = ""

        let session =
            createObj
                [ "prompt", box (fun (_: obj) -> Promise.create (fun _ _ -> ()))
                  "abort",
                  box (fun (arg: obj) ->
                      abortCalled <- true
                      abortPath <- Wanxiangshu.Runtime.Dyn.str (Wanxiangshu.Runtime.Dyn.get arg "path") "id"
                      Promise.lift ()) ]

        let client = createObj [ "session", box session ]
        let signal, trigger = createMockAbortSignal ()
        let args = box {| path = box {| id = "child-abort-host" |} |}

        let task =
            Wanxiangshu.Hosts.Opencode.SubagentSpawnTransport.promptWithAbort client args signal

        let! () = yieldMicrotask ()
        trigger ()

        let mutable threw = false

        try
            let! _ = task
            ()
        with err ->
            let domainErr = Wanxiangshu.Runtime.ErrorClassify.translateJsError err

            match domainErr with
            | Wanxiangshu.Kernel.Errors.DomainError.ClientCancellation _
            | Wanxiangshu.Kernel.Errors.DomainError.MessageAborted -> threw <- true
            | _ -> ()

        check "promptWithAbort rejects on abort" threw
        check "promptWithAbort calls host session.abort" abortCalled
        equal "promptWithAbort aborts child path" "child-abort-host" abortPath
    }

let promptWithAbort_staleGateSkipsHostAbort () =
    promise {
        let mutable abortCount = 0

        let session =
            createObj
                [ "prompt", box (fun (_: obj) -> Promise.create (fun _ _ -> ()))
                  "abort",
                  box (fun (_: obj) ->
                      abortCount <- abortCount + 1
                      Promise.lift ()) ]

        let client = createObj [ "session", box session ]
        let signal, trigger = createMockAbortSignal ()
        let args = box {| path = box {| id = "child-stale" |} |}

        let gate =
            Wanxiangshu.Hosts.Opencode.SubagentSpawnTransport.createPromptAbortGate ()

        Wanxiangshu.Hosts.Opencode.SubagentSpawnTransport.bumpPromptAbortEpoch gate
        |> ignore

        let task =
            Wanxiangshu.Hosts.Opencode.SubagentSpawnTransport.promptWithAbortOwned client args signal (Some gate)

        let! () = yieldMicrotask ()
        Wanxiangshu.Hosts.Opencode.SubagentSpawnTransport.closePromptAbortGate gate
        trigger ()

        try
            let! _ = task
            ()
        with _ ->
            ()

        check "closed gate skips physical host abort" (abortCount = 0)
    }

let promptWithAbort_alreadyAbortedCallsHostAbort () =
    promise {
        let mutable abortCalled = false

        let session =
            createObj
                [ "prompt", box (fun (_: obj) -> Promise.lift ())
                  "abort",
                  box (fun (_: obj) ->
                      abortCalled <- true
                      Promise.lift ()) ]

        let client = createObj [ "session", box session ]
        let signal = createObj [ "aborted", box true ]
        let args = box {| path = box {| id = "child-pre" |} |}

        let mutable threw = false

        try
            do! Wanxiangshu.Hosts.Opencode.SubagentSpawnTransport.promptWithAbort client args signal
        with _ ->
            threw <- true

        check "pre-aborted signal rejects" threw
        check "pre-aborted signal still host-aborts" abortCalled
    }

let run () : JS.Promise<unit> =
    promise {
        do! promptWithAbort_abortsHostWhenSignalWins ()
        do! promptWithAbort_staleGateSkipsHostAbort ()
        do! promptWithAbort_alreadyAbortedCallsHostAbort ()
    }

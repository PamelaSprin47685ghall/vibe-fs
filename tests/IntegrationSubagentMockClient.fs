module Wanxiangshu.Tests.IntegrationSubagentMockClient

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter
open Wanxiangshu.Kernel.Subsession.Types

let makeMockClient (pObjRef: obj ref) (parentId: string) (responseText: string) =
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mutable sessionCounter = 0
    let mutable sessionNonces = Map.empty<string, string>

    let mockClient =
        createObj
            [ "session",
              box (
                  createObj
                      [ "create",
                        box (
                            System.Func<obj, JS.Promise<obj>>(fun arg ->
                                (promise {
                                    createCalls.Add(arg)
                                    sessionCounter <- sessionCounter + 1
                                    let childId = parentId + "-session-" + string sessionCounter
                                    return box {| data = box {| id = childId |} |}
                                }))
                        )
                        "prompt",
                        box (
                            System.Func<obj, JS.Promise<unit>>(fun arg ->
                                (promise {
                                    promptCalls.Add(arg)

                                    if not (isNull pObjRef.Value) then
                                        let runtime = pObjRef.Value?__fallbackRuntime |> unbox<FallbackRuntimeStore>

                                        let childId = Dyn.str (Dyn.get arg "path") "id"
                                        runtime.SetTaskComplete childId true

                                        let nonce =
                                            let body = Dyn.get arg "body"

                                            if Dyn.isNullish body then
                                                ""
                                            else
                                                let parts = Dyn.get body "parts"

                                                if Dyn.isNullish parts || not (Dyn.isArray parts) then
                                                    ""
                                                else
                                                    let firstPart = Dyn.get parts "0"

                                                    if Dyn.isNullish firstPart then
                                                        ""
                                                    else
                                                        let meta = Dyn.get firstPart "metadata"
                                                        if Dyn.isNullish meta then "" else Dyn.str meta "nonce"

                                        if nonce <> "" then
                                            sessionNonces <- Map.add childId nonce sessionNonces
                                            let msgId = childId + "-msg"
                                            let receipt = UserMessageObserved msgId

                                            let _ =
                                                Wanxiangshu.Hosts.Opencode.SubsessionDispatch.PendingTurnReceipt.tryResolve
                                                    nonce
                                                    receipt

                                            ()
                                        else
                                            sessionNonces <- Map.remove childId sessionNonces

                                        // Drive the real event hook after prompt resolution. OpenCode emits
                                        // message.updated BEFORE session.idle; the actor needs the output evidence
                                        // before processing idle, otherwise it completes with no text.
                                        let eventHook = Dyn.get pObjRef.Value "event"

                                        if not (Dyn.isNullish eventHook) then
                                            JS.setTimeout
                                                (fun () ->
                                                    promise {
                                                        let messageUpdatedEvent =
                                                            let infoObj =
                                                                match Map.tryFind childId sessionNonces with
                                                                | Some n when n <> "" ->
                                                                    box {| role = "assistant"; nonce = n |}
                                                                | _ -> box {| role = "assistant" |}

                                                            box
                                                                {| event =
                                                                    box
                                                                        {| ``type`` = "message.updated"
                                                                           properties =
                                                                            box
                                                                                {| sessionID = childId
                                                                                   info = infoObj
                                                                                   parts =
                                                                                    [| box
                                                                                           {| ``type`` = "text"
                                                                                              text = responseText |} |] |} |} |}

                                                        do!
                                                            (eventHook $ messageUpdatedEvent)
                                                            |> unbox<JS.Promise<unit>>

                                                        let idleEvent =
                                                            box
                                                                {| event =
                                                                    box
                                                                        {| ``type`` = "session.idle"
                                                                           properties = box {| sessionID = childId |} |} |}

                                                        do! (eventHook $ idleEvent) |> unbox<JS.Promise<unit>>
                                                    }
                                                    |> ignore)
                                                0
                                            |> ignore
                                }))
                        )
                        "messages",
                        box (
                            System.Func<obj, JS.Promise<obj>>(fun arg ->
                                (promise {
                                    let childId =
                                        if Dyn.isNullish arg then
                                            ""
                                        else
                                            let path = Dyn.get arg "path"
                                            if Dyn.isNullish path then "" else Dyn.str path "id"

                                    let userMessage =
                                        box (
                                            createObj
                                                [ "info", box (createObj [ "role", box "user" ])
                                                  "parts",
                                                  box
                                                      [| box (createObj [ "type", box "text"; "text", box "prompt" ]) |] ]
                                        )

                                    let infoList: (string * obj) list =
                                        [ "role", box "assistant"
                                          "model",
                                          box (createObj [ "providerID", box "mock"; "modelID", box "mock-model" ]) ]

                                    let infoListWithNonce =
                                        if childId <> "" then
                                            match Map.tryFind childId sessionNonces with
                                            | Some n when n <> "" -> infoList @ [ "nonce", box n ]
                                            | _ -> infoList
                                        else
                                            infoList

                                    let assistantMessage =
                                        box (
                                            createObj
                                                [ "info", box (createObj infoListWithNonce)
                                                  "parts",
                                                  box
                                                      [| box (
                                                             createObj [ "type", box "text"; "text", box responseText ]
                                                         ) |] ]
                                        )

                                    return box {| data = [| userMessage; assistantMessage |] |}
                                }))
                        )
                        "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (Promise.lift ()))) ]
              ) ]

    createCalls, promptCalls, mockClient

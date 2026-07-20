module Wanxiangshu.Tests.IntegrationSubagentMockClient

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeCompactionPure
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec

module Metadata = Wanxiangshu.Runtime.OpencodeSessionPromptCodec.WanxiangshuMetadataCodec

let makeMockClient
    (pObjRef: obj ref)
    (fallbackRuntimeRef: obj ref)
    (workspaceRef: string ref)
    (parentId: string)
    (responseText: string)
    =
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

                                    if not (isNull fallbackRuntimeRef.Value) then
                                        let runtime = unbox<FallbackRuntimeStore> fallbackRuntimeRef.Value

                                        let childId = Dyn.str (Dyn.get arg "path") "id"
                                        runtime.UpdateSession(childId, setTaskComplete)

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
                                                        match Metadata.tryDecodeFromPart firstPart with
                                                        | Some m when m.Nonce <> "" -> m.Nonce
                                                        | _ -> ""

                                        if nonce <> "" then
                                            sessionNonces <- Map.add childId nonce sessionNonces
                                            let msgId = childId + "-msg"
                                            let receipt = UserMessageObserved msgId
                                            let ws = workspaceFor workspaceRef.Value

                                            let _ = HostReceiptWaiterRegistry.tryResolve ws childId nonce receipt

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
                                                            let partMetadata =
                                                                match Map.tryFind childId sessionNonces with
                                                                | Some n when n <> "" ->
                                                                    Metadata.encodePartMetadata
                                                                        n
                                                                        Metadata.nudgeKind
                                                                        None
                                                                        0
                                                                        0
                                                                        ""
                                                                        0
                                                                        0
                                                                | _ -> box null

                                                            let infoObj = box {| role = "assistant" |}

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
                                                                                              text = responseText
                                                                                              metadata = partMetadata |} |] |} |} |}

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

                                    let partMetadata =
                                        if childId <> "" then
                                            match Map.tryFind childId sessionNonces with
                                            | Some n when n <> "" ->
                                                Metadata.encodePartMetadata n Metadata.nudgeKind None 0 0 "" 0 0
                                            | _ -> box null
                                        else
                                            box null

                                    let assistantMessage =
                                        box (
                                            createObj
                                                [ "info", box (createObj infoList)
                                                  "parts",
                                                  box
                                                      [| box (
                                                             createObj
                                                                 [ "type", box "text"
                                                                   "text", box responseText
                                                                   "metadata", partMetadata ]
                                                         ) |] ]
                                        )

                                    return box {| data = [| userMessage; assistantMessage |] |}
                                }))
                        )
                        "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (Promise.lift ()))) ]
              ) ]

    createCalls, promptCalls, mockClient

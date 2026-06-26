module Wanxiangshu.Tests.IntegrationSubmitKnowledgeGraphSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.KnowledgeGraphFiles
open Wanxiangshu.Shell.Dyn

open Wanxiangshu.Tests.IntegrationSubmitKnowledgeGraphSpecsAppend

let submitKnowledgeGraphRejectsSecondCallSpec () = promise {
    let! workspaceDir = mkdtempAsync "kg-second-call-"
    do! ensureKnowledgeGraphDir workspaceDir
    let sessionID = "kg-second-call-session"
    let marker = renderJobMarker { workspaceRoot = workspaceDir; kind = AppendAfterWork }
    let appendDay = "2026-06-20"
    let mutable firstResult = ""
    let mutable calledOnce = false
    let mockClient =
        createObj [ "session", box (createObj [
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                promise {
                    if not calledOnce then
                        return box {| data = [| userTextMessage sessionID marker |] |}
                    else
                        let toolMsg =
                            box {|
                                info = createObj [
                                    "id", box (sessionID + "-tool")
                                    "agent", box "bookkeeper"
                                    "sessionID", box sessionID
                                    "role", box "assistant"
                                ]
                                parts = [| box {|
                                    ``type`` = "tool"
                                    tool = "return_bookkeeper"
                                    callID = "kg-second-call-1"
                                    state = createObj [
                                        "status", box "completed"
                                        "output", box firstResult
                                        "error", box ""
                                        "input", box (createObj [ "entries", box [| knowledgeGraphDraftEntry None ["首次问题"] "首次答案" |] ])
                                    ]
                                |} |]
                            |}
                        return box {| data = [| userTextMessage sessionID marker; toolMsg |] |}
                }))
            "todo", box (System.Func<unit, JS.Promise<obj>>(fun () -> promise { return box {| data = [||] |} }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ()))
            "create", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = box {| id = "child-bookkeeper-session" |} |} }))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ()))
        ]) ]
    let! p = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs appendDay |})
    registerKnowledgeGraphJobForTest (pluginKnowledgeGraphRuntime p) sessionID workspaceDir "append" (createObj [ "today", box appendDay ])
    let submitTool = submitKnowledgeGraphTool p
    let entries1 = [| knowledgeGraphDraftEntry None ["首次问题"] "首次答案" |]
    let! result1 =
        ((get submitTool "execute")
            $ (createObj [ "entries", box entries1 ], createObj [ "directory", box workspaceDir; "sessionID", box sessionID ]))
        |> unbox<JS.Promise<string>>
    check "return_bookkeeper first call succeeds" (result1.Contains "Appended 1 knowledge graph entries")
    firstResult <- result1
    calledOnce <- true
    let! filesAfterFirst = readAllKnowledgeGraphFiles workspaceDir
    let entryCountAfterFirst =
        filesAfterFirst |> List.sumBy (fun f -> f.entries.Length)
    let entries2 = [| knowledgeGraphDraftEntry None ["二次问题"] "二次答案" |]
    let! result2 =
        ((get submitTool "execute")
            $ (createObj [ "entries", box entries2 ], createObj [ "directory", box workspaceDir; "sessionID", box sessionID ]))
        |> unbox<JS.Promise<string>>
    check "return_bookkeeper second call returns scold not append success"
        (not (result2.Contains "Appended") && result2 <> "")
    check "return_bookkeeper second call contains rejection phrase"
        (result2.Contains "already completed" || result2.Contains "Do not call return_bookkeeper again")
    check "return_bookkeeper second call does not mention No active job"
        (not (result2.Contains "No active knowledge graph job"))
    let! filesAfterSecond = readAllKnowledgeGraphFiles workspaceDir
    let entryCountAfterSecond =
        filesAfterSecond |> List.sumBy (fun f -> f.entries.Length)
    check "return_bookkeeper second call no extra NDJSON entries"
        (entryCountAfterSecond = entryCountAfterFirst)
    do! rmAsync workspaceDir
}
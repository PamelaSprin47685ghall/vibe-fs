module VibeFs.Tests.IntegrationDedupOpenCodeSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace

open VibeFs.Kernel.MagicCore
open VibeFs.Opencode.Plugin
open VibeFs.Shell.Dyn


let private findMsgById (msgs: obj[]) (idPrefix: string) : obj =
    msgs |> Array.find (fun m -> str (get m "info") "id" = idPrefix)

let private info (id: string) (sessionID: string) : obj =
    createObj [
        "id", box id
        "agent", box "manager"
        "role", box "assistant"
        "sessionID", box sessionID
    ]

let private readToolPart (output: string) : obj =
    createObj [
        "type", box "tool"
        "tool", box "read"
        "state", box (createObj [ "status", box "completed"; "output", box output ])
    ]

let private todoToolPart (report: string) : obj =
    createObj [
        "type", box "tool"
        "tool", box magicTodoToolName
        "state", box (createObj [
            "status", box "completed"
            "input", box (createObj [ "completedWorkReport", box report; "todos", box [||] ])
            "output", box "Todos updated."
        ])
    ]

let private assistantMsg (id: string) (sessionID: string) (parts: obj array) : obj =
    createObj [ "info", box (info id sessionID); "parts", box parts ]

let private readOutputOf (msg: obj) : string =
    let part = get msg "parts" |> unbox<obj[]> |> Array.item 0
    let state = get part "state"
    str state "output"

let opencodeDedupInPlaceSpec () = promise {
    let! workspaceDir = mkdtempAsync "dedup-plugin-"
    let! p = plugin (box {| directory = workspaceDir |})
    let stableContent = String.replicate 8 "line of stable content\n"
    let readStateA = createObj [ "output", box stableContent ]
    let readStateB = createObj [ "output", box stableContent ]
    let readPartA = createObj [ "type", box "tool"; "tool", box "read"; "state", box readStateA ]
    let readPartB = createObj [ "type", box "tool"; "tool", box "read"; "state", box readStateB ]
    let dedupInPlace =
        createObj [ "messages", box [|
            createObj [ "info", box (createObj [ "id", box "dedup-m1"; "agent", box "manager"; "role", box "assistant"; "sessionID", box "dedup-session" ])
                        "parts", box [| readPartA |] ]
            createObj [ "info", box (createObj [ "id", box "dedup-m2"; "agent", box "manager"; "role", box "assistant"; "sessionID", box "dedup-session" ])
                        "parts", box [| readPartB |] ]
        |] ]
    let dedupMessagesRef = get dedupInPlace "messages"
    do! (get p "experimental.chat.messages.transform") $ (box {| sessionID = "dedup-session" |}, dedupInPlace) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> dedupMessagesRef
    check "opencode dedup keeps messages array ref" (obj.ReferenceEquals(msgs, unbox<obj[]> (get dedupInPlace "messages")))
    let msg1 = findMsgById msgs "dedup-m1"
    let msg2 = findMsgById msgs "dedup-m2"
    let partA = get msg1 "parts" |> unbox<obj[]> |> Array.item 0
    let partB = get msg2 "parts" |> unbox<obj[]> |> Array.item 0
    check "opencode dedup keeps first part ref" (obj.ReferenceEquals(partA, readPartA))
    check "opencode dedup keeps second part ref" (obj.ReferenceEquals(partB, readPartB))
    let stateA = get partA "state"
    let stateB = get partB "state"
    check "opencode dedup keeps first state ref" (obj.ReferenceEquals(stateA, readStateA))
    check "opencode dedup keeps second state ref" (obj.ReferenceEquals(stateB, readStateB))
    check "opencode dedup keeps first read output" (str stateA "output" = stableContent)
    check "opencode dedup replaces exact duplicate" (str stateB "output" = VibeFs.Kernel.ToolOutputInfo.noChangeEnvelope ())
    let supersetContent = stableContent + String.replicate 8 "new content\n"
    let supersetState = createObj [ "output", box supersetContent ]
    let supersetPart = createObj [ "type", box "tool"; "tool", box "read"; "state", box supersetState ]
    let dedupSuperset =
        createObj [ "messages", box [|
            createObj [ "info", box (createObj [ "id", box "dedup-s1"; "agent", box "manager"; "role", box "assistant"; "sessionID", box "dedup-session2" ])
                        "parts", box [| readPartA |] ]
            createObj [ "info", box (createObj [ "id", box "dedup-s2"; "agent", box "manager"; "role", box "assistant"; "sessionID", box "dedup-session2" ])
                        "parts", box [| supersetPart |] ]
        |] ]
    do! (get p "experimental.chat.messages.transform") $ (box {| sessionID = "dedup-session2" |}, dedupSuperset) |> unbox<JS.Promise<unit>>
    let supMsgs = unbox<obj[]> (get dedupSuperset "messages")
    let supMsg = findMsgById supMsgs "dedup-s2"
    let supPart = get supMsg "parts" |> unbox<obj[]> |> Array.item 0
    let supState = get supPart "state"
    check "opencode dedup superset keeps state ref" (obj.ReferenceEquals(supState, supersetState))
    check "opencode dedup superset not replaced" (str supState "output" = supersetContent)
    do! rmAsync workspaceDir
}

let opencodeDedupIgnoresMagicFoldedReadsSpec () = promise {
    let! workspaceDir = mkdtempAsync "dedup-magic-"
    let! p = plugin (box {| directory = workspaceDir |})
    let sessionID = "dedup-magic-session"
    let messages =
        createObj [ "messages", box [|
            assistantMsg "mg-m1" sessionID [| readToolPart "same" |]
            assistantMsg "mg-m2" sessionID [| todoToolPart "first" |]
            assistantMsg "mg-m3" sessionID [| readToolPart "same" |]
            assistantMsg "mg-m4" sessionID [| todoToolPart "second" |]
            assistantMsg "mg-m5" sessionID [| todoToolPart "third" |]
            assistantMsg "mg-m6" sessionID [| readToolPart "same" |]
        |] ]
    let messagesRef = get messages "messages"
    do! (get p "experimental.chat.messages.transform") $ (box {| sessionID = sessionID |}, messages) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> messagesRef
    check "opencode dedup ignores magic folded reads: keeps messages ref" (obj.ReferenceEquals(msgs, unbox<obj[]> (get messages "messages")))
    let latest = findMsgById msgs "mg-m6"
    check "opencode dedup ignores magic folded reads: latest read kept" (readOutputOf latest = "same")
    do! rmAsync workspaceDir
}

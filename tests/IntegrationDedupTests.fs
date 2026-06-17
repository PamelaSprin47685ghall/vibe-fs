module VibeFs.Tests.IntegrationDedupTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Dyn
open VibeFs.Index
open VibeFs.Opencode.Plugin

let private fileReadOutput (content: string) : obj =
    box {| success = true; file_size = content.Length; modifiedTime = "2024-01-01T00:00:00.000Z"; lines_read = 1; content = content |}

let private readMsg (toolName: string) (output: obj) (callId: string) : obj =
    box {| parts = [| box {| ``type`` = "dynamic-tool"; toolName = toolName; state = "output-available"; output = output; toolCallId = callId |} |] |}

let private readMsgWithPath (toolName: string) (filePath: string) (output: obj) (callId: string) : obj =
    box {| parts = [| box {| ``type`` = "dynamic-tool"; toolName = toolName; state = "output-available"
                             input = box {| path = filePath |}; output = output; toolCallId = callId |} |] |}

let private firstOutput (msg: obj) : obj =
    get (unbox<obj[]> (get msg "parts")).[0] "output"

let dedupStringOutputSpec () =
    let msgs = [| readMsg "read" (box "same content") "1"; readMsg "file_read" (box "same content") "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup string: keeps first" (unbox<string> (firstOutput r.[0]) = "same content")
    check "dedup string: replaces repeat" (unbox<string> (firstOutput r.[1]) = "[No Change Since Previous Read/Write]")

let dedupObjectOutputSpec () =
    let msgs = [| readMsg "file_read" (fileReadOutput "hello") "1"; readMsg "file_read" (fileReadOutput "hello") "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup object: keeps first content" (str (firstOutput r.[0]) "content" = "hello")
    check "dedup object: replaces repeat" (unbox<string> (firstOutput r.[1]) = "[No Change Since Previous Read/Write]")

let dedupPerPathDifferentSpec () =
    let shared = fileReadOutput "shared bytes"
    let msgs = [| readMsgWithPath "file_read" "a.ts" shared "1"; readMsgWithPath "file_read" "b.ts" shared "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup per-path: different path not deduped" (str (firstOutput r.[1]) "content" = "shared bytes")

let dedupPerPathSameSpec () =
    let out = fileReadOutput "repeat me"
    let msgs = [| readMsgWithPath "file_read" "same.ts" out "1"; readMsgWithPath "file_read" "same.ts" out "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup same path: second marked" (unbox<string> (firstOutput r.[1]) = "[No Change Since Previous Read/Write]")

let dedupDifferentSpec () =
    let msgs = [| readMsg "read" (box "unique a") "1"; readMsg "read" (box "unique b") "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup different: first unchanged" (unbox<string> (firstOutput r.[0]) = "unique a")
    check "dedup different: second unchanged" (unbox<string> (firstOutput r.[1]) = "unique b")

let dedupNonReadSpec () =
    let msgs = [|
        readMsg "read" (box "read content") "1"
        box {| parts = [| box {| ``type`` = "dynamic-tool"; toolName = "write"; state = "output-available"; output = box "write result"; toolCallId = "2" |} |] |}
    |]
    let r = deduplicateReadOutputs msgs
    check "dedup non-read: write preserved" (unbox<string> (firstOutput r.[1]) = "write result")

let dedupEmptySpec () =
    let r = deduplicateReadOutputs [||]
    check "dedup empty: empty array" (r.Length = 0)

let collectReadOutputsSpec () =
    let seen = collectReadOutputs [| readMsg "read" (box "seen before") "h1" |]
    check "collect string: returns array" (seen.Length = 1 && seen.[0] = "seen before")
    let seenObj = collectReadOutputs [| readMsg "file_read" (fileReadOutput "historical") "h1" |]
    check "collect object: extracts content" (seenObj.Length = 1 && seenObj.[0] = "historical")

let collectOrderSpec () =
    let seen = collectReadOutputs [|
        readMsgWithPath "file_read" "z.ts" (fileReadOutput "first") "1"
        readMsgWithPath "file_read" "a.ts" (fileReadOutput "second") "2"
    |]
    check "collectReadOutputs preserves message order" (seen.Length = 2 && seen.[0] = "first" && seen.[1] = "second")

let dedupAgainstHistorySpec () =
    let history = [| readMsg "file_read" (fileReadOutput "from history") "h1" |]
    let window = [| readMsg "file_read" (fileReadOutput "from history") "w1" |]
    let r = deduplicateReadOutputsAgainstHistory history window
    check "againstHistory: repeat vs history marked" (unbox<string> (firstOutput r.[0]) = "[No Change Since Previous Read/Write]")

let dedupAgainstHistoryWindowSpec () =
    let window = [| readMsg "read" (box "same") "w1"; readMsg "read" (box "same") "w2" |]
    let r = deduplicateReadOutputsAgainstHistory [||] window
    check "againstHistory: window first kept" (unbox<string> (firstOutput r.[0]) = "same")
    check "againstHistory: window second marked" (unbox<string> (firstOutput r.[1]) = "[No Change Since Previous Read/Write]")

let dedupAgainstHistoryPerPathSpec () =
    let shared = fileReadOutput "shared across files"
    let history = [| readMsgWithPath "file_read" "a.ts" shared "h1" |]
    let window = [| readMsgWithPath "file_read" "b.ts" shared "w1" |]
    let r = deduplicateReadOutputsAgainstHistory history window
    check "againstHistory per-path: different path not deduped" (str (firstOutput r.[0]) "content" = "shared across files")

let dedupModelTextSpec () =
    let seen, msgs = deduplicateModelReadOutputsWithSeen [||] [|
        box {| content = [| box {| ``type`` = "tool-result"; toolName = "read"; output = box {| ``type`` = "text"; value = box "hello" |} |} |] |}
        box {| content = [| box {| ``type`` = "tool-result"; toolName = "read"; output = box {| ``type`` = "text"; value = box "hello" |} |} |] |}
    |]
    check "ModelMessage text: returns seen" (seen |> Array.contains "hello")
    let firstOut = get ((unbox<obj[]> (get msgs.[0] "content")).[0]) "output"
    let secondOut = get ((unbox<obj[]> (get msgs.[1] "content")).[0]) "output"
    check "ModelMessage text: first preserved" (str firstOut "value" = "hello")
    check "ModelMessage text: second replaced" (str secondOut "value" = "[No Change Since Previous Read/Write]")

let dedupModelJsonSpec () =
    let seen, msgs = deduplicateModelReadOutputsWithSeen [||] [|
        box {| content = [| box {| ``type`` = "tool-result"; toolName = "file_read"; output = box {| ``type`` = "json"; value = box {| content = "json content" |} |} |} |] |}
        box {| content = [| box {| ``type`` = "tool-result"; toolName = "file_read"; output = box {| ``type`` = "json"; value = box {| content = "json content" |} |} |} |] |}
    |]
    check "ModelMessage json: returns seen" (seen |> Array.contains "json content")
    let secondOut = get ((unbox<obj[]> (get msgs.[1] "content")).[0]) "output"
    check "ModelMessage json: second replaced" (str secondOut "value" = "[No Change Since Previous Read/Write]")

let dedupModelDifferentSpec () =
    let _, msgs = deduplicateModelReadOutputsWithSeen [||] [|
        box {| content = [| box {| ``type`` = "tool-result"; toolName = "read"; output = box {| ``type`` = "text"; value = box "first" |} |} |] |}
        box {| content = [| box {| ``type`` = "tool-result"; toolName = "read"; output = box {| ``type`` = "text"; value = box "second" |} |} |] |}
    |]
    let firstOut = get ((unbox<obj[]> (get msgs.[0] "content")).[0]) "output"
    let secondOut = get ((unbox<obj[]> (get msgs.[1] "content")).[0]) "output"
    check "ModelMessage different: first unchanged" (str firstOut "value" = "first")
    check "ModelMessage different: second unchanged" (str secondOut "value" = "second")

let dedupModelNonReadSpec () =
    let _, msgs = deduplicateModelReadOutputsWithSeen [||] [|
        box {| content = [| box {| ``type`` = "tool-result"; toolName = "write"; output = box {| ``type`` = "text"; value = box "write result" |} |} |] |}
    |]
    let out = get ((unbox<obj[]> (get msgs.[0] "content")).[0]) "output"
    check "ModelMessage non-read: write preserved" (str out "value" = "write result")

let dedupModelEmptySpec () =
    let _, msgs = deduplicateModelReadOutputsWithSeen [||] [||]
    check "ModelMessage empty: empty array" (msgs.Length = 0)

let private findMsgById (msgs: obj[]) (idPrefix: string) : obj =
    msgs |> Array.find (fun m -> str (get m "info") "id" = idPrefix)

let opencodeDedupInPlaceSpec () = async {
    let! workspaceDir = mkdtempAsync "dedup-plugin-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
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
    do! (get p "experimental.chat.messages.transform") $ (box {| sessionID = "dedup-session" |}, dedupInPlace) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
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
    check "opencode dedup replaces exact duplicate" (str stateB "output" = "[No Change Since Previous Read/Write]")
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
    do! (get p "experimental.chat.messages.transform") $ (box {| sessionID = "dedup-session2" |}, dedupSuperset) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let supMsgs = unbox<obj[]> (get dedupSuperset "messages")
    let supMsg = findMsgById supMsgs "dedup-s2"
    let supPart = get supMsg "parts" |> unbox<obj[]> |> Array.item 0
    let supState = get supPart "state"
    check "opencode dedup superset keeps state ref" (obj.ReferenceEquals(supState, supersetState))
    check "opencode dedup superset not replaced" (str supState "output" = supersetContent)
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let run () : JS.Promise<unit> =
    async {
        dedupStringOutputSpec ()
        dedupObjectOutputSpec ()
        dedupPerPathDifferentSpec ()
        dedupPerPathSameSpec ()
        dedupDifferentSpec ()
        dedupNonReadSpec ()
        dedupEmptySpec ()
        collectReadOutputsSpec ()
        collectOrderSpec ()
        dedupAgainstHistorySpec ()
        dedupAgainstHistoryWindowSpec ()
        dedupAgainstHistoryPerPathSpec ()
        dedupModelTextSpec ()
        dedupModelJsonSpec ()
        dedupModelDifferentSpec ()
        dedupModelNonReadSpec ()
        dedupModelEmptySpec ()
        do! opencodeDedupInPlaceSpec ()
    }
    |> Async.StartAsPromise

module VibeFs.Tests.IntegrationDedupTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationDedupOpenCodeSpecs

open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.ToolOutputInfo
open VibeFs.Opencode.Plugin
open VibeFs.Shell.Dyn

let private deduplicateReadOutputs (messages: obj array) : obj array =
    VibeFs.Mux.ReadDedup.deduplicateReadOutputsWithSeen [||] messages

let private deduplicateReadOutputsAgainstHistory (history: obj array) (messages: obj array) : obj array =
    let seenByPath = VibeFs.Mux.ReadDedup.collectReadOutputsByPath history
    VibeFs.Mux.ReadDedup.deduplicateReadOutputsWithSeenByPath seenByPath messages

let private deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj array =
    VibeFs.Mux.ReadDedup.deduplicateModelReadOutputsWithSeen seenOutputs messages

let private collectReadOutputs (messages: obj array) : string[] =
    VibeFs.Mux.ReadDedup.collectReadOutputs messages

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
    check "dedup string: replaces repeat" (unbox<string> (firstOutput r.[1]) = noChangeEnvelope ())

let dedupObjectOutputSpec () =
    let msgs = [| readMsg "file_read" (fileReadOutput "hello") "1"; readMsg "file_read" (fileReadOutput "hello") "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup object: keeps first content" (str (firstOutput r.[0]) "content" = "hello")
    check "dedup object: replaces repeat" (str (firstOutput r.[1]) "content" = noChangeEnvelope ())

let dedupPerPathDifferentSpec () =
    let shared = fileReadOutput "shared bytes"
    let msgs = [| readMsgWithPath "file_read" "a.ts" shared "1"; readMsgWithPath "file_read" "b.ts" shared "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup per-path: different path not deduped" (str (firstOutput r.[1]) "content" = "shared bytes")

let dedupPerPathSameSpec () =
    let out = fileReadOutput "repeat me"
    let msgs = [| readMsgWithPath "file_read" "same.ts" out "1"; readMsgWithPath "file_read" "same.ts" out "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup same path: second marked" (str (firstOutput r.[1]) "content" = noChangeEnvelope ())

let dedupSubstringSpec () =
    let msgs = [| readMsg "read" (box "hello world foo bar") "1"; readMsg "read" (box "hello world") "2" |]
    let r = deduplicateReadOutputs msgs
    check "dedup substring: keeps first longer output" (unbox<string> (firstOutput r.[0]) = "hello world foo bar")
    check "dedup substring: marks substring against longer seen" (unbox<string> (firstOutput r.[1]) = noChangeEnvelope ())

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
    check "againstHistory: repeat vs history marked" (str (firstOutput r.[0]) "content" = noChangeEnvelope ())

let dedupAgainstHistoryWindowSpec () =
    let window = [| readMsg "read" (box "same") "w1"; readMsg "read" (box "same") "w2" |]
    let r = deduplicateReadOutputsAgainstHistory [||] window
    check "againstHistory: window first kept" (unbox<string> (firstOutput r.[0]) = "same")
    check "againstHistory: window second marked" (unbox<string> (firstOutput r.[1]) = noChangeEnvelope ())

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
    check "ModelMessage text: second replaced" (str secondOut "value" = noChangeEnvelope ())

let dedupModelJsonSpec () =
    let seen, msgs = deduplicateModelReadOutputsWithSeen [||] [|
        box {| content = [| box {| ``type`` = "tool-result"; toolName = "file_read"; output = box {| ``type`` = "json"; value = box {| content = "json content" |} |} |} |] |}
        box {| content = [| box {| ``type`` = "tool-result"; toolName = "file_read"; output = box {| ``type`` = "json"; value = box {| content = "json content" |} |} |} |] |}
    |]
    check "ModelMessage json: returns seen" (seen |> Array.contains "json content")
    let secondOut = get ((unbox<obj[]> (get msgs.[1] "content")).[0]) "output"
    check "ModelMessage json: second replaced" (str secondOut "value" = noChangeEnvelope ())

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

let run () : JS.Promise<unit> =
    promise {
        dedupStringOutputSpec ()
        dedupObjectOutputSpec ()
        dedupPerPathDifferentSpec ()
        dedupPerPathSameSpec ()
        dedupSubstringSpec ()
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
        do! opencodeDedupIgnoresMagicFoldedReadsSpec ()
    }

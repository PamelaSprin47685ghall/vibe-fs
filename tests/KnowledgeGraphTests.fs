module VibeFs.Tests.KnowledgeGraphTests

open Fable.Core
open VibeFs.Tests.Assert
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraph.JobTesting
open VibeFs.Kernel.Messaging

open VibeFs.Tests.KnowledgeGraphTestsCodec

let private ok r =
    match r with
    | Ok v -> v
    | Error e -> failwithf "%A" e

let private some (o: 'a option) : 'a =
    match o with Some v -> v | None -> failwith "expected Some"

let private isOk r =
    match r with
    | Ok _ -> true
    | Error _ -> false

let private isErr r =
    match r with
    | Ok _ -> false
    | Error _ -> true

let private entry idStr entities fact : KnowledgeGraphEntry =
    { id = some (tryParseId idStr); entity = entities; fact = fact }

let private projection (entries: KnowledgeGraphEntry list) : KnowledgeGraphProjection =
    entries |> List.map (fun e -> e.id, e) |> Map.ofList

let fetchAnswerSpec () =
    let e1 = entry "0a3f" ["项目插件入口"] "src/Opencode/Plugin.fs"
    let e2 = entry "b912" ["项目插件入口"] "build/src/Mux/Plugin.js"
    let proj = projection [ e1; e2 ]
    let result = ok (fetchAnswer proj "项目插件入口")
    check "fetchAnswer concatenates facts for entity" (result.Contains "src/Opencode/Plugin.fs" && result.Contains "build/src/Mux/Plugin.js")
    check "fetchAnswer entity no match Error" (isErr (fetchAnswer proj "missing entity"))

let draftValidationSpec () =
    check "validateDraft valid id Ok" (isOk (validateDraft { id = Some "0a3f"; entity = ["e"]; fact = "f" }))
    check "validateDraft bad id Error" (isErr (validateDraft { id = Some "BAD"; entity = ["e"]; fact = "f" }))
    check "validateDraft empty entity Error" (isErr (validateDraft { id = None; entity = []; fact = "f" }))
    check "validateDraft empty fact Error" (isErr (validateDraft { id = None; entity = ["e"]; fact = "" }))
    check "validateDraft no id Ok" (isOk (validateDraft { id = None; entity = ["e"]; fact = "f" }))

let applyDraftsSpec () =
    let counter = ref 0
    let allocator (_existingIds: Set<string>) : Result<string, string> =
        counter.Value <- counter.Value + 1
        Ok (sprintf "%04x" counter.Value)
    let existing = entry "0a3f" ["old e"] "old fact"
    let proj = projection [ existing ]
    let drafts =
        [ { id = Some "0a3f"; entity = ["updated e"]; fact = "updated fact" }
          { id = Some "9999"; entity = ["ghost e"]; fact = "ghost fact" }
          { id = None; entity = ["fresh e"]; fact = "fresh fact" } ]
    let results = ok (applyDrafts allocator proj drafts)
    check "applyDrafts 3 results" (results.Length = 3)
    equal "applyDrafts existing id reused" "0a3f" (idValue results.[0].id)
    equal "applyDrafts existing id entity" ["updated e"] results.[0].entity
    equal "applyDrafts existing id fact" "updated fact" results.[0].fact
    equal "applyDrafts ghost id reassigned" "0001" (idValue results.[1].id)
    equal "applyDrafts ghost entity kept" ["ghost e"] results.[1].entity
    equal "applyDrafts fresh id assigned" "0002" (idValue results.[2].id)
    equal "applyDrafts fresh entity kept" ["fresh e"] results.[2].entity
    let empty = ok (applyDrafts allocator proj [])
    check "applyDrafts empty Ok" empty.IsEmpty

let allocateSpec () =
    let mutable i = 4
    let src () =
        i <- i + 1
        i
    let existing = Set.ofList [ sprintf "%04x" (5 % 65536) ]
    match allocateRandomHexId src existing with
    | Ok id -> equal "allocateRandomHexId skips existing" (sprintf "%04x" (6 % 65536)) id
    | Error _ -> check "allocateRandomHexId should find next free" false
    let always5 () = 5
    let existing5 = Set.ofList [ sprintf "%04x" (5 % 65536) ]
    check "allocateRandomHexId exhausted Error" (isErr (allocateRandomHexId always5 existing5))

let private mkToolMessage (role: Role) (toolName: string) (callID: string) (status: string) : Message<obj> =
    { info =
          { id = callID + "-msg"
            sessionID = "session-kg"
            role = role
            agent = "bookkeeper"
            isError = false
            toolName = toolName
            details = null
            time = null }
      parts = [ ToolPart(toolName, callID, Some { status = status; output = ""; error = ""; input = null; operationAction = "" }, null) ]
      source = Native
      raw = null }

let private mkTextMessage (role: Role) (text: string) : Message<obj> =
    { info =
          { id = text.GetHashCode().ToString() + "-msg"
            sessionID = "session-kg"
            role = role
            agent = "bookkeeper"
            isError = false
            toolName = ""
            details = null
            time = null }
      parts = [ TextPart text ]
      source = Native
      raw = null }

let private mkMarkerMessage (ctx: KnowledgeGraphJobContext) : Message<obj> =
    mkTextMessage User (renderJobMarker ctx)

let secondReturnBookkeeperDetectionSpec () =
    let ctx = { workspaceRoot = "/tmp/kg-root"; kind = AppendAfterWork }
    check "historyHasCompletedReturnBookkeeper empty history is false"
        (not (historyHasCompletedReturnBookkeeper []))
    check "historyHasCompletedReturnBookkeeper marker-only is false"
        (not (historyHasCompletedReturnBookkeeper [ mkMarkerMessage ctx ]))
    check "historyHasCompletedReturnBookkeeper pending tool is false"
        (not (historyHasCompletedReturnBookkeeper [
            mkMarkerMessage ctx
            mkToolMessage Assistant "return_bookkeeper" "call-1" "pending"
        ]))
    check "historyHasCompletedReturnBookkeeper completed tool is true"
        (historyHasCompletedReturnBookkeeper [
            mkMarkerMessage ctx
            mkToolMessage Assistant "return_bookkeeper" "call-1" "completed"
        ])
    check "historyHasCompletedReturnBookkeeper other tool completed is false"
        (not (historyHasCompletedReturnBookkeeper [
            mkToolMessage Assistant "file_read" "call-2" "completed"
        ]))
    let scold = rejectSecondReturnBookkeeperMessage
    check "rejectSecondReturnBookkeeperMessage non-empty" (scold <> "")
    check "rejectSecondReturnBookkeeperMessage is English rejection" (
        scold.Contains "already completed" && scold.Contains "Do not call return_bookkeeper again")

let run () : JS.Promise<unit> =
    promise {
        idParseSpec ()
        headerParseSpec ()
        headerRenderSpec ()
        entryParseRenderSpec ()
        ndjsonParseSpec ()
        ndjsonRenderSpec ()
        projectionSpec ()
        testingJobKindSpec ()
        jobMarkerSpec ()
        preludeSpec ()
        fetchAnswerSpec ()
        draftValidationSpec ()
        applyDraftsSpec ()
        allocateSpec ()
        secondReturnBookkeeperDetectionSpec ()
    }
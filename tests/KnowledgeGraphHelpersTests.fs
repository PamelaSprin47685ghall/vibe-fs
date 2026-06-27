module Wanxiangshu.Tests.KnowledgeGraphHelpersTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.BookkeeperPolicy
open Wanxiangshu.Kernel.KnowledgeGraph.Idempotency
open Wanxiangshu.Kernel.KnowledgeGraph.Id
open Wanxiangshu.Kernel.Messaging

// ── BookkeeperPolicy ─────────────────────────────────────────────────────────

let recordsToBookkeeperFileTools () =
    check "edit feeds bookkeeper" (recordsToBookkeeper "edit")
    check "write feeds bookkeeper" (recordsToBookkeeper "write")
    check "apply_patch feeds bookkeeper" (recordsToBookkeeper "apply_patch")
    check "patch feeds bookkeeper" (recordsToBookkeeper "patch")
    check "ast_edit feeds bookkeeper" (recordsToBookkeeper "ast_edit")

let recordsToBookkeeperSubagentTools () =
    check "coder feeds bookkeeper" (recordsToBookkeeper "coder")
    check "investigator feeds bookkeeper" (recordsToBookkeeper "investigator")
    check "meditator feeds bookkeeper" (recordsToBookkeeper "meditator")
    check "browser feeds bookkeeper" (recordsToBookkeeper "browser")
    check "executor feeds bookkeeper" (recordsToBookkeeper "executor")
    check "websearch feeds bookkeeper" (recordsToBookkeeper "websearch")
    check "webfetch feeds bookkeeper" (recordsToBookkeeper "webfetch")

let recordsToBookkeeperExcludesReviewAndLookup () =
    check "submit_review excluded" (not (recordsToBookkeeper "submit_review"))
    check "lookup excluded" (not (recordsToBookkeeper "lookup"))
    check "kg_query excluded" (not (recordsToBookkeeper "kg_query"))
    check "random tool excluded" (not (recordsToBookkeeper "random_tool"))

// ── Idempotency ──────────────────────────────────────────────────────────────

let makeToolPart (toolName: string) (status: string) : Part<obj> =
    let state =
        { status = status
          output = ""
          error = ""
          input = null
          operationAction = "" }
    ToolPart(toolName, "call-1", Some state, null)

let makeMsg (parts: Part<obj> list) : Message<obj> =
    { info =
        { id = "msg-1"
          sessionID = "sess-1"
          role = ToolResult
          agent = "assistant"
          isError = false
          toolName = ""
          details = null
          time = null }
      parts = parts
      source = Native
      raw = null }

let historyHasCompletedReturnBookkeeperTrue () =
    let msg = makeMsg [ makeToolPart "return_bookkeeper" "completed" ]
    check "completed return_bookkeeper detected" (historyHasCompletedReturnBookkeeper [ msg ])

let historyHasCompletedReturnBookkeeperFalseOtherTool () =
    let msg = makeMsg [ makeToolPart "coder" "completed" ]
    check "other tool completed not detected" (not (historyHasCompletedReturnBookkeeper [ msg ]))

let historyHasCompletedReturnBookkeeperFalseWrongStatus () =
    let msg = makeMsg [ makeToolPart "return_bookkeeper" "running" ]
    check "return_bookkeeper running not detected" (not (historyHasCompletedReturnBookkeeper [ msg ]))

let historyHasCompletedReturnBookkeeperFalseNoToolPart () =
    let msg = makeMsg [ TextPart "just text" ]
    check "text-only message not detected" (not (historyHasCompletedReturnBookkeeper [ msg ]))

let rejectSecondReturnBookkeeperMessageNonEmpty () =
    let msg = rejectSecondReturnBookkeeperMessage
    check "reject message is non-empty" (msg <> "")
    check "reject message mentions return_bookkeeper" (msg.Contains "return_bookkeeper")
    check "reject message warns about second call" (msg.Contains "Do not call return_bookkeeper again")

// ── KnowledgeGraph.Id ────────────────────────────────────────────────────────

let tryParseIdValid () =
    match tryParseId "0a3f" with
    | Some (KnowledgeGraphId v) -> check "tryParseId valid returns id" (v = "0a3f")
    | None -> check "tryParseId valid is None" false

let tryParseIdInvalid () =
    check "tryParseId non-hex returns None" (tryParseId "xyz1" = None)
    check "tryParseId empty returns None" (tryParseId "" = None)
    check "tryParseId too long returns None" (tryParseId "12345" = None)
    check "tryParseId too short returns None" (tryParseId "abc" = None)

let idValueExtracts () =
    let kgId = KnowledgeGraphId "0a3f"
    check "idValue returns inner string" (idValue kgId = "0a3f")

let allocateRandomHexIdReturnsUnique () =
    let rng = fun () -> 42
    match allocateRandomHexId rng Set.empty with
    | Ok v -> check "allocate returns ok" (v <> "")
    | Error _ -> check "allocate returns ok" false

let allocateRandomHexIdExhaustion () =
    // Pre-fill all 65536 slots so the next allocation must fail.
    let allIds =
        seq { 0 .. 65535 }
        |> Seq.map (sprintf "%04x")
        |> Set.ofSeq
    match allocateRandomHexId (fun () -> 0) allIds with
    | Error msg -> check "exhaustion returns Error" (msg = "knowledge graph id space exhausted")
    | Ok _ -> check "exhaustion returns Error" false

let run () =
    // BookkeeperPolicy
    recordsToBookkeeperFileTools ()
    recordsToBookkeeperSubagentTools ()
    recordsToBookkeeperExcludesReviewAndLookup ()
    // Idempotency
    historyHasCompletedReturnBookkeeperTrue ()
    historyHasCompletedReturnBookkeeperFalseOtherTool ()
    historyHasCompletedReturnBookkeeperFalseWrongStatus ()
    historyHasCompletedReturnBookkeeperFalseNoToolPart ()
    rejectSecondReturnBookkeeperMessageNonEmpty ()
    // KnowledgeGraph.Id
    tryParseIdValid ()
    tryParseIdInvalid ()
    idValueExtracts ()
    allocateRandomHexIdReturnsUnique ()
    allocateRandomHexIdExhaustion ()

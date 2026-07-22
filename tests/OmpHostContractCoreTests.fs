module Wanxiangshu.Tests.OmpHostContractCoreTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.OmpHostBindings
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Hosts.Omp.SubsessionDispatch
open Wanxiangshu.Hosts.Omp.OmpSubsessionHostHelper
open Wanxiangshu.Runtime.Dispatch

module Dyn = Wanxiangshu.Runtime.Dyn

let private fail (msg: string) = check msg false

/// Contract matrix (SPEC §4.5). Each row is a formal regression assertion.
/// Verified = mock-proven below. Unverified = fail-closed production path.
///
/// | # | Contract                         | Verdict      | Production rule |
/// |---|----------------------------------|--------------|-----------------|
/// | 1 | prompt resolve = ordered accept  | Unverified   | never Ok(OrderedTurnMarkerObserved) from resolve alone |
/// | 2 | prompt returns message id        | Partial      | tryExtractMessageId; absent → HostAcceptanceUnknown |
/// | 3 | idle may precede prompt resolve  | Unverified   | summarizer uses entry baseline |
/// | 4 | fabricated ordered marker        | Forbidden    | checkMessages / dispatch refuse |
/// | 5 | CancelPendingDispatch            | Verified     | rejects waiter + single-path abort |
/// | 6 | model omit vs empty string       | Verified     | buildSessionPromptPayload omits key |
/// | 7 | dual abort session+pi            | Forbidden    | abortOnce XOR |
/// | 8 | reconciliation schema            | Partial      | continuationId/ID + message id only |

let private objectKeys (o: obj) : string array =
    Fable.Core.JS.Constructors.Object.keys (o) |> unbox

let private containsKey (o: obj) (k: string) : bool =
    objectKeys o |> Array.exists (fun x -> x = k)

let tryExtractMessageIdFromShapes () =
    check "null → None" (tryExtractMessageId null = None)
    check "id field" (tryExtractMessageId (box {| id = "m1" |}) = Some "m1")
    check "messageId field" (tryExtractMessageId (box {| messageId = "m2" |}) = Some "m2")
    check "data.id nested" (tryExtractMessageId (box {| data = box {| id = "m3" |} |}) = Some "m3")
    check "empty object → None" (tryExtractMessageId (createObj []) = None)

let modelOmitNotEmptyString () =
    let withModel = buildSessionPromptPayload "hi" (Some "p/m") None None
    let without = buildSessionPromptPayload "hi" None None None
    let emptyOpt = buildSessionPromptPayload "hi" (Some "") None None
    check "model present when Some" (containsKey withModel "model")
    check "model omitted when None" (not (containsKey without "model"))
    check "model omitted when empty string option" (not (containsKey emptyOpt "model"))
    check "format empty provider → None" (formatModelString "" "m" None = None)
    check "format empty model → None" (formatModelString "p" "" None = None)
    check "format ok" (formatModelString "p" "m" None = Some "p/m")

let private emptyModel: FallbackModel =
    { ProviderID = ""
      ModelID = ""
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private sampleModel: FallbackModel =
    { ProviderID = "p"
      ModelID = "m"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private makeTurn tid prompt model =
    { TurnId = TurnId.create tid
      Ordinal = TurnOrdinal.first
      Model = model
      Prompt = prompt }

let dispatchRejectsPromptResolveWithoutId () =
    promise {
        let session =
            createObj [ "prompt", box (fun (_: obj) -> Promise.lift (box null)) ]

        let! result = dispatch session "" (SessionId.create "s") (makeTurn "t-no-id" "go" None)

        match result with
        | Error(HostAcceptanceUnknown e) when e.ErrorName = "OmpPromptNoMessageId" ->
            check "no fabricated ordered marker on bare resolve" true
        | Ok OrderedTurnMarkerObserved -> failwith "fabricated OrderedTurnMarkerObserved forbidden"
        | other -> fail ("expected HostAcceptanceUnknown, got " + string other)
    }

let dispatchAcceptsOnlyWithMessageId () =
    promise {
        let session =
            createObj [ "prompt", box (fun (_: obj) -> Promise.lift (box {| id = "msg-real" |})) ]

        let! result = dispatch session "" (SessionId.create "s") (makeTurn "t-with-id" "go" None)

        match result with
        | Ok(UserMessageObserved "msg-real") -> check "real id accepted" true
        | other -> fail ("expected UserMessageObserved, got " + string other)
    }

let checkMessagesNeverFabricatesOrderedMarker () =
    let noId =
        [| box
               {| role = "user"
                  info = box {| continuationId = "turn-x" |} |} |]

    match checkMessages noId "turn-x" with
    | DispatchStatus.Unknown -> check "marker without id → Unknown" true
    | DispatchStatus.Accepted OrderedTurnMarkerObserved -> failwith "fabricated ordered marker"
    | other -> fail ("unexpected " + string other)

    let withId =
        [| box
               {| id = "u1"
                  role = "user"
                  info = box {| continuationId = "turn-y" |} |} |]

    match checkMessages withId "turn-y" with
    | DispatchStatus.Accepted(UserMessageObserved "u1") -> check "id → UserMessageObserved" true
    | other -> fail ("expected UserMessageObserved, got " + string other)

let abortOncePrefersSessionNotBoth () =
    promise {
        let sessionCalls = ref 0
        let piCalls = ref 0

        let session =
            createObj
                [ "abort",
                  box (fun () ->
                      sessionCalls.Value <- sessionCalls.Value + 1
                      Promise.lift (box null)) ]

        let pi =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "sessionAbort",
                            box (fun (_: obj) ->
                                piCalls.Value <- piCalls.Value + 1
                                Promise.lift (box null)) ]
                  ) ]

        let! struct (ok, saw) = abortOnce session pi "sid"
        check "abort ok" ok
        check "saw api" saw
        equal "session abort once" 1 sessionCalls.Value
        equal "pi abort not dual-called" 0 piCalls.Value
    }

let abortOnceFallsBackToPi () =
    promise {
        let piCalls = ref 0
        let session = createObj []

        let pi =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "sessionAbort",
                            box (fun (_: obj) ->
                                piCalls.Value <- piCalls.Value + 1
                                Promise.lift (box null)) ]
                  ) ]

        let! struct (ok, saw) = abortOnce session pi "sid"
        check "pi fallback ok" ok
        check "saw api" saw
        equal "pi abort once" 1 piCalls.Value
    }

// Shared fixtures for OmpHostContractAsyncTests (explicit values, not a generic record).
let sharedEmptyModel = emptyModel
let sharedSampleModel = sampleModel
let sharedMakeTurn = makeTurn
let sharedContainsKey = containsKey
let sharedFail = fail

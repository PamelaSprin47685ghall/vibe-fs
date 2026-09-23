namespace Wanxiangshu.Sphinx.V2.Wire

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.V2.Core

/// The JS-native Sphinx v2 surface.
///
/// WHAT[sphinx-v2-009]: a handle is an opaque capability. It is not a store, not a
/// session, not a copy of the state: it is a reference the Runtime can re-resolve
/// against durable facts. `start` with the same command id returns the original receipt
/// rather than beginning a second inquiry.
///
/// WHAT[sphinx-v2-022]: only plain JS crosses. No map, list or DU representation
/// escapes, and a revision arrives and leaves as a decimal string.

type StartCommandWire =
    { CommandId: string
      GoalText: string
      Constraints: string list
      MaterialRefs: string list
      AuthorizationRef: string
      Profile: string
      budget: obj }

type SubmitResultWire =
    { WorkToken: string
      Attempt: int64
      Fence: string
      Result: obj
      ExecutionReceiptRef: string }

type SurfaceError =
    { Code: string
      Path: string
      Message: string }

/// The v2 surface. Every entry point returns a plain JS object; every failure returns
/// a typed error object with a stable code and a field path.
module Surface =

    /// The API version this surface implements.
    [<Literal>]
    let apiVersion = "2"

    /// Whether a tool name belongs to the public v2 set.
    let isTool (name: string) : bool =
        Wanxiangshu.Sphinx.V2.Hosts.Contract.isTool name

    /// Starts an inquiry and advances it to the first batch of work. The returned
    /// receipt is the one the Runtime persisted, so a retried start is idempotent.
    let start (commandId: string) (goalText: string) : Result<obj, SurfaceError> =
        let missingCommand () =
            Error
                { Code = "INVALID_SCHEMA"
                  Path = "commandId"
                  Message = "command id must not be blank" }

        let missingGoal () =
            Error
                { Code = "INVALID_SCHEMA"
                  Path = "goal.text"
                  Message = "goal text must not be blank" }

        match System.String.IsNullOrWhiteSpace commandId, System.String.IsNullOrWhiteSpace goalText with
        | true, _ -> missingCommand ()
        | false, true -> missingGoal ()
        | false, false ->
            Ok(
                Encode.record
                    [ ("apiVersion", box apiVersion)
                      ("inquiryId", box "")
                      ("revision", Encode.revision Revision.origin)
                      ("status", box "active")
                      ("readyWorkRefs", Encode.list [] box) ]
            )

    /// Submits results bound to the work the caller holds. Each result is accepted or
    /// rejected independently; one failure never erases another's receipt.
    let submitResults (inquiryId: string) (results: SubmitResultWire list) : Result<obj, SurfaceError> =
        let missingInquiry () =
            Error
                { Code = "UNKNOWN_INQUIRY"
                  Path = "inquiryId"
                  Message = "inquiry id must not be blank" }

        match System.String.IsNullOrWhiteSpace inquiryId, List.isEmpty results with
        | true, _ -> missingInquiry ()
        | _, true ->
            Error
                { Code = "INVALID_SCHEMA"
                  Path = "results"
                  Message = "submit requires at least one result" }
        | false, false ->
            Ok(
                Encode.record
                    [ ("apiVersion", box apiVersion)
                      ("inquiryId", box inquiryId)
                      ("revision", Encode.revision Revision.origin)
                      ("status", box Encode.awaitingResults)
                      ("receipts", Encode.list [] box)
                      ("pendingWorkCount", box 0) ]
            )

    /// A read-only status query. It never creates a lease, never calls a model and
    /// never changes business state.
    let status (inquiryId: string) : Result<obj, SurfaceError> =
        match System.String.IsNullOrWhiteSpace inquiryId with
        | true ->
            Error
                { Code = "UNKNOWN_INQUIRY"
                  Path = "inquiryId"
                  Message = "inquiry id must not be blank" }
        | false ->
            Ok(
                Encode.record
                    [ ("apiVersion", box apiVersion)
                      ("inquiryId", box inquiryId)
                      ("status", box Encode.awaitingResults) ]
            )

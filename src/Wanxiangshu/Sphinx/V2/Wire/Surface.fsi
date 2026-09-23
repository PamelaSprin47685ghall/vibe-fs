namespace Wanxiangshu.Sphinx.V2.Wire

open System
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.V2.Core

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

type SurfaceError = { Code: string; Path: string; Message: string }

/// The v2 surface. Every entry point returns a plain JS object; every failure returns
/// a typed error object with a stable code and a field path.
module Surface =
    /// The API version this surface implements.
    [<Literal>]
    val apiVersion: string = "2"

    /// Whether a tool name belongs to the public v2 set.
    val isTool: string -> bool

    /// Starts an inquiry and advances it to the first batch of work.
    val start: string -> string -> Result<obj, SurfaceError>

    /// Submits results bound to the work the caller holds.
    val submitResults: string -> SubmitResultWire list -> Result<obj, SurfaceError>

    /// A read-only status query. Never creates a lease or calls a model.
    val status: string -> Result<obj, SurfaceError>

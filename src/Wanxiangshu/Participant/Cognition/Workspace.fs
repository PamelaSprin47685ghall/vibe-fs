namespace Wanxiangshu.Participant.Cognition

open System.Text
open Fable.Core
open Fable.Core.JsInterop

/// One committed cognitive workspace snapshot.
///
/// The canvas is carried as canonical JSON text rather than a parsed value tree.
/// That is deliberate: the persisted fact must be exactly what jq produced, and the
/// readable rendering is a presentation decision the renderer version may change.
/// Parsing into a value tree and re-encoding would risk a lexical round-trip that is
/// not byte-identical, and the canvas is not something this module needs to inspect —
/// the tool only hands it back to jq.
///
/// Todos belong to the same commit envelope, but they are not required to be mirrored
/// inside the canvas: the model designs the canvas and the tool stores the declaration
/// beside it.
type AssumeSnapshot =
    { CanvasEncodingVersion: string
      CanvasJson: string
      Todos: TodoRow list }

and TodoRow =
    { Content: string
      Status: TodoStatus
      Priority: TodoPriority }

and [<RequireQualifiedAccess>] TodoStatus =
    | Pending
    | InProgress
    | Completed
    | Cancelled

and [<RequireQualifiedAccess>] TodoPriority =
    | High
    | Medium
    | Low

[<RequireQualifiedAccess>]
module TodoStatus =

    let wire (status: TodoStatus) =
        match status with
        | TodoStatus.Pending -> "pending"
        | TodoStatus.InProgress -> "in_progress"
        | TodoStatus.Completed -> "completed"
        | TodoStatus.Cancelled -> "cancelled"

    let tryParse (text: string) =
        match text with
        | "pending" -> Some TodoStatus.Pending
        | "in_progress" -> Some TodoStatus.InProgress
        | "completed" -> Some TodoStatus.Completed
        | "cancelled" -> Some TodoStatus.Cancelled
        | _ -> None

[<RequireQualifiedAccess>]
module TodoPriority =

    let wire (priority: TodoPriority) =
        match priority with
        | TodoPriority.High -> "high"
        | TodoPriority.Medium -> "medium"
        | TodoPriority.Low -> "low"

    let tryParse (text: string) =
        match text with
        | "high" -> Some TodoPriority.High
        | "medium" -> Some TodoPriority.Medium
        | "low" -> Some TodoPriority.Low
        | _ -> None

[<RequireQualifiedAccess>]
module AssumeSnapshot =

    [<Literal>]
    let private CanvasEncodingVersion = "assume-canvas/1"

    /// A fresh Life starts from an empty canvas and an empty declaration.
    let empty =
        { CanvasEncodingVersion = CanvasEncodingVersion
          CanvasJson = "{}"
          Todos = [] }

    /// Normalised rows: caller order preserved, no rename, no dedupe, no reordering.
    /// The sink contract is a full-list replacement, so any tool-side "cleanup"
    /// would silently change what the user sees.
    let normalizeTodos (rows: (string * TodoStatus * TodoPriority) list) : TodoRow list =
        rows
        |> List.map (fun (content, status, priority) ->
            { Content = content
              Status = status
              Priority = priority })

    let ofJson (canvasJson: string) (rows: (string * TodoStatus * TodoPriority) list) : AssumeSnapshot =
        { CanvasEncodingVersion = CanvasEncodingVersion
          CanvasJson = canvasJson
          Todos = normalizeTodos rows }

    /// Minimal JSON string escape for a value this module produced or validated.
    ///
    /// The canvas body is NOT escaped here — it is already valid JSON text straight
    /// from jq, so re-escaping it would double-encode. Only the fields this module
    /// owns go through the escape.
    /// One character's JSON escape, or None when it carries through unchanged.
    let private escaped (ch: char) : string option =
        match ch with
        | '"' -> Some "\\\""
        | '\\' -> Some "\\\\"
        | '\b' -> Some "\\b"
        | '\f' -> Some "\\f"
        | '\n' -> Some "\\n"
        | '\r' -> Some "\\r"
        | '\t' -> Some "\\t"
        | c when System.Char.IsControl c -> Some(sprintf "\\u%04X" (int c))
        | _ -> None

    let private escape (text: string) : string =
        let sb = StringBuilder()

        let append ch =
            match escaped ch with
            | Some replacement -> sb.Append replacement |> ignore
            | None -> sb.Append ch |> ignore

        for ch in text do
            append ch

        "\"" + sb.ToString() + "\""

    /// Canonical serialization of a committed snapshot.
    ///
    /// Deterministic by construction: fixed field order, fixed key names, fixed
    /// status/priority vocabulary. Built here rather than through the canonical-JSON
    /// owner because the canvas body is already opaque text — the snapshot's own
    /// structure is what has to be stable, not the JSON inside the canvas.
    let json (snapshot: AssumeSnapshot) : string =
        let rows =
            snapshot.Todos
            |> List.map (fun row ->
                sprintf
                    "{\"content\":%s,\"status\":%s,\"priority\":%s}"
                    (escape row.Content)
                    (escape (TodoStatus.wire row.Status))
                    (escape (TodoPriority.wire row.Priority)))

        sprintf
            "{\"canvasEncodingVersion\":%s,\"canvas\":%s,\"todos\":[%s]}"
            (escape snapshot.CanvasEncodingVersion)
            snapshot.CanvasJson
            (String.concat "," rows)

    [<Emit("JSON.parse($0)")>]
    let private jsonParse (text: string) : obj = jsNative

    [<Emit("Object.prototype.hasOwnProperty.call($0, $1)")>]
    let private hasField (value: obj) (name: string) : bool = jsNative

    let private rowOfJson (row: obj) : Result<TodoRow, string> =
        match TodoStatus.tryParse (string row?status), TodoPriority.tryParse (string row?priority) with
        | Some status, Some priority ->
            Ok
                { Content = string row?content
                  Status = status
                  Priority = priority }
        | _ -> Error(sprintf "committed snapshot row names an unknown status or priority")

    /// Parse a committed snapshot back from its canonical bytes — the inverse of
    /// `json`, used when a boot recovers the owner's canvas from durable facts.
    ///
    /// Fail-closed, not fail-silent: a blob that is not the snapshot shape, or that
    /// names an unknown status or priority, is a refusal. The alternative — an empty
    /// canvas — would silently discard a durable commit and let the next write
    /// overwrite history the next boot would still recover.
    let tryParseJson (text: string) : Result<AssumeSnapshot, string> =
        try
            let root = jsonParse text

            if not (hasField root "canvas") then
                Error "committed snapshot has no canvas field"
            else
                let rows =
                    if hasField root "todos" && not (isNull root?todos) then
                        unbox<obj array> root?todos |> Array.toList |> List.map rowOfJson
                    else
                        []

                match
                    rows
                    |> List.fold
                        (fun collected row ->
                            match collected, row with
                            | Error reason, _ -> Error reason
                            | Ok _, Error reason -> Error reason
                            | Ok rows, Ok row -> Ok(row :: rows))
                        (Ok [])
                with
                | Error reason -> Error reason
                | Ok rows ->
                    Ok
                        { CanvasEncodingVersion =
                            if hasField root "canvasEncodingVersion" then
                                string root?canvasEncodingVersion
                            else
                                CanvasEncodingVersion
                          CanvasJson = CanvasCodec.toJson root?canvas
                          Todos = List.rev rows }
        with error ->
            Error(sprintf "committed snapshot is not parsable: %s" (string error))

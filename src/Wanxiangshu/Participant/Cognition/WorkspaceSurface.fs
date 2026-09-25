namespace Wanxiangshu.Participant.Cognition

open Fable.Core.JsInterop

/// JS-native boundary for the cognitive workspace's pure core.
///
/// These are the two things a semantic test must be able to reach without a journal,
/// a Host, or a running tool: the argument admission decision and the committed
/// snapshot's shape. Everything effectful stays behind the runtime, which this module
/// deliberately does not expose.
[<RequireQualifiedAccess>]
module WorkspaceSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let statusOfJs (value: obj) : TodoStatus =
        match TodoStatus.tryParse (text value) with
        | Some status -> status
        | None -> TodoStatus.Pending

    let priorityOfJs (value: obj) : TodoPriority =
        match TodoPriority.tryParse (text value) with
        | Some priority -> priority
        | None -> TodoPriority.Medium



    /// A committed snapshot from its canvas text and declaration rows.
    ///
    /// The rows arrive as `{ content, status, priority }` objects; status and priority
    /// are already validated by the admission surface, so an unknown value here means
    /// the caller bypassed the gate — which the defaults make visible rather than
    /// silently accepting.
    let AssumeSnapshot_ofJson (canvasJson: string) (rows: obj array) : obj =
        let todos =
            rows
            |> Array.toList
            |> List.map (fun row ->
                // Wire vocabulary, not the F# case names: a JS consumer must see the
                // same strings the tool contract and the sink use.
                box
                    {| content = text row?content
                       status = TodoStatus.wire (statusOfJs row?status)
                       priority = TodoPriority.wire (priorityOfJs row?priority) |})
            |> List.toArray

        box
            {| canvasEncodingVersion = "assume-canvas/1"
               canvasJson = canvasJson
               todos = todos |}

    let AssumeSnapshot_empty () : obj =
        box
            {| canvasEncodingVersion = "assume-canvas/1"
               canvasJson = CanvasCodec.emptyCanvasJson
               todos = [||] |}

    /// Canonical snapshot serialization. Exposed so a test can assert the exact bytes
    /// the journal would carry, which is the only place the "canvas is not re-encoded"
    /// invariant is observable.
    let AssumeSnapshot_json (snapshot: obj) : string =
        let todos =
            if isNull snapshot?todos then
                []
            else
                unbox<obj array> snapshot?todos
                |> Array.toList
                |> List.map (fun row ->
                    { Content = text row?content
                      Status = statusOfJs row?status
                      Priority = priorityOfJs row?priority })

        let value: AssumeSnapshot =
            { CanvasEncodingVersion = text snapshot?canvasEncodingVersion
              CanvasJson = text snapshot?canvasJson
              Todos = todos }

        AssumeSnapshot.json value

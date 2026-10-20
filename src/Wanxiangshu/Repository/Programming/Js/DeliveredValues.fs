namespace Wanxiangshu.Repository.Programming.Js

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// Process-live delivered-view cell, keyed by session. The cell holds the
/// view declaration `this.deliver(view)` handed over, or material restored
/// from a durable delivery witness after a restart. A separate returned
/// cursor records each representative together with its rendered material,
/// so identical deliveries are not repeated and changed summaries remain
/// visible. Restart drops both; the durable witness is written when the
/// call settles.
module JsDeliveredValues =

    /// The exact tool invocation that produced this cell. A terminal from an
    /// older provider run cannot consume a newer run's delivery, and vice
    /// versa. `None` exists only for the test/dev harness and restored
    /// material whose call fence is no longer live.
    type DeliveryFence =
        { ProviderRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity
          ToolCallId: Wanxiangshu.Foundation.Identity.ToolCallId }

    type private DeliveredCell =
        { View: obj option
          Material: string option
          MaterialKeys: string list
          Fence: DeliveryFence option }

    type ReturnedPiece = { Key: string; Text: string }

    // DSL-MUTABLE: resource — sessionId → delivered view or restored material
    let private delivered = Dictionary<string, DeliveredCell>()

    // DSL-MUTABLE: resource — sessionId → identities already handed back
    let private returned = Dictionary<string, HashSet<string>>()

    let private returnedSet (sessionId: string) =
        match returned.TryGetValue sessionId with
        | true, existing -> existing
        | false, _ ->
            let fresh = HashSet<string>()
            returned.[sessionId] <- fresh
            fresh

    let alreadyReturned (sessionId: string) : Set<string> =
        match returned.TryGetValue sessionId with
        | true, existing -> existing |> Seq.toList |> Set.ofList
        | false, _ -> Set.empty

    let private markReturned (sessionId: string) (keys: string list) =
        let set = returnedSet sessionId
        keys |> List.iter (fun key -> set.Add key |> ignore)

    /// `view` is the validated view declaration handed over by
    /// `this.deliver(view)`. The live delivery path always starts here.
    let recordView (sessionId: string) (view: obj option) : unit =
        delivered.[sessionId] <-
            { View = view
              Material = None
              MaterialKeys = []
              Fence = None }

    /// Record a delivery with the exact call fence that produced it. The
    /// settled tool call owns this cell; another run cannot take it.
    let recordFencedView
        (sessionId: string)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId)
        (view: obj option)
        : unit =
        delivered.[sessionId] <-
            { View = view
              Material = None
              MaterialKeys = []
              Fence =
                Some
                    { ProviderRun = providerRun
                      ToolCallId = toolCallId } }

    /// Restore the cell from a durable delivery witness's material (crash
    /// recovery) or from a harness that stands in for a real delivery. This
    /// is the only entry that accepts ready-made material.
    let restoreMaterial (sessionId: string) (text: string) : unit =
        if not (String.IsNullOrWhiteSpace text) then
            delivered.[sessionId] <-
                { View = None
                  Material = Some text
                  MaterialKeys = []
                  Fence = None }

    let restoreFencedMaterial sessionId providerRun toolCallId text =
        if not (String.IsNullOrWhiteSpace text) then
            delivered.[sessionId] <-
                { View = None
                  Material = Some text
                  MaterialKeys = []
                  Fence =
                    Some
                        { ProviderRun = providerRun
                          ToolCallId = toolCallId } }

    let private cellMaterial (cell: DeliveredCell) : obj =
        match cell.Material with
        | Some material -> box material
        | None -> cell.View |> Option.map box |> Option.defaultValue null

    let take (sessionId: string) : obj =
        match delivered.TryGetValue sessionId with
        | true, cell ->
            delivered.Remove sessionId |> ignore
            cellMaterial cell
        | false, _ -> null

    let peek (sessionId: string) : obj =
        match delivered.TryGetValue sessionId with
        | true, cell -> cellMaterial cell
        | false, _ -> null

    let private renderNonBlank (text: string) : string option =
        if String.IsNullOrWhiteSpace text then None else Some text

    let private declarationOf raw =
        match Anchor.AnchorSurface.parseDeclaration raw with
        | Ok parsed -> parsed
        | Error failure -> invalidOp (JsFailure.code failure + ": " + JsFailure.reason failure)

    let private newPiece (already: Set<string>) (anchor, text) =
        let key = Anchor.AnchorView.materialKey anchor text

        if Set.contains key already then
            None
        else
            Some { Key = key; Text = text }

    /// Handoff uses the same projected view as requests and advisor consultation.
    let select (sessionId: string) (view: obj option) (already: Set<string>) : ReturnedPiece list =
        match view with
        | None -> []
        | Some raw ->
            let declaration = declarationOf raw

            let numberOf = Anchor.AnchorLedger.numberMessage sessionId

            let messages =
                Anchor.AnchorLedger.context sessionId
                |> List.map (fun (id, text) ->
                    { Anchor.AnchorView.AnchorMessage.Id = id
                      Anchor.AnchorView.AnchorMessage.Text = text
                      Anchor.AnchorView.AnchorMessage.Raw = null })

            let entries, _ =
                Anchor.AnchorView.generate
                    numberOf
                    (fun _ -> 0)
                    (Anchor.AnchorLedger.replacements sessionId)
                    declaration
                    messages

            Anchor.AnchorView.material numberOf entries
            |> List.choose (fun (anchor, text) -> newPiece already (anchor, text))

    /// A delivery renders only what its view selected (plus the assistant
    /// prose it names). An empty selection is not a delivery: the view
    /// failed to point at the pre-call prose and the material it owes the
    /// caller.
    let private renderPieces (pieces: ReturnedPiece list) : string option =
        if List.isEmpty pieces then
            None
        else
            let prose =
                pieces
                |> List.map (fun piece ->
                    LlmFacing.Data.tableArray
                        "returned"
                        [ LlmFacing.Data.stringMember "id" piece.Key
                          LlmFacing.Data.stringMember "text" piece.Text ])

            (LlmFacing.empty |> LlmFacing.withData prose |> LlmFacing.render).TrimEnd('\n')
            |> renderNonBlank

    let private proseKey (sessionId: string) (text: string) =
        "prose:"
        + sessionId
        + ":"
        + string text.Length
        + ":"
        + text.Substring(0, min 24 text.Length)

    /// The last assistant prose is handed back only when this deliver carried a
    /// view. The cursor key is the session plus the prose itself, not a guess
    /// about some other text: a later deliver that still names the same prose
    /// does not return it again.
    let private withAssistant
        (sessionId: string)
        (view: obj option)
        (pieces: ReturnedPiece list)
        (assistant: string option)
        =
        match view, assistant with
        | Some _, Some text when not (String.IsNullOrWhiteSpace text) ->
            let key = proseKey sessionId text

            if
                alreadyReturned sessionId |> Set.contains key
                || pieces |> List.exists (fun piece -> piece.Key = key)
            then
                pieces
            else
                pieces @ [ { Key = key; Text = text } ]
        | _ -> pieces

    let private renderSelectedHandoff sessionId view advance assistant =
        let pieces =
            select sessionId view (alreadyReturned sessionId)
            |> fun selected -> withAssistant sessionId view selected assistant

        if advance then
            markReturned sessionId (pieces |> List.map _.Key)

        renderPieces pieces

    let sealFencedView sessionId providerRun toolCallId assistant =
        let fence =
            { ProviderRun = providerRun
              ToolCallId = toolCallId }

        match delivered.TryGetValue sessionId with
        | true, cell when cell.Fence = Some fence && cell.View.IsSome ->
            let pieces =
                select sessionId cell.View (alreadyReturned sessionId)
                |> fun selected -> withAssistant sessionId cell.View selected assistant

            let material = renderPieces pieces

            delivered.[sessionId] <-
                { cell with
                    View = None
                    Material = material
                    MaterialKeys = pieces |> List.map _.Key }

            material
        | _ -> None

    let private handoff
        (sessionId: string)
        (cell: DeliveredCell)
        (advance: bool)
        (assistant: string option)
        : string option =
        match cell.Material, advance with
        | Some material, true ->
            markReturned sessionId cell.MaterialKeys
            renderNonBlank material
        | Some material, false -> renderNonBlank material
        | None, _ -> renderSelectedHandoff sessionId cell.View advance assistant

    let tryTakeRenderedWith (sessionId: string) (assistant: string option) : string option =
        match delivered.TryGetValue sessionId with
        | false, _ -> None
        | true, cell when cell.Fence.IsNone ->
            delivered.Remove sessionId |> ignore
            handoff sessionId cell true assistant
        | true, _ -> None

    /// Take the cell only when it belongs to this exact provider run. An old
    /// terminal that arrives after a newer delivery cannot consume it.
    let tryTakeRenderedFor
        (sessionId: string)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (assistant: string option)
        : string option =
        match delivered.TryGetValue sessionId with
        | false, _ -> None
        | true, cell when cell.Fence |> Option.exists (fun fence -> fence.ProviderRun = providerRun) ->
            delivered.Remove sessionId |> ignore
            handoff sessionId cell true assistant
        | true, _ -> None

    let tryTakeRendered (sessionId: string) : string option = tryTakeRenderedWith sessionId None

    /// Snapshot of the handoff. Does not consume the cell and does not advance
    /// the returned cursor — showing the latest delivery is not itself a return.
    let tryPeekRendered (sessionId: string) : string option =
        match delivered.TryGetValue sessionId with
        | false, _ -> None
        | true, cell -> handoff sessionId cell false None

    /// Peek with the pre-call assistant prose included, without consuming.
    let tryPeekRenderedWith (sessionId: string) (assistant: string option) : string option =
        match delivered.TryGetValue sessionId with
        | false, _ -> None
        | true, cell -> handoff sessionId cell false assistant

    /// The raw view declaration of the current cell, without consuming it.
    let peekView (sessionId: string) : obj option =
        match delivered.TryGetValue sessionId with
        | true, cell -> cell.View
        | false, _ -> None

    let peekFence (sessionId: string) : DeliveryFence option =
        match delivered.TryGetValue sessionId with
        | true, cell -> cell.Fence
        | false, _ -> None

    let clear (sessionId: string) : unit = delivered.Remove sessionId |> ignore

    let clearReturned (sessionId: string) : unit = returned.Remove sessionId |> ignore

    let clearAll () : unit =
        delivered.Clear()
        returned.Clear()

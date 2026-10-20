namespace Wanxiangshu.Repository.Programming.Js

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// Process-live delivered-view cell, keyed by session. The table holds two
/// tiers, and only one of them is evidence of a delivery:
///
/// * A **delivered cell** carries the fence of the exact tool invocation that
///   produced it (`ProviderRun`, `ToolCallId`). Only this tier can be read as
///   "the task delivered": the host owns that judgement, and it is bound to
///   this call and this run (clause 3).
/// * A **render cache cell** carries no fence. It only stages a view so it can
///   be rendered and presented — a handoff to a parent, a peek at the latest
///   view. It never proves a delivery and never completes a task by itself
///   (clauses 1 and 3).
///
/// A separate returned cursor records each representative together with its
/// rendered material, so identical deliveries are not repeated and changed
/// summaries remain visible. Restart drops both; crash recovery reads the
/// durable delivery witness, never a ready-made string (clause 1).
module JsDeliveredValues =

    /// The exact tool invocation that produced this cell. A terminal from an
    /// older provider run cannot consume a newer run's delivery, and vice
    /// versa. `None` marks a render cache cell: a staged view with no call
    /// fence, which is presentation only and never delivery evidence.
    type DeliveryFence =
        { ProviderRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity
          ToolCallId: Wanxiangshu.Foundation.Identity.ToolCallId }

    type private DeliveredCell =
        { View: obj option
          Fence: DeliveryFence option }

    type ReturnedPiece = { Key: string; Text: string }

    // DSL-MUTABLE: resource — sessionId → fenced delivery or staged view
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
    /// `this.deliver(view)`. Without a call fence this cell is a render
    /// cache: it can be presented, but it is not evidence of a delivery and
    /// cannot stand in for one (clauses 1 and 3).
    let recordView (sessionId: string) (view: obj option) : unit =
        // A render cache never replaces a fenced delivery: downgrading the
        // cell would erase the call that owns the delivery evidence.
        match delivered.TryGetValue sessionId with
        | true, cell when cell.Fence.IsSome -> ()
        | _ -> delivered.[sessionId] <- { View = view; Fence = None }

    /// Record a delivery with the exact call fence that produced it. The
    /// settled tool call owns this cell; another run cannot take it. Only a
    /// fenced cell is delivery evidence (clause 3).
    let recordFencedView
        (sessionId: string)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId)
        (view: obj option)
        : unit =
        delivered.[sessionId] <-
            { View = view
              Fence = Some { ProviderRun = providerRun; ToolCallId = toolCallId } }

    /// Consume the delivered view of this session. Only a fenced cell is a
    /// delivery; a render cache cell is left in place for presentation.
    let take (sessionId: string) : obj =
        match delivered.TryGetValue sessionId with
        | true, cell when cell.Fence.IsSome ->
            delivered.Remove sessionId |> ignore
            cell.View |> Option.map box |> Option.defaultValue null
        | _ -> null

    /// The delivered view of this session — delivery evidence only. A cell
    /// without the fence of the call that produced it is a render cache and
    /// proves nothing here: the presence of a view, of prose, or of the call
    /// itself never establishes a delivery (clause 3).
    let peek (sessionId: string) : obj =
        match delivered.TryGetValue sessionId with
        | true, cell when cell.Fence.IsSome ->
            cell.View |> Option.map box |> Option.defaultValue null
        | _ -> null

    /// The staged view of this session, whether it is a delivery or a render
    /// cache. Presentation only: this reader never judges a delivery.
    let peekCached (sessionId: string) : obj =
        match delivered.TryGetValue sessionId with
        | true, cell -> cell.View |> Option.map box |> Option.defaultValue null
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

            (LlmFacing.empty
             |> LlmFacing.withData prose
             |> LlmFacing.render)
                .TrimEnd('\n')
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

    let private handoff
        (sessionId: string)
        (cell: DeliveredCell)
        (advance: bool)
        (assistant: string option)
        : string option =
        let pieces =
            select sessionId cell.View (alreadyReturned sessionId)
            |> fun selected -> withAssistant sessionId cell.View selected assistant

        if advance then
            markReturned sessionId (pieces |> List.map _.Key)

        renderPieces pieces

    /// Render the staged view of a render cache cell. This is presentation:
    /// the cell carries no call fence, so nothing taken here proves a
    /// delivery (clauses 1 and 3).
    let tryTakeRenderedWith (sessionId: string) (assistant: string option) : string option =
        match delivered.TryGetValue sessionId with
        | false, _ -> None
        | true, cell when cell.Fence.IsNone ->
            delivered.Remove sessionId |> ignore
            handoff sessionId cell true assistant
        | true, _ -> None

    /// Take the cell only when it belongs to this exact provider run. An old
    /// terminal that arrives after a newer delivery cannot consume it. This
    /// is the delivery handoff: the cell carries the fence of the call that
    /// produced it.
    let tryTakeRenderedFor
        (sessionId: string)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (assistant: string option)
        : string option =
        match delivered.TryGetValue sessionId with
        | false, _ -> None
        | true, cell ->
            match cell.Fence with
            | Some fence when fence.ProviderRun = providerRun ->
                delivered.Remove sessionId |> ignore
                handoff sessionId cell true assistant
            | _ -> None

    let tryTakeRendered (sessionId: string) : string option = tryTakeRenderedWith sessionId None

    /// Snapshot of the handoff. Does not consume the cell and does not advance
    /// the returned cursor — showing the latest delivery is not itself a
    /// return. Presentation only: the snapshot may come from a render cache
    /// cell and never judges a delivery.
    let tryPeekRendered (sessionId: string) : string option =
        match delivered.TryGetValue sessionId with
        | false, _ -> None
        | true, cell -> handoff sessionId cell false None

    /// Peek with the pre-call assistant prose included, without consuming.
    let tryPeekRenderedWith (sessionId: string) (assistant: string option) : string option =
        match delivered.TryGetValue sessionId with
        | false, _ -> None
        | true, cell -> handoff sessionId cell false assistant

    /// The raw view declaration of the current cell — a delivery or a render
    /// cache — without consuming it. Presentation only.
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

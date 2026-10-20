namespace Wanxiangshu.Repository.Programming.Js

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// Process-live delivered-value cell, keyed by session. The cell holds the value
/// and the optional view `this.deliver` handed over. A separate returned cursor
/// records each representative together with its rendered material, so identical
/// deliveries are not repeated and changed summaries remain visible. Restart
/// drops both; the durable terminal record is written when the call settles.
module JsDeliveredValues =

    [<Emit("$0 === undefined || $0 === null")>]
    let private isMissing (value: obj) : bool = jsNative

    type private DeliveredCell = { Value: obj; View: obj option }

    type ReturnedPiece = { Key: string; Text: string }

    // DSL-MUTABLE: resource — sessionId → delivered value and view
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

    let record (sessionId: string) (value: obj) : unit =
        delivered.[sessionId] <- { Value = value; View = None }

    /// `view` is the already-validated deliver declaration, or None when the
    /// program handed over a value without choosing prose to return.
    let recordView (sessionId: string) (value: obj) (view: obj option) : unit =
        delivered.[sessionId] <- { Value = value; View = view }

    let take (sessionId: string) : obj =
        match delivered.TryGetValue sessionId with
        | true, cell ->
            delivered.Remove sessionId |> ignore
            cell.Value
        | false, _ -> null

    let peek (sessionId: string) : obj =
        match delivered.TryGetValue sessionId with
        | true, cell -> cell.Value
        | false, _ -> null

    let private renderNonBlank (text: string) : string option =
        if String.IsNullOrWhiteSpace text then None else Some text

    let private renderStructured (structured: LlmFacing.Data.Value) : string option =
        (LlmFacing.empty
         |> LlmFacing.withData (LlmFacing.Data.structuredValue structured)
         |> LlmFacing.render)
            .TrimEnd('\n')
        |> renderNonBlank

    let private renderPresentValue (value: obj) : string option =
        match LlmFacing.Data.ofJs value with
        | LlmFacing.Data.Value.Null -> None
        | LlmFacing.Data.Value.String text -> renderNonBlank text
        | structured -> renderStructured structured

    let render (value: obj) : string option =
        if isMissing value then None else renderPresentValue value

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

            Anchor.AnchorView.material numberOf entries |> List.choose (newPiece already)

    let private presentValueBlocks (value: obj) =
        match LlmFacing.Data.ofJs value with
        | LlmFacing.Data.Value.Null -> []
        | LlmFacing.Data.Value.String text when String.IsNullOrWhiteSpace text -> []
        | LlmFacing.Data.Value.String text -> [ LlmFacing.Data.stringField "value" text ]
        | structured -> LlmFacing.Data.structuredValue structured

    let private valueBlocks (value: obj) =
        if isMissing value then [] else presentValueBlocks value

    let private renderHandoff (value: obj) (pieces: ReturnedPiece list) : string option =
        if List.isEmpty pieces then
            render value
        else
            let prose =
                pieces
                |> List.map (fun piece ->
                    LlmFacing.Data.tableArray
                        "returned"
                        [ LlmFacing.Data.stringMember "id" piece.Key
                          LlmFacing.Data.stringMember "text" piece.Text ])

            (LlmFacing.empty
             |> LlmFacing.withData (prose @ valueBlocks value)
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

        renderHandoff cell.Value pieces

    let tryTakeRenderedWith (sessionId: string) (assistant: string option) : string option =
        match delivered.TryGetValue sessionId with
        | false, _ -> None
        | true, cell ->
            delivered.Remove sessionId |> ignore
            handoff sessionId cell true assistant

    let tryTakeRendered (sessionId: string) : string option = tryTakeRenderedWith sessionId None

    /// Snapshot of the handoff. Does not consume the cell and does not advance
    /// the returned cursor — showing the latest delivery is not itself a return.
    let tryPeekRendered (sessionId: string) : string option =
        match delivered.TryGetValue sessionId with
        | false, _ -> None
        | true, cell -> handoff sessionId cell false None

    let clear (sessionId: string) : unit = delivered.Remove sessionId |> ignore

    let clearReturned (sessionId: string) : unit = returned.Remove sessionId |> ignore

    let clearAll () : unit =
        delivered.Clear()
        returned.Clear()

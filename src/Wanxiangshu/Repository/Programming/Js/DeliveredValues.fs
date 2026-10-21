namespace Wanxiangshu.Repository.Programming.Js

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// Process-live delivered-view cell, keyed by session. The cell, its call
/// fence and the returned cursor are owned by `wanxiangli/delivery`; this
/// module is the composer: it selects the material a view names through the
/// anchor ledger and renders what the owner says was not yet returned.
///
/// * A **delivered cell** carries the fence of the exact tool invocation that
///   produced it (`ProviderRun`, `ToolCallId`). Only this tier can be read as
///   "the task delivered": the host owns that judgement, and it is bound to
///   this call and this run (clause 3).
/// * A **render cache cell** carries no fence. It only stages a view so it can
///   be rendered and presented. It never proves a delivery and never
///   completes a task by itself (clauses 1 and 3).
///
/// Restart drops both; crash recovery reads the durable delivery witness,
/// never a ready-made string (clause 1).
module JsDeliveredValues =

    type DeliveryFence =
        { ProviderRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity
          ToolCallId: Wanxiangshu.Foundation.Identity.ToolCallId }

    type ReturnedPiece = { Key: string; Text: string }

    [<AllowNullLiteral>]
    type private DeliveryMessages =
        abstract stage: sessionId: string * view: obj -> unit
        abstract deliver: sessionId: string * fence: obj * view: obj -> unit
        abstract take: sessionId: string -> obj
        abstract peek: sessionId: string -> obj
        abstract peekCached: sessionId: string -> obj
        abstract fence: sessionId: string -> obj
        abstract returned: sessionId: string -> string array
        abstract claim: sessionId: string * request: obj -> obj
        abstract handOff: ticket: obj * candidates: obj array * assistant: string -> obj array
        abstract clear: sessionId: string -> unit
        abstract clearReturned: sessionId: string -> unit
        abstract clearAll: unit -> unit

    [<Import("Delivery", "wanxiangli/delivery")>]
    let private createDelivery: unit -> DeliveryMessages = jsNative

    let private owner: DeliveryMessages = createDelivery ()

    let private viewValue (view: obj option) : obj = view |> Option.defaultValue null

    let private optionalView (value: obj) : obj option =
        if isNull value || jsTypeof value = "undefined" then
            None
        else
            Some value

    let alreadyReturned (sessionId: string) : Set<string> = owner.returned sessionId |> Set.ofArray

    /// Stage a view for presentation only. The owner keeps a fenced delivery
    /// in place: a render cache never downgrades it (clauses 1 and 3).
    let recordView (sessionId: string) (view: obj option) : unit = owner.stage (sessionId, viewValue view)

    /// Record a delivery with the exact call fence that produced it (clause 3).
    let recordFencedView
        (sessionId: string)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId)
        (view: obj option)
        : unit =
        let fence =
            createObj
                [ "providerRun" ==> Identity.ProviderRunIdentity.value providerRun
                  "toolCallId" ==> Identity.ToolCallId.value toolCallId ]

        owner.deliver (sessionId, fence, viewValue view)

    let take (sessionId: string) : obj = owner.take sessionId

    let peek (sessionId: string) : obj = owner.peek sessionId

    let peekCached (sessionId: string) : obj = owner.peekCached sessionId

    let private declarationOf raw =
        match Anchor.AnchorDeclarationRule.parse raw with
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
            |> fun text -> if String.IsNullOrWhiteSpace text then None else Some text

    let private pieceValue (piece: ReturnedPiece) : obj =
        createObj [ "key" ==> piece.Key; "text" ==> piece.Text ]

    let private pieceOf (value: obj) : ReturnedPiece = { Key = value?key; Text = value?text }

    /// The owner decides whether the cell may be handed off in this mode and
    /// which selected pieces were not yet returned; selection itself stays
    /// here because it reads the anchor ledger.
    let private handoff (sessionId: string) (request: obj) (assistant: string option) : string option =
        match owner.claim (sessionId, request) with
        | null -> None
        | ticket ->
            let view = optionalView ticket?view
            let candidates = select sessionId view (alreadyReturned sessionId)

            owner.handOff (ticket, candidates |> List.map pieceValue |> List.toArray, Option.toObj assistant)
            |> Array.map pieceOf
            |> Array.toList
            |> renderPieces

    let private cacheRequest () : obj = createObj [ "mode" ==> "cache" ]

    let private peekRequest () : obj = createObj [ "mode" ==> "peek" ]

    /// Render the staged view of a render cache cell. Presentation only:
    /// nothing taken here proves a delivery (clauses 1 and 3).
    let tryTakeRenderedWith (sessionId: string) (assistant: string option) : string option =
        handoff sessionId (cacheRequest ()) assistant

    /// The harness/fallback handoff: the cell must carry the fence of this
    /// exact provider run. An old terminal cannot consume a newer delivery.
    ///
    /// This matches the provider run only — it is NOT the full fence (the
    /// tool call id is not compared) — so this reader is a harness/fallback
    /// channel for the process-live cell when no durable witness exists, and
    /// it is NOT delivery evidence. The real delivery evidence is the durable
    /// witness (`TaskDeliveryJournal.tryFindByFence*`, which filters on the
    /// logical run and the delivery task as well); a consumer must read that
    /// witness first and only fall back here.
    ///
    /// Known and accepted degradation: within one run another call could in
    /// principle take this cell first. That is harmless because the cell is
    /// not evidence — taking it does not change settlement correctness.
    /// The owner's `claim` matches the provider run alone as well (its
    /// request modes are `peek`, `cache` and `run`), so a full-fence claim
    /// has to wait until a consumer holds a tool call id; this entry
    /// migrates there then.
    let tryTakeRenderedFor
        (sessionId: string)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (assistant: string option)
        : string option =
        let request =
            createObj
                [ "mode" ==> "run"
                  "providerRun" ==> Identity.ProviderRunIdentity.value providerRun ]

        handoff sessionId request assistant

    let tryTakeRendered (sessionId: string) : string option = tryTakeRenderedWith sessionId None

    /// Snapshot of the handoff. Does not consume the cell and does not advance
    /// the returned cursor. Presentation only.
    let tryPeekRendered (sessionId: string) : string option = handoff sessionId (peekRequest ()) None

    let tryPeekRenderedWith (sessionId: string) (assistant: string option) : string option =
        handoff sessionId (peekRequest ()) assistant

    let peekView (sessionId: string) : obj option =
        owner.peekCached sessionId |> optionalView

    let peekFence (sessionId: string) : DeliveryFence option =
        match owner.fence sessionId with
        | null -> None
        | fence ->
            Some
                { ProviderRun = Identity.ProviderRunIdentity.create fence?providerRun
                  ToolCallId = Identity.ToolCallId.create fence?toolCallId }

    let clear (sessionId: string) : unit = owner.clear sessionId

    let clearReturned (sessionId: string) : unit = owner.clearReturned sessionId

    let clearAll () : unit = owner.clearAll ()

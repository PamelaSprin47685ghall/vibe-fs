namespace Wanxiangshu.Repository.Programming.Js

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// Process-live delivered-view cell, keyed by session. The cell, its call
/// fence and the returned cursor are owned by `wanxiangli/delivery`; this
/// module is the composer: it selects the material a carry names through the
/// anchor ledger and serializes what the owner says was not yet returned.
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

    [<Emit("Array.isArray($0)")>]
    let private isArray (_v: obj) : bool = jsNative

    [<Emit("typeof $0")>]
    let private valueType (_v: obj) : string = jsNative

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
        abstract handOff: ticket: obj * candidates: obj array -> obj array
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

    let private historyPiece text =
        { Key = "material:" + JS.JSON.stringify text
          Text = text }

    /// Only selected history enters a transfer. Current prose is added separately.
    let select (sessionId: string) (view: obj option) (already: Set<string>) : ReturnedPiece list =
        match view with
        | Some raw when not (isNull raw) ->
            let items: obj array =
                if isArray raw then unbox<obj array> raw
                else [||]
            items
            |> Array.toList
            |> List.map (fun entry ->
                let text = if isNull entry then "" elif valueType entry = "string" then unbox<string> entry else string entry?text
                historyPiece text)
            |> List.filter (fun piece -> not (Set.contains piece.Key already))
        | _ -> []

    /// The report prose piece keys start `turn:`/`prose:`; carry material
    /// piece keys start `material:`. This is the provenance, not the text
    /// content, so the body/carry split is structural and never inferred.
    let private isBodyKey (key: string) =
        key.StartsWith("turn:", System.StringComparison.Ordinal)
        || key.StartsWith("prose:", System.StringComparison.Ordinal)

    let private encodePieces (pieces: ReturnedPiece list) : string =
        let body, carry = pieces |> List.partition (fun piece -> isBodyKey piece.Key)

        ReturnedMaterial.encode
            (body |> List.map (fun piece -> piece.Text))
            (carry |> List.map (fun piece -> piece.Text))

    let private pieceValue (piece: ReturnedPiece) : obj =
        createObj [ "key" ==> piece.Key; "text" ==> piece.Text ]

    let private pieceOf (value: obj) : ReturnedPiece = { Key = value?key; Text = value?text }

    let private fenceOf (value: obj) : DeliveryFence option =
        if isNull value then
            None
        else
            Some
                { ProviderRun = Identity.ProviderRunIdentity.create value?providerRun
                  ToolCallId = Identity.ToolCallId.create value?toolCallId }

    let private currentPiece (sessionId: string) (fence: DeliveryFence option) (assistant: string option) =
        assistant
        |> Option.filter (fun prose -> not (String.IsNullOrWhiteSpace prose))
        |> Option.map (fun prose ->
            let key =
                match fence with
                | Some fence ->
                    "turn:"
                    + JS.JSON.stringify
                        [| sessionId
                           Identity.ProviderRunIdentity.value fence.ProviderRun
                           Identity.ToolCallId.value fence.ToolCallId |]
                | None -> "prose:" + JS.JSON.stringify [| sessionId; prose |]

            { Key = key; Text = prose })

    let private materialPieces sessionId view fence assistant already =
        let history = select sessionId view already

        match view, currentPiece sessionId fence assistant with
        | Some _, Some current when not (Set.contains current.Key already) -> history @ [ current ]
        | _ -> history

    /// Live handoff and the durable witness share one storage representation.
    let materialize sessionId carry fence assistant =
        materialPieces sessionId (Some carry) fence assistant Set.empty |> encodePieces

    let private handOffPieces (ticket: obj) (candidates: ReturnedPiece list) : string option =
        owner.handOff (ticket, candidates |> List.map pieceValue |> List.toArray)
        |> Array.map pieceOf
        |> Array.toList
        |> encodePieces
        |> Some

    [<Emit("$0.ok === true")>]
    let private claimSucceeded (outcome: obj) : bool = jsNative

    /// The owner compares the fence itself: a refused claim carries the exact
    /// reason and leaves the cell and the cursor where they are.
    let private claimTicket (sessionId: string) (request: obj) : obj option =
        let outcome = owner.claim (sessionId, request)

        if claimSucceeded outcome then Some outcome?ticket else None

    /// The owner decides whether the cell may be handed off in this mode and
    /// which selected pieces were not yet returned; selection itself stays
    /// here because it reads the anchor ledger.
    let private handoffCarry (sessionId: string) (ticket: obj) (assistant: string option) : string option =
        match optionalView ticket?view with
        | None ->
            handOffPieces ticket [] |> ignore
            None
        | Some carry ->
            let candidates =
                materialPieces sessionId (Some carry) (fenceOf ticket?fence) assistant (alreadyReturned sessionId)

            handOffPieces ticket candidates

    let private handoff (sessionId: string) (request: obj) (assistant: string option) : string option =
        match claimTicket sessionId request with
        | None -> None
        | Some ticket -> handoffCarry sessionId ticket assistant

    let private cacheRequest () : obj = createObj [ "mode" ==> "cache" ]

    let private peekRequest () : obj = createObj [ "mode" ==> "peek" ]

    /// The whole call identity: provider run AND tool call id. The owner
    /// compares both fields; the caller does not pre-check anything.
    let private runRequest
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId)
        : obj =
        createObj
            [ "mode" ==> "run"
              "fence"
              ==> createObj
                      [ "providerRun" ==> Identity.ProviderRunIdentity.value providerRun
                        "toolCallId" ==> Identity.ToolCallId.value toolCallId ] ]

    /// A read-only observation of the exact call: the owner answers whether
    /// the current cell is this call's delivery, without taking anything.
    let private observeRequest
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId)
        : obj =
        createObj
            [ "mode" ==> "observe"
              "fence"
              ==> createObj
                      [ "providerRun" ==> Identity.ProviderRunIdentity.value providerRun
                        "toolCallId" ==> Identity.ToolCallId.value toolCallId ] ]

    let peekFence (sessionId: string) : DeliveryFence option = owner.fence sessionId |> fenceOf

    /// The exact handoff: the cell must carry the fence of this very tool
    /// call — the provider run AND the tool call id.
    ///
    /// The owner stores the whole fence when a delivery is recorded and
    /// compares both fields itself. A mismatch claims nothing and advances
    /// nothing: the cell stays where it is and the consumer sees nothing.
    /// The caller no longer peeks the fence before claiming.
    let tryTakeMaterialForCall
        (sessionId: string)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId)
        (assistant: string option)
        : string option =
        handoff sessionId (runRequest providerRun toolCallId) assistant

    /// Consume the frozen material of a durable witness. Its anchors were
    /// resolved when deliver settled; a later compression cannot reinterpret
    /// them or make an already proven delivery disappear.
    let tryTakeWitnessMaterial
        (sessionId: string)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId)
        (material: string)
        (assistant: string)
        : string option =
        match claimTicket sessionId (runRequest providerRun toolCallId) with
        | None -> None
        | Some ticket ->
            let fence = fenceOf ticket?fence
            let carryTexts = ReturnedMaterial.carry material
            let bodyTexts = ReturnedMaterial.body material
            let current = currentPiece sessionId fence (Some assistant)
            let bodyLastIndex = List.length bodyTexts - 1

            let bodyPieces =
                bodyTexts
                |> List.mapi (fun index text ->
                    current
                    |> Option.filter (fun piece -> index = bodyLastIndex && piece.Text = text)
                    |> Option.defaultWith (fun () -> historyPiece text))

            let candidates = (carryTexts |> List.map historyPiece) @ bodyPieces

            handOffPieces ticket candidates

    /// Select the staged view of a render cache cell. Presentation only:
    /// nothing taken here proves a delivery (clauses 1 and 3).
    let tryTakeMaterialWith (sessionId: string) (assistant: string option) : string option =
        handoff sessionId (cacheRequest ()) assistant

    /// Read-only observation: does the current cell belong to this very tool
    /// call? The owner answers from the stored fence; nothing is taken and
    /// the returned cursor does not move.
    let ownsCell
        (sessionId: string)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId)
        : bool =
        match claimTicket sessionId (observeRequest providerRun toolCallId) with
        | Some _ -> true
        | None -> false

    let tryTakeMaterial (sessionId: string) : string option = tryTakeMaterialWith sessionId None

    /// Snapshot of the handoff. Does not consume the cell and does not advance
    /// the returned cursor. Presentation only.
    let tryPeekMaterial (sessionId: string) : string option = handoff sessionId (peekRequest ()) None

    let tryPeekMaterialWith (sessionId: string) (assistant: string option) : string option =
        handoff sessionId (peekRequest ()) assistant

    let peekView (sessionId: string) : obj option =
        owner.peekCached sessionId |> optionalView

    let clear (sessionId: string) : unit = owner.clear sessionId

    let clearReturned (sessionId: string) : unit = owner.clearReturned sessionId

    let clearAll () : unit = owner.clearAll ()

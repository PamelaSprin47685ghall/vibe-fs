namespace Wanxiangshu.Repository.Programming.Js

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// The delivery owner holds cells, call fences and returned keys. This composer
/// selects Anchor material and renders the bytes captured at settlement.
module JsDeliveredValues =

    /// The exact tool invocation that produced this cell. A terminal from an
    /// older provider run cannot consume a newer run's delivery, and vice
    /// versa. `None` exists only for the test/dev harness and restored
    /// material whose call fence is no longer live.
    type DeliveryFence =
        { ProviderRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity
          ToolCallId: Wanxiangshu.Foundation.Identity.ToolCallId }

    type ReturnedPiece = { Key: string; Text: string }

    [<AllowNullLiteral>]
    type private DeliveryMessages =
        abstract stage: sessionId: string * view: obj -> unit
        abstract deliver: sessionId: string * fence: obj * view: obj -> unit
        abstract seal: sessionId: string * fence: obj * material: string * keys: string array -> bool
        abstract restore: sessionId: string * fence: obj * material: string -> unit
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

    let private owner = createDelivery ()

    let private optionalView (value: obj) : obj option =
        if isNull value || jsTypeof value = "undefined" then
            None
        else
            Some value

    let private fenceValue providerRun toolCallId =
        createObj
            [ "providerRun" ==> Identity.ProviderRunIdentity.value providerRun
              "toolCallId" ==> Identity.ToolCallId.value toolCallId ]

    let alreadyReturned (sessionId: string) : Set<string> = owner.returned sessionId |> Set.ofArray

    let recordView (sessionId: string) (view: obj option) : unit =
        owner.stage (sessionId, Option.toObj view)

    /// Record a delivery with the exact call fence that produced it. The
    /// settled tool call owns this cell; another run cannot take it.
    let recordFencedView
        (sessionId: string)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId)
        (view: obj option)
        : unit =
        owner.deliver (sessionId, fenceValue providerRun toolCallId, Option.toObj view)

    /// Restore the cell from a durable delivery witness's material (crash
    /// recovery) or from a harness that stands in for a real delivery. This
    /// is the only entry that accepts ready-made material.
    let restoreMaterial (sessionId: string) (text: string) : unit =
        if not (String.IsNullOrWhiteSpace text) then
            owner.restore (sessionId, null, text)

    let restoreFencedMaterial sessionId providerRun toolCallId text =
        if not (String.IsNullOrWhiteSpace text) then
            owner.restore (sessionId, fenceValue providerRun toolCallId, text)

    let peek (sessionId: string) : obj = owner.peekCached sessionId

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

    let private selectTicket sessionId (ticket: obj) assistant =
        let candidates =
            select sessionId (optionalView ticket?view) Set.empty
            |> List.map (fun piece -> createObj [ "key" ==> piece.Key; "text" ==> piece.Text ])
            |> List.toArray

        owner.handOff (ticket, candidates, Option.toObj assistant)
        |> Array.map (fun piece -> { Key = piece?key; Text = piece?text })
        |> Array.toList

    let sealFencedView sessionId providerRun toolCallId assistant =
        let request =
            createObj
                [ "mode" ==> "fence"
                  "providerRun" ==> Identity.ProviderRunIdentity.value providerRun
                  "toolCallId" ==> Identity.ToolCallId.value toolCallId ]

        match owner.claim (sessionId, request) with
        | null -> None
        | ticket when unbox<bool> ticket?``sealed`` || isNull ticket?view -> None
        | ticket ->
            let pieces = selectTicket sessionId ticket assistant
            let material = renderPieces pieces

            if
                owner.seal (
                    sessionId,
                    fenceValue providerRun toolCallId,
                    Option.toObj material,
                    pieces |> List.map _.Key |> List.toArray
                )
            then
                material
            else
                None

    let private handoff sessionId request assistant =
        match owner.claim (sessionId, request) with
        | null -> None
        | ticket when unbox<bool> ticket?``sealed`` ->
            owner.handOff (ticket, [||], null) |> ignore
            Option.ofObj (unbox<string> ticket?material)
        | ticket -> selectTicket sessionId ticket assistant |> renderPieces

    let tryTakeRenderedWith (sessionId: string) (assistant: string option) : string option =
        handoff sessionId (createObj [ "mode" ==> "cache" ]) assistant

    /// Take the cell only when it belongs to this exact provider run. An old
    /// terminal that arrives after a newer delivery cannot consume it.
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
    /// the returned cursor — showing the latest delivery is not itself a return.
    let tryPeekRendered (sessionId: string) : string option =
        handoff sessionId (createObj [ "mode" ==> "peek" ]) None

    /// Peek with the pre-call assistant prose included, without consuming.
    let tryPeekRenderedWith (sessionId: string) (assistant: string option) : string option =
        handoff sessionId (createObj [ "mode" ==> "peek" ]) assistant

    /// The raw view declaration of the current cell, without consuming it.
    let peekView (sessionId: string) : obj option =
        match owner.claim (sessionId, createObj [ "mode" ==> "peek" ]) with
        | null -> None
        | ticket -> optionalView ticket?view

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

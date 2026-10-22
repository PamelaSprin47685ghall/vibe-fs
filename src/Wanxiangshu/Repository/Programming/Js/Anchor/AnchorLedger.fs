namespace Wanxiangshu.Repository.Programming.Js.Anchor

open System.Collections.Generic
open Wanxiangshu.Repository.Programming.Js

module AnchorLedger =
    type private Snapshot =
        { Source: AnchorMessage list
          Segments: AnchorSegment list }

    type private Checkpoint =
        { Source: AnchorMessage list
          Segments: AnchorSegment list
          Declaration: AnchorViewDeclaration
          CallIdentity: (string * string) option
          LeadingAnchor: int option }

    // DSL-MUTABLE: resource — stable message identities and request snapshots
    let private nextNumber = ref 0
    let private numbers = Dictionary<string, Dictionary<string, int>>()
    let private snapshots = Dictionary<string, Snapshot>()
    let private checkpoints = Dictionary<string, Checkpoint>()
    let private views = Dictionary<string, AnchorViewDeclaration>()

    let numberMessage (sessionId: string) (messageId: string) : int =
        let session =
            match numbers.TryGetValue sessionId with
            | true, existing -> existing
            | false, _ ->
                let fresh = Dictionary<string, int>()
                numbers.[sessionId] <- fresh
                fresh

        match session.TryGetValue messageId with
        | true, anchor -> anchor
        | false, _ ->
            nextNumber.Value <- nextNumber.Value + 1
            session.[messageId] <- nextNumber.Value
            nextNumber.Value

    let visible sessionId =
        match snapshots.TryGetValue sessionId with
        | true, snapshot -> snapshot.Segments
        | false, _ -> []

    let context sessionId =
        visible sessionId
        |> List.collect _.Messages
        |> List.map (fun message -> message.Id, message.Text)

    let remember sessionId source segments =
        snapshots.[sessionId] <- { Source = source; Segments = segments }

    let checkpointSource sessionId =
        match checkpoints.TryGetValue sessionId with
        | true, checkpoint -> Some checkpoint.Source
        | false, _ -> None

    let checkpointCall sessionId =
        match checkpoints.TryGetValue sessionId with
        | true, checkpoint ->
            checkpoint.CallIdentity
            |> Option.map (fun (messageId, callId) -> messageId, callId, checkpoint.Declaration)
        | false, _ -> None

    let protectedPrefix sessionId =
        match checkpoints.TryGetValue sessionId with
        | true, checkpoint -> checkpoint.Segments |> List.take checkpoint.Declaration.PrefixCount
        | false, _ -> []

    let private leadingAnchorFor sessionId (checkpoint: Checkpoint) =
        match checkpoint.LeadingAnchor with
        | Some anchor -> anchor
        | None ->
            nextNumber.Value <- nextNumber.Value + 1

            checkpoints.[sessionId] <-
                { checkpoint with
                    LeadingAnchor = Some nextNumber.Value }

            nextNumber.Value

    let project sessionId suffix =
        let segments = AnchorHistory.append (numberMessage sessionId) [] suffix

        match checkpoints.TryGetValue sessionId, segments with
        | (false, _), _ -> segments
        | (true, checkpoint), (({ Anchor = 0 } as leading) :: tail) ->
            let anchor = leadingAnchorFor sessionId checkpoint
            checkpoint.Segments @ ({ leading with Anchor = anchor } :: tail)
        | (true, checkpoint), _ -> checkpoint.Segments @ segments

    let anchors sessionId =
        visible sessionId
        |> List.map (fun segment ->
            let identity =
                segment.Messages |> List.tryPick _.Id |> Option.defaultValue "opening"

            segment.Anchor, identity)

    let validate sessionId declaration =
        AnchorHistory.validate (visible sessionId) declaration

    let setView
        (sessionId: string)
        (callIdentity: (string * string) option)
        (declaration: AnchorViewDeclaration)
        : Result<unit, JsFailure> =
        let segments = visible sessionId

        validate sessionId declaration
        |> Result.map (fun () ->
            let carried =
                declaration.Carry
                |> List.map (function
                    | AnchorCarryItem.Reference anchor ->
                        segments |> List.find (fun segment -> segment.Anchor = anchor)
                    | AnchorCarryItem.Literal text ->
                        let id = sprintf "carry-literal-%d" (nextNumber.Value + 1)
                        let anchor = numberMessage sessionId id

                        { Anchor = anchor
                          Messages =
                            [ { Id = Some id
                                Text = text
                                Raw = null } ] })

            let source =
                match snapshots.TryGetValue sessionId with
                | true, snapshot -> snapshot.Source
                | false, _ -> []

            checkpoints.[sessionId] <-
                { Source = source
                  Segments = (segments |> List.take declaration.PrefixCount) @ carried
                  Declaration = declaration
                  CallIdentity = callIdentity
                  LeadingAnchor = None }

            views.[sessionId] <- declaration)

    let takeView sessionId =
        match views.TryGetValue sessionId with
        | true, declaration ->
            views.Remove sessionId |> ignore
            Some declaration
        | false, _ -> None

    let clearSession sessionId =
        numbers.Remove sessionId |> ignore
        snapshots.Remove sessionId |> ignore
        checkpoints.Remove sessionId |> ignore
        views.Remove sessionId |> ignore

    let clearAll () =
        numbers.Clear()
        snapshots.Clear()
        checkpoints.Clear()
        views.Clear()
        nextNumber.Value <- 0

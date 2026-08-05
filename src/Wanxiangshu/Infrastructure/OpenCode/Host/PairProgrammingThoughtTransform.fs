namespace Wanxiangshu.OpenCode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host

/// HOST-013: the pair-programming thought marker.
///
/// Injected into the final provider-facing transcript at
/// `experimental.chat.messages.transform`: one marker after every anchor (a user
/// message or a completed tool-result message) and before ReviewSeal, so the
/// seal digests the exact bytes the provider receives. XTrace capture runs
/// earlier in the chain, so the marker never enters a work record.
module PairProgrammingThoughtTransform =

    /// The frozen provider-visible thought text (HOST-013). Single point of
    /// definition — no other file may copy the literal.
    let text = "让我遵循结对编程的理念，用中文进行对话式思考。"

    /// The marker's source identity (HOST-013). Filtering must use this, never
    /// the text: a real user may quote the sentence.
    let source = "pair-programming-thought"

    let private idPrefix = "pair-programming-thought-"

    /// HOST-013: the marker identity predicate, by `info.source` only.
    let isPairProgrammingThought (rawMsg: obj) : bool =
        if isNull rawMsg then
            false
        else
            match rawMsg?info with
            | null -> false
            | info -> unbox<string> info?source = source

    /// HOST-013: an anchor is a user message or a message carrying a completed
    /// tool result.
    let private isAnchor (rawMsg: obj) : bool =
        match Projection.decodeMessage rawMsg with
        | None -> false
        | Some message ->
            message.Role = "user"
            || message.Parts
               |> List.exists (function
                   | WireToolResult _ -> true
                   | _ -> false)

    /// HOST-013: stable marker id = digest(sessionId + anchorMessageId +
    /// source). A missing session id participates as the empty string, so the
    /// id stays stable per anchor; re-transforming the same anchor yields the
    /// same id, keeping prompt bytes, prefix cache and review seal stable.
    let private stableId (sessionId: string option) (anchorMessageId: string option) : string =
        let digest =
            HostDigest.sha256Hex ((defaultArg sessionId "") + (defaultArg anchorMessageId "") + source)

        idPrefix + digest.Substring(0, 24)

    /// HOST-013: the synthetic assistant message, one reasoning part.
    let private buildMarker (id: string) : obj =
        createObj
            [ "info",
              box (
                  createObj
                      [ "id", box id
                        "role", box "assistant"
                        "source", box source
                        "synthetic", box true ]
              )
              "parts", box [| createObj [ "type", box "reasoning"; "text", box text ] |] ]

    /// HOST-013: replay one marker per anchor, oldest first, appending to the
    /// transcript. Every transform injects a marker after EVERY anchor (a user
    /// message or a completed tool-result message), not only the latest one:
    ///
    /// * a marker already present after an anchor is kept byte-identical
    ///   (idempotency key = anchor identity + marker source);
    /// * a new anchor gets its own marker.
    ///
    /// Because Host does not persist synthetic messages, each transform starts
    /// from the raw history; replaying every anchor with a stable id makes the
    /// provider-visible wire strictly append-only across rounds, so the prefix
    /// cache stays hit and the ReviewSeal never sees a rewritten prefix.
    let tryInject (sessionId: string option) (rawMessages: obj list) : obj list option =
        let anchorIndexes =
            rawMessages
            |> List.mapi (fun index raw -> index, raw)
            |> List.filter (fun (_, raw) -> isAnchor raw)
            |> List.map fst

        if List.isEmpty anchorIndexes then
            None
        else
            // Insert from the back so earlier indices stay valid.
            (rawMessages, List.rev anchorIndexes)
            ||> List.fold (fun acc anchorIndex ->
                match List.tryItem (anchorIndex + 1) acc with
                | Some next when isPairProgrammingThought next -> acc
                | _ ->
                    let anchorMessageId =
                        List.tryItem anchorIndex acc |> Option.bind Projection.hostMessageId

                    let marker = buildMarker (stableId sessionId anchorMessageId)

                    List.take (anchorIndex + 1) acc @ (marker :: List.skip (anchorIndex + 1) acc))
            |> Some

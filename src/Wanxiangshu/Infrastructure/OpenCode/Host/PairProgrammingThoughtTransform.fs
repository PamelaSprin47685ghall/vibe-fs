namespace Wanxiangshu.OpenCode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host

/// HOST-013: the pair-programming thought marker.
///
/// Injected into the final provider-facing transcript at
/// `experimental.chat.messages.transform`: after the latest anchor (a user
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

    /// Index of the latest anchor, scanning from the back (HOST-013). `None`
    /// when the history has no anchor — empty array, or system/assistant only.
    let private latestAnchorIndex (rawMessages: obj list) : int option =
        rawMessages
        |> List.mapi (fun index raw -> index, raw)
        |> List.rev
        |> List.tryFind (fun (_, raw) -> isAnchor raw)
        |> Option.map fst

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

    /// HOST-013: insert the marker right after the latest anchor.
    ///
    /// `None` when there is no anchor, or when the anchor is already followed
    /// by this round's marker (idempotency key = anchor identity + marker
    /// source). Previous rounds' markers are not anchors and never suppress a
    /// new injection.
    let tryInject (sessionId: string option) (rawMessages: obj list) : obj list option =
        match latestAnchorIndex rawMessages with
        | None -> None
        | Some anchorIndex ->
            match List.tryItem (anchorIndex + 1) rawMessages with
            | Some next when isPairProgrammingThought next -> None
            | _ ->
                let anchorMessageId =
                    List.tryItem anchorIndex rawMessages |> Option.bind Projection.hostMessageId

                let marker = buildMarker (stableId sessionId anchorMessageId)

                Some(
                    List.take (anchorIndex + 1) rawMessages
                    @ (marker :: List.skip (anchorIndex + 1) rawMessages)
                )

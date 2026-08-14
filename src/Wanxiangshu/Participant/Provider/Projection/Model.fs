namespace Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// The two provider projections (VERIFY-007).
///
/// They are separate types on purpose. One projection cannot serve both
/// purposes: byte equality needs the IDs, cross-session equality must not have
/// them. The old single `ProviderVisibleMessage` was used for both, which is why
/// the canary fixture matcher grew a bespoke normaliser beside it.
///
/// `Wire → Semantic` is one-way and lossy. `Semantic → Wire` does not exist —
/// the discarded IDs are unrecoverable, so there is deliberately no function
/// with that signature anywhere.
module ProviderProjection =

    // ── Wire: exactly what goes out ─────────────────────────────────────────

    /// A wire part, IDs included.
    ///
    /// `WireMedia` carries the content DIGEST rather than the bytes. Equality is
    /// unaffected — two different images digest differently, so every question this
    /// projection answers still gets the same answer — and the projection stays a
    /// value small enough to hold per request instead of megabytes of base64. The
    /// digest is computed at the Host codec boundary, exactly as `argsCanonical`
    /// already is.
    type WirePart =
        | WireText of text: string
        | WireReasoning of text: string
        | WireToolCall of callId: ToolCallId * name: string * argsCanonical: string
        | WireToolResult of callId: ToolCallId * resultCanonical: string
        | WireMedia of mediaType: string option * contentDigest: string

    type WireMessage = { Role: string; Parts: WirePart list }

    /// Used for prefix-cache gating (ARCH-004), the Seal Barrier (COMPANION-009)
    /// and Review input proof (REVIEW-010).
    ///
    /// Compared by exact bytes. Because it carries IDs it is only meaningful
    /// within one Session's timeline; comparing it across Sessions is
    /// meaningless, not merely strict.
    type ProviderWireProjection =
        {
            ProviderId: string option
            ModelId: string option
            Variant: string option
            /// Tool names in the order the provider received them. Order is part of
            /// the wire bytes, so it is part of the identity.
            Tools: string list
            System: string list
            Messages: WireMessage list
        }

    // ── Semantic: what the exchange meant ───────────────────────────────────

    /// A semantic part. No call IDs: those differ on every run, so keeping them
    /// would make a fixture unmatchable on its second execution.
    ///
    /// `SemanticMedia` keeps the media's stable identity (COMPANION-012). The digest
    /// is what makes two canonical prefixes containing different images compare
    /// unequal, which CTX-011's cutoff proof depends on. It is never sent to the
    /// Companion — CTX-013 replaces the whole part with an omission marker.
    type SemanticPart =
        | SemanticText of text: string
        | SemanticReasoning of text: string
        | SemanticToolCall of name: string * argsCanonical: string
        | SemanticToolResult of resultCanonical: string
        | SemanticMedia of mediaType: string option * contentDigest: string

    type SemanticMessage =
        { Role: string
          Parts: SemanticPart list }

    /// Used for canary fixture matching (VERIFY-003), Blogger delta
    /// (COMPANION-012) and behavioural comparison.
    ///
    /// Excludes message IDs, call IDs, timestamps, runtime metadata, directory,
    /// status, finish reason, cost and usage. Comparable across Sessions and
    /// across restarts, which is what makes a fixture reusable.
    ///
    /// Provider and model are NOT excluded: they are configuration, not identity,
    /// and FALLBACK-002's A/B switch changes the model, so a fixture that could
    /// not see it would be unable to distinguish the two sides.
    type ProviderSemanticProjection =
        { ProviderId: string option
          ModelId: string option
          Variant: string option
          Tools: string list
          System: string list
          Messages: SemanticMessage list }

    // ── the one-way downgrade ───────────────────────────────────────────────

    let private semanticPart (part: WirePart) : SemanticPart =
        match part with
        | WireText text -> SemanticText text
        | WireReasoning text -> SemanticReasoning text
        | WireToolCall(_callId, name, args) -> SemanticToolCall(name, args)
        | WireToolResult(_callId, result) -> SemanticToolResult result
        | WireMedia(mediaType, digest) -> SemanticMedia(mediaType, digest)

    let private semanticMessage (message: WireMessage) : SemanticMessage =
        { Role = message.Role
          Parts = message.Parts |> List.map semanticPart }

    /// Drop the identities. Named explicitly because VERIFY-007 permits exactly
    /// one such function and forbids implicit conversion.
    let toSemantic (wire: ProviderWireProjection) : ProviderSemanticProjection =
        { ProviderId = wire.ProviderId
          ModelId = wire.ModelId
          Variant = wire.Variant
          Tools = wire.Tools
          System = wire.System
          Messages = wire.Messages |> List.map semanticMessage }

    // ── canonical rendering ─────────────────────────────────────────────────
    //
    // Hand-built rather than reflected. A serializer's field order and optional
    // handling can change with a library upgrade, and both projections depend on
    // stable output: the Wire digest becomes a durable seal, and the Semantic
    // string becomes a fixture key. A version bump must not invalidate either.

    let private quote (text: string) =
        let escaped =
            text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")

        "\"" + escaped + "\""

    let private optional (value: string option) =
        match value with
        | Some text -> quote text
        | None -> "null"

    let private jsonArray (items: string list) = "[" + String.Join(",", items) + "]"

    let private field name value = quote name + ":" + value

    let private jsonObject (fields: string list) = "{" + String.Join(",", fields) + "}"

    let private wirePartJson (part: WirePart) =
        match part with
        | WireText text -> jsonObject [ field "kind" (quote "text"); field "text" (quote text) ]
        | WireReasoning text -> jsonObject [ field "kind" (quote "reasoning"); field "text" (quote text) ]
        | WireToolCall(callId, name, args) ->
            jsonObject
                [ field "kind" (quote "tool-call")
                  field "callId" (quote (ToolCallId.value callId))
                  field "name" (quote name)
                  field "args" (quote args) ]
        | WireToolResult(callId, result) ->
            jsonObject
                [ field "kind" (quote "tool-result")
                  field "callId" (quote (ToolCallId.value callId))
                  field "result" (quote result) ]
        | WireMedia(mediaType, digest) ->
            jsonObject
                [ field "kind" (quote "media")
                  field "mediaType" (optional mediaType)
                  field "contentDigest" (quote digest) ]

    let private semanticPartJson (part: SemanticPart) =
        match part with
        | SemanticText text -> jsonObject [ field "kind" (quote "text"); field "text" (quote text) ]
        | SemanticReasoning text -> jsonObject [ field "kind" (quote "reasoning"); field "text" (quote text) ]
        | SemanticToolCall(name, args) ->
            jsonObject
                [ field "kind" (quote "tool-call")
                  field "name" (quote name)
                  field "args" (quote args) ]
        | SemanticToolResult result -> jsonObject [ field "kind" (quote "tool-result"); field "result" (quote result) ]
        | SemanticMedia(mediaType, digest) ->
            jsonObject
                [ field "kind" (quote "media")
                  field "mediaType" (optional mediaType)
                  field "contentDigest" (quote digest) ]

    /// Byte-exact rendering of what was sent.
    let renderWire (wire: ProviderWireProjection) : string =
        jsonObject
            [ field "provider" (optional wire.ProviderId)
              field "model" (optional wire.ModelId)
              field "variant" (optional wire.Variant)
              field "tools" (jsonArray (wire.Tools |> List.map quote))
              field "system" (jsonArray (wire.System |> List.map quote))
              field
                  "messages"
                  (jsonArray (
                      wire.Messages
                      |> List.map (fun message ->
                          jsonObject
                              [ field "role" (quote message.Role)
                                field "parts" (jsonArray (message.Parts |> List.map wirePartJson)) ])
                  )) ]

    /// Cross-session rendering of what the exchange meant.
    let renderSemantic (semantic: ProviderSemanticProjection) : string =
        jsonObject
            [ field "provider" (optional semantic.ProviderId)
              field "model" (optional semantic.ModelId)
              field "variant" (optional semantic.Variant)
              field "tools" (jsonArray (semantic.Tools |> List.map quote))
              field "system" (jsonArray (semantic.System |> List.map quote))
              field
                  "messages"
                  (jsonArray (
                      semantic.Messages
                      |> List.map (fun message ->
                          jsonObject
                              [ field "role" (quote message.Role)
                                field "parts" (jsonArray (message.Parts |> List.map semanticPartJson)) ])
                  )) ]

    // ── the questions each projection answers ───────────────────────────────

    /// ARCH-004: `next` must keep `previous` as a byte prefix.
    ///
    /// Tools must be identical, not merely prefixed: a changed tool set
    /// invalidates the KV cache entirely, so treating it as an append would
    /// report a cache hit the provider will not honour.
    let isAppendOnlyPrefix (previous: ProviderWireProjection) (next: ProviderWireProjection) : bool =
        previous.Tools = next.Tools
        && previous.System = next.System
        && previous.ProviderId = next.ProviderId
        && previous.ModelId = next.ModelId
        && previous.Variant = next.Variant
        && List.length previous.Messages <= List.length next.Messages
        && (next.Messages |> List.truncate (List.length previous.Messages)) = previous.Messages

    /// REVIEW-010 `CanonicalVersion`. Bump when `renderWire` changes shape, so an
    /// old seal is recognisable as having been produced by a different renderer
    /// rather than silently compared against new bytes.
    ///
    /// Plain `let`, not `[<Literal>]`: Fable inlines a literal and emits no export.
    let CanonicalVersion = 1

    /// REVIEW-010: the digest that becomes a `ProviderInputSeal`.
    ///
    /// Takes the hash as a parameter so this module stays pure — Domain owns what
    /// gets hashed, the Host boundary owns how.
    let sealDigest (sha256: string -> string) (wire: ProviderWireProjection) : SealDigest =
        SealDigest.create (sha256 (renderWire wire))

    /// REVIEW-010: the digest of ONE tool result, as the wire projection renders it.
    ///
    /// REVIEW-003's challenge digest and REVIEW-010's seal contents must both come
    /// from this function. If either side applied its own normalisation, a single
    /// character of drift would make every confirmation fail closed — a defect that
    /// is indistinguishable from correct fail-closed behaviour and therefore nearly
    /// invisible. `resultCanonical` is already canonical: it is the value the codec
    /// put into `WireToolResult`.
    let toolResultDigest (sha256: string -> string) (resultCanonical: string) : SealDigest =
        SealDigest.create (sha256 resultCanonical)

    /// REVIEW-010: which parts this request carried, as digests.
    ///
    /// The set a second PERFECT is checked against. Tool results are digested
    /// as the challenge's delivery shape (REVIEW-003). Host 1.18.10's assembled
    /// view may carry the tool result as a TEXT part instead (`message-v2.ts`
    /// flattens completed tool outputs into assistant text), so text parts are
    /// digested too: the proof is "the model's input contained the challenge
    /// text", and a specific digest only matches that exact text — other text
    /// cannot impersonate it. Reasoning/media/tool-calls are not input the
    /// model could quote, so they stay out.
    let toolResultDigests (sha256: string -> string) (wire: ProviderWireProjection) : SealDigest list =
        wire.Messages
        |> List.collect (fun message -> message.Parts)
        |> List.choose (fun part ->
            match part with
            | WireToolResult(_callId, result) -> Some(toolResultDigest sha256 result)
            | WireText text -> Some(toolResultDigest sha256 text)
            | WireReasoning _
            | WireMedia _
            | WireToolCall _ -> None)

    /// VERIFY-003: the fixture key. Semantic by construction, so a fixture
    /// written once matches on every later run of the same conversation.
    let fixtureKey (semantic: ProviderSemanticProjection) : string = renderSemantic semantic

    /// COMPANION-012: Blogger delta compares semantic projections, never wire
    /// ones — otherwise a re-run with new call IDs would look like new content.
    let semanticallyEqual (left: ProviderSemanticProjection) (right: ProviderSemanticProjection) : bool =
        renderSemantic left = renderSemantic right

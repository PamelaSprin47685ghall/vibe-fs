namespace Wanxiangshu.Participant.Provider.Projection

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// The two provider projections (provider-projection-003).
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
    /// DSL-state-combination: physical — provider/model/variant options are
    /// wire identity facets for one rendered request; they are external protocol
    /// metadata, not business lifecycle state.
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

    /// Used for canary fixture matching (provider-projection-003) and behavioural comparison.
    ///
    /// Excludes message IDs, call IDs, timestamps, runtime metadata, directory,
    /// status, finish reason, cost and usage. Comparable across Sessions and
    /// across restarts, which is what makes a fixture reusable.
    ///
    /// Provider and model are NOT excluded: they are physical execution
    /// configuration, so a fixture must distinguish target changes.
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

    /// Drop the identities. Named explicitly because provider-projection-003
    /// permits exactly one such function and forbids implicit conversion.
    let toSemantic (wire: ProviderWireProjection) : ProviderSemanticProjection =
        { ProviderId = wire.ProviderId
          ModelId = wire.ModelId
          Variant = wire.Variant
          Tools = wire.Tools
          System = wire.System
          Messages = wire.Messages |> List.map semanticMessage }

    // ── canonical rendering ─────────────────────────────────────────────────
    //
    // The canonical bytes (field order, the five historical escapes, null for an
    // absent option) are owned by `wanxiangli/projection` `wire`/`wireMessage`/
    // `semantic` (CUT-17). The Wire digest is a durable seal and the Semantic
    // string a fixture key, so the format is fixed there, hand-built, and must not
    // follow a serializer upgrade. This module only lowers its typed parts to the
    // plain view the owner reads.

    [<AllowNullLiteral>]
    type private ProjectionBytes =
        abstract wire: projection: obj -> string
        abstract wireMessage: message: obj -> string
        abstract semantic: projection: obj -> string

    [<Import("Projection", "wanxiangli/projection")>]
    let private createProjection: unit -> ProjectionBytes = jsNative

    let private bytesOwner = createProjection ()

    let private wirePartView (part: WirePart) : obj =
        match part with
        | WireText text -> createObj [ "kind" ==> "text"; "text" ==> text ]
        | WireReasoning text -> createObj [ "kind" ==> "reasoning"; "text" ==> text ]
        | WireToolCall(callId, name, args) ->
            createObj
                [ "kind" ==> "tool-call"
                  "callId" ==> ToolCallId.value callId
                  "name" ==> name
                  "args" ==> args ]
        | WireToolResult(callId, result) ->
            createObj [ "kind" ==> "tool-result"; "callId" ==> ToolCallId.value callId; "result" ==> result ]
        | WireMedia(mediaType, digest) ->
            createObj [ "kind" ==> "media"; "mediaType" ==> Option.toObj mediaType; "contentDigest" ==> digest ]

    let private semanticPartView (part: SemanticPart) : obj =
        match part with
        | SemanticText text -> createObj [ "kind" ==> "text"; "text" ==> text ]
        | SemanticReasoning text -> createObj [ "kind" ==> "reasoning"; "text" ==> text ]
        | SemanticToolCall(name, args) -> createObj [ "kind" ==> "tool-call"; "name" ==> name; "args" ==> args ]
        | SemanticToolResult result -> createObj [ "kind" ==> "tool-result"; "result" ==> result ]
        | SemanticMedia(mediaType, digest) ->
            createObj [ "kind" ==> "media"; "mediaType" ==> Option.toObj mediaType; "contentDigest" ==> digest ]

    let private messageView (role: string) (parts: obj list) : obj =
        createObj [ "role" ==> role; "parts" ==> List.toArray parts ]

    let private wireMessageView (message: WireMessage) =
        messageView message.Role (message.Parts |> List.map wirePartView)

    let private projectionView provider model variant (tools: string list) (system: string list) (messages: obj list) =
        createObj
            [ "provider" ==> Option.toObj provider
              "model" ==> Option.toObj model
              "variant" ==> Option.toObj variant
              "tools" ==> List.toArray tools
              "system" ==> List.toArray system
              "messages" ==> List.toArray messages ]

    /// Byte-exact rendering of what was sent.
    let renderWire (wire: ProviderWireProjection) : string =
        projectionView
            wire.ProviderId
            wire.ModelId
            wire.Variant
            wire.Tools
            wire.System
            (wire.Messages |> List.map wireMessageView)
        |> bytesOwner.wire

    /// Cross-session rendering of what the exchange meant.
    let renderSemantic (semantic: ProviderSemanticProjection) : string =
        projectionView
            semantic.ProviderId
            semantic.ModelId
            semantic.Variant
            semantic.Tools
            semantic.System
            (semantic.Messages
             |> List.map (fun message -> messageView message.Role (message.Parts |> List.map semanticPartView)))
        |> bytesOwner.semantic

    // ── the questions each projection answers ───────────────────────────────

    [<Import("Prefix", "wanxiangli/prefix")>]
    let private prefixOwnerCtor: obj = jsNative

    let private prefixOwner: obj = createNew prefixOwnerCtor ()

    let private messageBytes (message: WireMessage) =
        bytesOwner.wireMessage (wireMessageView message)

    let private appendLawView (wire: ProviderWireProjection) : obj =
        createObj
            [ "provider", box (Option.toObj wire.ProviderId)
              "model", box (Option.toObj wire.ModelId)
              "variant", box (Option.toObj wire.Variant)
              "tools", box (List.toArray wire.Tools)
              "system", box (List.toArray wire.System)
              "messages", box (wire.Messages |> List.map messageBytes |> List.toArray) ]

    /// ARCH-004: `next` must keep `previous` as a byte prefix. The append law
    /// (identical provider/model/variant/tools/system, exact message prefix) is
    /// owned by `wanxiangli/prefix`; this projection only supplies each message's
    /// canonical wire bytes.
    let isAppendOnlyPrefix (previous: ProviderWireProjection) (next: ProviderWireProjection) : bool =
        let verdict: obj = prefixOwner?classify (appendLawView previous, appendLawView next)
        unbox<bool> verdict?append

    /// Compares semantic projections, never wire ones.
    let semanticallyEqual (left: ProviderSemanticProjection) (right: ProviderSemanticProjection) : bool =
        renderSemantic left = renderSemantic right

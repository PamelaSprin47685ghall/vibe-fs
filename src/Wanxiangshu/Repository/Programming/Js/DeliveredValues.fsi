namespace Wanxiangshu.Repository.Programming.Js

/// Process-live delivered-view cell, keyed by session. The table holds two
/// tiers: a delivered cell that carries the fence of the exact tool
/// invocation that produced it, and a render cache cell without a fence that
/// only stages a view for presentation. Only the fenced tier is evidence of a
/// delivery (clause 3). A separate returned cursor records each
/// representative together with its rendered material, so identical
/// deliveries are not repeated and changed summaries remain visible. Restart
/// drops both; crash recovery reads the durable delivery witness, never a
/// ready-made string (clause 1).
module JsDeliveredValues =

    /// The exact tool invocation that produced a cell. A terminal from an
    /// older provider run cannot consume a newer run's delivery, and vice
    /// versa. `None` marks a render cache cell: a staged view with no call
    /// fence, which is presentation only and never delivery evidence.
    type DeliveryFence =
        { ProviderRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity
          ToolCallId: Wanxiangshu.Foundation.Identity.ToolCallId }

    type ReturnedPiece = { Key: string; Text: string }

    val alreadyReturned: sessionId: string -> Set<string>
    /// Stage a view for presentation only: without the call fence this cell
    /// is a render cache and never delivery evidence (clauses 1 and 3).
    val recordView: sessionId: string -> view: obj option -> unit

    /// Record a delivery with the fence of the call that produced it. Only a
    /// fenced cell is delivery evidence (clause 3).
    val recordFencedView:
        sessionId: string ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId ->
        view: obj option ->
            unit

    /// Consume the delivered view of this session. Only a fenced cell is a
    /// delivery; a render cache cell is left in place for presentation.
    val take: sessionId: string -> obj
    /// The delivered view of this session — delivery evidence only. A cell
    /// without the fence of the call that produced it proves nothing here
    /// (clause 3).
    val peek: sessionId: string -> obj
    /// The staged view of this session, delivered or cached. Presentation
    /// only: this reader never judges a delivery.
    val peekCached: sessionId: string -> obj
    val select: sessionId: string -> view: obj option -> already: Set<string> -> ReturnedPiece list

    /// Select carry and append this call's report as canonical returned material.
    val materialize:
        sessionId: string -> carry: obj -> fence: DeliveryFence option -> assistant: string option -> string

    /// Select the staged view of a render cache cell. Presentation only:
    /// nothing taken here proves a delivery (clauses 1 and 3).
    val tryTakeMaterialWith: sessionId: string -> assistant: string option -> string option

    /// The exact handoff: the cell must carry the fence of this very tool
    /// call — the provider run AND the tool call id. A mismatch claims
    /// nothing and advances nothing; the owner compares the fence itself.
    val tryTakeMaterialForCall:
        sessionId: string ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId ->
        assistant: string option ->
            string option

    /// Read-only observation: does the current cell belong to this very tool
    /// call? Nothing is taken and the returned cursor does not move.
    val ownsCell:
        sessionId: string ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId ->
            bool

    /// Consume the exact call using already materialized durable evidence,
    /// without resolving its historical anchors a second time.
    val tryTakeWitnessMaterial:
        sessionId: string ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId ->
        material: string ->
        assistant: string ->
            string option

    /// Select the staged view of a render cache cell. Presentation only.
    val tryTakeMaterial: sessionId: string -> string option
    /// Snapshot of the handoff, delivery or render cache. Presentation only:
    /// it never judges a delivery.
    val tryPeekMaterial: sessionId: string -> string option
    val tryPeekMaterialWith: sessionId: string -> assistant: string option -> string option
    /// The raw view declaration of the current cell — a delivery or a render
    /// cache. Presentation only.
    val peekView: sessionId: string -> obj option
    val peekFence: sessionId: string -> DeliveryFence option
    val clear: sessionId: string -> unit
    val clearReturned: sessionId: string -> unit
    val clearAll: unit -> unit

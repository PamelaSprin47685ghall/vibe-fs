namespace Wanxiangshu.Repository.Programming.Js

/// Compose Anchor selection and rendering with the delivery owner's state.
module JsDeliveredValues =

    /// The exact tool invocation that produced a cell. A terminal from an
    /// older provider run cannot consume a newer run's delivery, and vice
    /// versa. `None` marks a render cache cell: a staged view with no call
    /// fence, which is presentation only and never delivery evidence.
    type DeliveryFence =
        { ProviderRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity
          ToolCallId: Wanxiangshu.Foundation.Identity.ToolCallId }

    type ReturnedPiece = { Key: string; Text: string }

    [<Sealed>]
    type CapturedDelivery =
        member Text: string

    val captureMaterial: sessionId: string -> view: obj -> assistant: string option -> CapturedDelivery option

    val publishCaptured:
        sessionId: string ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId ->
        captured: CapturedDelivery ->
            bool

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

    val restoreMaterial: sessionId: string -> text: string -> unit

    val restoreFencedMaterial:
        sessionId: string ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId ->
        text: string ->
            unit

    val sealFencedView:
        sessionId: string ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId ->
        assistant: string option ->
            string option

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
    /// Render the staged view of a render cache cell. Presentation only:
    /// nothing taken here proves a delivery (clauses 1 and 3).
    val tryTakeRenderedWith: sessionId: string -> assistant: string option -> string option

    /// The exact handoff: the cell must carry the fence of this very tool
    /// call — the provider run AND the tool call id. A mismatch claims
    /// nothing and advances nothing.
    val tryTakeRenderedForCall:
        sessionId: string ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId ->
        assistant: string option ->
            string option

    /// The run-only handoff: the cell must carry the fence of this provider
    /// run. It matches the provider run only, not the full fence, so within
    /// one run a different call can take this cell and advance the returned
    /// cursor. It is a harness-only channel; production consumers use
    /// `tryTakeRenderedForCall` or the durable witness.
    val tryTakeRenderedFor:
        sessionId: string ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        assistant: string option ->
            string option

    /// Render the staged view of a render cache cell. Presentation only.
    val tryTakeRendered: sessionId: string -> string option
    /// Snapshot of the handoff, delivery or render cache. Presentation only:
    /// it never judges a delivery.
    val tryPeekRendered: sessionId: string -> string option
    val tryPeekRenderedWith: sessionId: string -> assistant: string option -> string option
    /// The raw view declaration of the current cell — a delivery or a render
    /// cache. Presentation only.
    val peekView: sessionId: string -> obj option
    val peekFence: sessionId: string -> DeliveryFence option
    val clear: sessionId: string -> unit
    val clearReturned: sessionId: string -> unit
    val clearAll: unit -> unit

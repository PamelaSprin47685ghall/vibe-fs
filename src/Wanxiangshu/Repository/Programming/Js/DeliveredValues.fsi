namespace Wanxiangshu.Repository.Programming.Js

/// Compose Anchor selection and rendering with the delivery owner's state.
module JsDeliveredValues =

    /// The exact tool invocation that produced a cell. A terminal from an
    /// older provider run cannot consume a newer run's delivery, and vice
    /// versa. `None` exists only for the test/dev harness and restored
    /// material whose call fence is no longer live.
    type DeliveryFence =
        { ProviderRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity
          ToolCallId: Wanxiangshu.Foundation.Identity.ToolCallId }

    type ReturnedPiece = { Key: string; Text: string }

    val alreadyReturned: sessionId: string -> Set<string>
    val recordView: sessionId: string -> view: obj option -> unit

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

    val peek: sessionId: string -> obj
    val select: sessionId: string -> view: obj option -> already: Set<string> -> ReturnedPiece list
    val tryTakeRenderedWith: sessionId: string -> assistant: string option -> string option

    val tryTakeRenderedFor:
        sessionId: string ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        assistant: string option ->
            string option

    val tryTakeRendered: sessionId: string -> string option
    val tryPeekRendered: sessionId: string -> string option
    val tryPeekRenderedWith: sessionId: string -> assistant: string option -> string option
    val peekView: sessionId: string -> obj option
    val peekFence: sessionId: string -> DeliveryFence option
    val clear: sessionId: string -> unit
    val clearReturned: sessionId: string -> unit
    val clearAll: unit -> unit

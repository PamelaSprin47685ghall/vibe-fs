namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// CRASH-018 marker for the exact Host user material produced by `/continue`.
///
/// A SessionId is a reusable container and is therefore not a valid suppression
/// lifetime. The durable semantic marker rides on the visible text part itself.
/// The process-local registry below only remembers which exact physical material
/// carried that marker so later reconcile/idle passes can recognize the same turn;
/// a new unmarked PhysicalUserMessageId for the same SessionId clears it immediately.
/// No session-end/idle/abort signal is required for correctness.
module ExplicitResumeSuppression =

    [<Literal>]
    let MetadataKey = "wanxiangshu_explicit_resume"

    let markedTextPart (text: string) : obj =
        createObj
            [ "type" ==> "text"
              "text" ==> text
              "metadata" ==> createObj [ MetadataKey ==> true ] ]

    let private gate = obj ()
    let private markedPhysicalBySession = Dictionary<string, string>()

    [<Emit("""(() => {
      const parts = Array.isArray($0?.parts) ? $0.parts : [];
      return parts.some((part) => part?.metadata?.wanxiangshu_explicit_resume === true);
    })()""")>]
    let private outputHasMarker (_output: obj) : bool = jsNative

    /// chat.message is the physical-material boundary. Same marked material keeps
    /// its suppression across provider retries; a later ordinary user material on
    /// the same SessionId removes it immediately.
    let observePhysicalMaterial (sessionId: SessionId) (physicalId: PhysicalUserMessageId) (output: obj) : unit =
        lock gate (fun () ->
            let sessionKey = SessionId.value sessionId

            if outputHasMarker output then
                markedPhysicalBySession.[sessionKey] <- PhysicalUserMessageId.value physicalId
            else
                markedPhysicalBySession.Remove sessionKey |> ignore)

    let isPhysicalMaterial (sessionId: SessionId) (physicalId: PhysicalUserMessageId) : bool =
        lock gate (fun () ->
            match markedPhysicalBySession.TryGetValue(SessionId.value sessionId) with
            | true, current -> current = PhysicalUserMessageId.value physicalId
            | false, _ -> false)

    /// Cleanup only. Exact-new-material replacement is the correctness boundary.
    let dropSession (sessionId: SessionId) : unit =
        lock gate (fun () ->
            markedPhysicalBySession.Remove(SessionId.value sessionId) |> ignore)

    /// The provider-facing transform always receives the current user request as
    /// the trailing real message. Historical `/continue` messages may remain in
    /// the transcript, so only that trailing user material is authoritative.
    [<Emit("""(() => {
      const messages = Array.isArray($0?.messages) ? $0.messages : [];
      const last = messages.length === 0 ? null : messages[messages.length - 1];
      const info = last?.info ?? last;
      if (String(info?.role ?? '').toLowerCase() !== 'user') return false;
      const parts = Array.isArray(last?.parts) ? last.parts : [];
      return parts.some((part) => part?.metadata?.wanxiangshu_explicit_resume === true);
    })()""")>]
    let isCurrentMaterial (_output: obj) : bool = jsNative

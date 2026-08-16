namespace Wanxiangshu.OpenCode

open Fable.Core
open Fable.Core.JsInterop

/// CRASH-018 marker for the exact Host user material produced by `/continue`.
///
/// This is deliberately stateless. A SessionId is a reusable container and is
/// therefore not a valid suppression lifetime. The marker rides on the visible
/// text part itself; provider retries of that same material remain disclosure-only,
/// while the next ordinary user material naturally has no marker and proceeds
/// normally without waiting for idle/abort/session teardown.
module ExplicitResumeSuppression =

    [<Literal>]
    let MetadataKey = "wanxiangshu_explicit_resume"

    let markedTextPart (text: string) : obj =
        createObj
            [ "type" ==> "text"
              "text" ==> text
              "metadata" ==> createObj [ MetadataKey ==> true ] ]

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

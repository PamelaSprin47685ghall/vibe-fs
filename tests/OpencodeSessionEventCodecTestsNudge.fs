module VibeFs.Tests.OpencodeSessionEventCodecTestsNudge

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Shell.OpencodeSessionEventCodec
open VibeFs.Kernel.NudgeState
open VibeFs.Kernel.Nudge.Types

let private part (text: string) : obj = box {| ``type`` = "text"; text = text |}

let decodeNudgeHostEventStreamAbort () =
    let ev = decodeNudgeHostEvent "stream-abort" (createObj [])
    check "decodeNudgeHostEvent stream-abort" (match ev with StreamAbort -> true | _ -> false)

let decodeNudgeHostEventSessionLifecyclePure () =
    let ev = decodeNudgeHostEvent "session.deleted" (createObj [])
    check "decodeNudgeHostEvent session.deleted" (match ev with SessionDeleted -> true | _ -> false)

let decodeNudgeHostEventSessionIdle () =
    let ev = decodeNudgeHostEvent "session.idle" (createObj [])
    check "decodeNudgeHostEvent session.idle" (match ev with SessionIdle -> true | _ -> false)

let decodeNudgeHostEventRetryProgressByName () =
    let ev = decodeNudgeHostEvent "session.next.text.delta" (createObj [])
    check "decodeNudgeHostEvent retry-progress" (match ev with RetryProgress -> true | _ -> false)

let decodeNudgeHostEventUnknown () =
    let ev = decodeNudgeHostEvent "unknown.event" (createObj [])
    check "decodeNudgeHostEvent unknown falls to Other" (match ev with Other -> true | _ -> false)

let decodeNudgeHostEventMessageUpdatedCompleted () =
    let props =
        box {|
            info = box {| role = "assistant"; finish = "stop" |}
        |}
    let ev = decodeNudgeHostEvent "message.updated" props
    check "decodeNudgeHostEvent message.updated completed"
        (match ev with MessageUpdated UpdateCompletedAssistant -> true | _ -> false)

let decodeNudgeHostEventMessageUpdatedAborted () =
    let props =
        box {|
            info = box {| role = "assistant"; error = box {| name = "MessageAbortedError" |} |}
        |}
    let ev = decodeNudgeHostEvent "message.updated" props
    check "decodeNudgeHostEvent message.updated aborted"
        (match ev with MessageUpdated UpdateAborted -> true | _ -> false)

let decodeNudgeHostEventMessageUpdatedNoChange () =
    let props =
        box {|
            info = box {| role = "user" |}
        |}
    let ev = decodeNudgeHostEvent "message.updated" props
    check "decodeNudgeHostEvent message.updated nochange"
        (match ev with MessageUpdated UpdateNoChange -> true | _ -> false)

let decodeNudgeHostEventMessagePartUpdatedRetry () =
    let props = box {| part = box {| ``type`` = "retry" |} |}
    let ev = decodeNudgeHostEvent "message.part.updated" props
    check "decodeNudgeHostEvent message.part.updated retry"
        (match ev with MessagePartUpdated PartRetry -> true | _ -> false)

let decodeNudgeHostEventMessagePartUpdatedAborted () =
    let props = box {| part = box {| ``type`` = "tool"; error = box {| name = "MessageAbortedError" |} |} |}
    let ev = decodeNudgeHostEvent "message.part.updated" props
    check "decodeNudgeHostEvent message.part.updated aborted"
        (match ev with MessagePartUpdated PartAborted -> true | _ -> false)

let decodeNudgeHostEventSessionStatusBusy () =
    let props = box {| status = box {| ``type`` = "busy" |} |}
    let ev = decodeNudgeHostEvent "session.status" props
    check "decodeNudgeHostEvent session.status busy"
        (match ev with SessionStatusBusy -> true | _ -> false)

let decodeNudgeHostEventSessionStatusUnknownFallsToOther () =
    let props = box {| status = box {| ``type`` = "garbage" |} |}
    let ev = decodeNudgeHostEvent "session.status" props
    check "decodeNudgeHostEvent session.status unknown falls to Other"
        (match ev with Other -> true | _ -> false)

let decodeNudgeHostEventSessionPromptedFromPartsFallback () =
    let props = box {| parts = box [| part "hello world" |] |}
    let ev = decodeNudgeHostEvent "session.next.prompted" props
    check "decodeNudgeHostEvent session.next.prompted from parts"
        (match ev with SessionNextPrompted text when text = "hello world" -> true | _ -> false)

let decodeNudgeHostEventSessionPromptedFromPromptText () =
    let props = box {| prompt = box {| text = "direct text" |} |}
    let ev = decodeNudgeHostEvent "session.next.prompted" props
    check "decodeNudgeHostEvent session.next.prompted from prompt"
        (match ev with SessionNextPrompted text when text = "direct text" -> true | _ -> false)
module Wanxiangshu.Kernel.CapsSynthPolicy

let capsUserPrefix = "caps-synth-user-"
let capsAssistantPrefix = "caps-synth-assistant-"
let capsAcknowledgePrefix = "caps-synth-ack-"
let capsSynthIdPrefix = "caps-synth-"
let private maxToolCallIdLength = 64

/// Build a stable synthetic tool-call ID accepted by providers that cap call IDs
/// at 64 characters. Preserve the tail of long session IDs, where UUID entropy lives.
let capsToolCallId (prefix: string) (epochId: string) (fingerprint: string) (index: int) : string =
    let suffix = $"-{fingerprint}-{index}"
    let maxEpochLength = max 0 (maxToolCallIdLength - prefix.Length - suffix.Length)

    let epochSuffix =
        if epochId.Length <= maxEpochLength then
            epochId
        else
            epochId.Substring(epochId.Length - maxEpochLength)

    prefix + epochSuffix + suffix

let isCapsSynthId (id: string) : bool =
    id <> "" && id.StartsWith capsSynthIdPrefix

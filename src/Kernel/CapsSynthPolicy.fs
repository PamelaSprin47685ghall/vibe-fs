module Wanxiangshu.Kernel.CapsSynthPolicy

let capsUserPrefix = "caps-synth-user-"
let capsAssistantPrefix = "caps-synth-assistant-"
let capsAcknowledgePrefix = "caps-synth-ack-"
let capsSynthIdPrefix = "caps-synth-"

let isCapsSynthId (id: string) : bool =
    id <> "" && id.StartsWith capsSynthIdPrefix

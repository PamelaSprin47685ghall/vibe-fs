module VibeFs.Kernel.SyntheticIds

open VibeFs.Kernel.Dyn
open VibeFs.Kernel.MessageDecoder

let capsSynthUserPrefix = "caps-synth-user-"
let capsSynthAssistantPrefix = "caps-synth-assistant-"
let rewritePreludeUserPrefix = "rewrite-prelude-user-"
let rewritePreludeAssistantPrefix = "rewrite-prelude-assistant-"
let magicTodoProjectionPrefix = "magic-todo-projection-"
let magicTodoPrefixPrefix = "magic-todo-prefix-"

let private allPrefixes =
    [ capsSynthUserPrefix; capsSynthAssistantPrefix
      rewritePreludeUserPrefix; rewritePreludeAssistantPrefix
      magicTodoProjectionPrefix; magicTodoPrefixPrefix ]

let isSyntheticId (id: string) : bool =
    id <> "" && allPrefixes |> List.exists id.StartsWith

let isSyntheticMessage (msg: obj) : bool =
    let info = messageInfo msg
    if isNullish info then false else isSyntheticId (infoId info)

let stripSyntheticMessages (messages: obj array) : obj array =
    if isNullish messages then [||]
    else messages |> Array.filter (fun msg -> not (isSyntheticMessage msg))

module VibeFs.Kernel.MessageDecoder

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

let messageInfo (msg: obj) : obj = Dyn.get msg "info"

let messageParts (msg: obj) : obj = Dyn.get msg "parts"

let infoId (info: obj) : string = Dyn.str info "id"

let infoAgent (info: obj) : string = Dyn.str info "agent"

let infoSessionID (info: obj) : string = Dyn.str info "sessionID"

let infoRole (info: obj) : string = Dyn.str info "role"

let infoError (info: obj) : obj = Dyn.get info "error"

let infoToolName (info: obj) : string = Dyn.str info "toolName"

let infoContent (info: obj) : obj = Dyn.get info "content"

let infoDetails (info: obj) : obj = Dyn.get info "details"

let infoIsError (info: obj) : bool =
    let isError = Dyn.get info "isError"
    not (Dyn.isNullish isError) && (isError :?> bool)

let infoFinish (info: obj) : string = Dyn.str info "finish"

let infoTimeCompleted (info: obj) : obj =
    let time = Dyn.get info "time"
    if Dyn.isNullish time then null else Dyn.get time "completed"

let entryType (entry: obj) : string = Dyn.str entry "type"

let entryCustomType (entry: obj) : string = Dyn.str entry "customType"

let entryMessage (entry: obj) : obj = Dyn.get entry "message"

let entryData (entry: obj) : obj = Dyn.get entry "data"

let partText (part: obj) : obj = Dyn.get part "text"

let partType (part: obj) : string = Dyn.str part "type"

let partError (part: obj) : obj = Dyn.get part "error"

let partState (part: obj) : obj = Dyn.get part "state"

let partOutput (part: obj) : obj = Dyn.get part "output"


let firstPresent (keys: string list) (source: obj) : string option =
    keys |> List.tryPick (fun key ->
        let value = Dyn.get source key
        if Dyn.isNullish value then None else Some (string value))


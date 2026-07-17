module Wanxiangshu.Runtime.MessageSourceClassify

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Messaging

[<Emit("typeof $0")>]
let private jsTypeOf (o: obj) : string = Fable.Core.JS.undefined

let private checkText (t: string) =
    t <> null && t.Contains("<wanxiangshu-caps")

let private checkMeta (t: string) =
    t <> null
    && (t.Contains("caps-synth-")
        || t.Contains("caps-call-")
        || t.Contains("caps-fr-")
        || t.Contains("caps-tool-"))

let private checkVal (v: obj) =
    if box v = null then
        false
    else
        let s = string v
        checkText s || checkMeta s

let private checkObj (r: obj) =
    if box r = null then
        false
    elif jsTypeOf r = "object" then
        let rid = r?id
        let rcallID = r?callID
        let rtoolCallId = r?toolCallId
        let rmetadata = r?metadata
        let rkind = if box rmetadata <> null then rmetadata?kind else null
        let rtext = r?text

        checkVal rid
        || checkVal rcallID
        || checkVal rtoolCallId
        || checkVal rkind
        || checkVal rtext
    else
        let rStr = string r
        checkText rStr || checkMeta rStr

let private partHasCaps (p: Part<obj>) : bool =
    match p with
    | TextPart t -> checkText t
    | ToolPart(tool, callID, stateOpt, _) ->
        checkText tool
        || checkText callID
        || checkMeta callID
        || (match stateOpt with
            | Some st -> checkText st.output || checkText st.error
            | None -> false)
    | RawPart r -> checkObj r

let classifySource (id: string) (parts: Part<obj> list option) (raw: obj option) : Source =
    let isSynth = id <> "" && synthPrefixes |> List.exists id.StartsWith

    let hasCapsTextOrMeta =
        let partsContain =
            match parts with
            | Some pts -> pts |> List.exists partHasCaps
            | None -> false

        let rawContain =
            match raw with
            | Some r -> checkObj r
            | None -> false

        partsContain || rawContain

    if isSynth then
        let prefix = synthPrefixes |> List.find id.StartsWith
        Synthetic prefix
    elif hasCapsTextOrMeta then
        Synthetic "caps"
    else
        Native

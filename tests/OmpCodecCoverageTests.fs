module Wanxiangshu.Tests.OmpCodecCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.MessagingCodecEncode
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn

let mi () : MessageInfo<obj> = { id="m1"; sessionID="s1"; role=Assistant; agent=""; isError=false; toolName=""; details=null; time=null }
let txt (t:string) : Message<obj> = { info=mi(); parts=[TextPart t]; source=Native; raw=null }
let tmsg (tn:string) (cid:string) (st:ToolState<obj> option) (raw:obj) : Message<obj> = { info=mi(); parts=[ToolPart(tn,cid,st,raw)]; source=Native; raw=raw }
let rmsg (raw:obj) : Message<obj> = { info=mi(); parts=[]; source=Native; raw=raw }

// encodeMessage

let encText () =
    let e = encodeMessage (txt "hello")
    check "text encoded" (not (isNull e))
    let p = unbox<obj array> (Dyn.get e "parts")
    equal "text type" "text" (Dyn.str (Array.get p 0) "type")
    equal "text body" "hello" (Dyn.str (Array.get p 0) "text")

let encToolStateMatch () =
    let st = {status="running";output="out";error="";input=box"in";operationAction=""}
    let raw = createObj ["state", box (createObj ["status", box"running"; "output", box"out"; "error", box""])]
    let e = encodeMessage (tmsg "t" "c1" (Some st) raw)
    equal "state status preserved" "running" (Dyn.str (Dyn.get e "state") "status")
    check "parts present" (Dyn.has e "parts")

let encToolStateMismatch () =
    let st = {status="done";output="new";error="";input=box"in";operationAction=""}
    let raw = createObj ["state", box (createObj ["status", box"running"; "output", box"old"; "error", box""])]
    let e = encodeMessage (tmsg "t" "c2" (Some st) raw)
    let parts = unbox<obj array> (Dyn.get e "parts")
    let ns = Dyn.get (Array.get parts 0) "state"
    equal "new status" "done" (Dyn.str ns "status")
    equal "new output" "new" (Dyn.str ns "output")

let encToolNoState () =
    let e = encodeMessage (tmsg "t" "c3" None null)
    let p = unbox<obj array> (Dyn.get e "parts")
    equal "type" "tool" (Dyn.str (Array.get p 0) "type")
    equal "tool" "t" (Dyn.str (Array.get p 0) "tool")
    check "no state field" (Dyn.isNullish (Dyn.get (Array.get p 0) "state"))

let encRawPart () =
    let raw = createObj ["type",box"raw";"data",box 42]
    let e = encodeMessage (rmsg raw)
    equal "raw type" "raw" (Dyn.str e "type")
    equal "raw data" 42 (unbox<int> (Dyn.get e "data"))
    check "raw parts empty" (Dyn.isArray (Dyn.get e "parts"))

let encRawSet () =
    let raw = createObj ["info", box (createObj []); "parts", box [||]]
    let e = encodeMessage {info=mi(); parts=[TextPart"hi"]; source=Native; raw=raw}
    check "raw path updated" (Dyn.isArray (Dyn.get e "parts"))

let encMsgs () =
    check "list→array" (Dyn.isArray (encodeMessages [txt"a";txt"b"]))

// decodeEntry / decodeEntries

let decUser () =
    let e = createObj ["message", box (createObj ["role", box"user"; "content", box [| createObj ["type", box"text"; "text", box"hi"] |]])]
    match decodeEntry "s1" e with None -> check "user→Some" false | Some m -> equal "role" User m.info.role

let decAsst () =
    let e = createObj ["message", box (createObj ["role", box"assistant"; "content", box [||]])]
    match decodeEntry "s1" e with None -> check "asst→Some" false | Some m -> equal "role" Assistant m.info.role

let decInfoFallback () =
    let e = createObj ["info", box (createObj ["role", box"user"; "id", box"e1"]); "parts", box [| createObj ["type", box"text"; "text", box"fb"] |]]
    match decodeEntry "s1" e with None -> check "info→Some" false | Some m -> equal "role" User m.info.role

let decNull () = check "null→None" (decodeEntry "s1" null = None)
let decEmptyRole () = check "emptyRole→None" (decodeEntry "s1" (createObj ["message", box (createObj ["role", box""; "content", box [||]])]) = None)
let decArr () =
    let e1 = createObj ["message", box (createObj ["role", box"user"; "content", box [||]])]
    let e2 = createObj ["message", box (createObj ["role", box"assistant"; "content", box [||]])]
    equal "arr len" 2 (decodeEntries "s1" [| e1; e2 |]).Length

// entries / readAssistantText

let entSM () =
    let sm = createObj ["getEntries", box (fun () -> [| createObj ["role", box "user"] |] :> obj)]
    equal "ent len" 1 (unbox<obj array> (entries (unbox<ISessionManager> sm))).Length

let entEmpty () = equal "ent empty" [||] (entries (unbox<ISessionManager> (createObj [])))

let rasst () =
    let ue = createObj ["message", box (createObj ["role", box "user"; "content", box [| createObj ["type", box "text"; "text", box "u"] |]])]
    let ae = createObj ["message", box (createObj ["role", box "assistant"; "content", box [| createObj ["type", box "text"; "text", box "a1"] |]])]
    let sm = createObj ["getEntries", box (fun () -> [| ue; ae |] :> obj)]
    equal "rasst" "a1" (Option.defaultValue "" (readAssistantText (unbox<ISessionManager> sm) 0 "\n"))

// openTodoStatuses

let otodo () =
    let task = createObj ["status", box"pending"]
    let phase = createObj ["tasks", box [| task |]]
    let e = createObj ["customType", box"todo-phases"; "content", box [| phase |]]
    let sm = createObj ["getEntries", box (fun () -> [| e |] :> obj)]
    let s = openTodoStatuses (unbox<ISessionManager> sm)
    check "todo found" (s.Length > 0)
    equal "pending" "pending" (List.item 0 s)

let otodoEmpty () =
    let e = createObj ["message", box (createObj ["role", box "user"; "content", box [||]])]
    let sm = createObj ["getEntries", box (fun () -> [| e |] :> obj)]
    equal "no todo" [] (openTodoStatuses (unbox<ISessionManager> sm))

// decodeToolState

let dtsFull () =
    let o = createObj ["status", box"done"; "output", box"r"; "error", box""; "input", box"i"]
    match decodeToolState o with None -> check "dts some" false | Some ts -> begin equal "st" "done" ts.status; equal "out" "r" ts.output end

let dtsNull () = check "dts null→None" (decodeToolState null = None)

// extractHistoryTexts

let eht () =
    let msgs = [
        {info=mi(); parts=[TextPart"h"]; source=Native; raw=null}
        {info=mi(); parts=[ToolPart("t","c1",Some{status="";output="to";error="";input=null;operationAction=""},null)]; source=Native; raw=null}
        {info=mi(); parts=[RawPart (createObj [])]; source=Native; raw=null}
    ]
    let ts = extractHistoryTexts msgs
    check "has h" (ts |> List.contains "h")
    check "has to" (ts |> List.contains "to")
    equal "raw→empty" "" (List.item 2 ts)

let run () = promise {
    encText(); encToolStateMatch(); encToolStateMismatch(); encToolNoState(); encRawPart(); encRawSet(); encMsgs()
    decUser(); decAsst(); decInfoFallback(); decNull(); decEmptyRole(); decArr()
    entSM(); entEmpty(); rasst(); otodo(); otodoEmpty(); dtsFull(); dtsNull(); eht()
}

module Wanxiangshu.Runtime.SembleSearchTypes

open Fable.Core
open Fable.Core.JsInterop

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let private envVar (name: string) : string =
    let v = nodeProcess?env?(name)
    if isNull v then "" else string v

[<Import("appendFileSync", "node:fs")>]
let private appendFileSync (path: string) (content: string) (encoding: string) : unit = jsNative

[<Import("tmpdir", "node:os")>]
let private tmpdir () : string = jsNative

type SembleResult =
    { filePath: string
      startLine: int
      endLine: int
      content: string
      score: float
      totalLines: int }

let private debugLogPath () : string =
    let dir = envVar "SEMBLE_INJECT_DEBUG_DIR"
    let baseDir = if dir = "" then tmpdir () else dir
    let ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    let token = System.Guid.NewGuid().ToString("N").[..7]
    $"{baseDir}/wanxiangshu-semble-{ts}-{token}.log"

let debugEnabled () : bool = envVar "SEMBLE_INJECT_DEBUG" = "1"

let trace (tag: string) (detail: string) : unit =
    if not (debugEnabled ()) then
        ()
    else
        let ts = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        let line = $"[semble {ts}] {tag}: {detail}\n"

        try
            appendFileSync (debugLogPath ()) line "utf8"
        with _ ->
            ()

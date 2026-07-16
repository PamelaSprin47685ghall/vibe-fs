module Wanxiangshu.Tests.ArchitectureGatesTests

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Wanxiangshu.Tests.Assert

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private existsSync (path: string) : bool = jsNative

[<Import("readdirSync", "node:fs")>]
let private readdirSync (path: string) : string array = jsNative

[<Import("statSync", "node:fs")>]
let private statSync (path: string) : obj = jsNative

[<Import("join", "node:path")>]
let private pathJoin (path: string) (seg: string) : string = jsNative

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let private isDirectory (path: string) : bool =
    let stat = statSync path
    stat?isDirectory ()

let rec private collectFsFiles (dir: string) : string list =
    if not (existsSync dir) then
        []
    else
        [ for entry in readdirSync dir do
              let full = pathJoin dir entry

              if isDirectory full then
                  yield! collectFsFiles full
              elif full.EndsWith(".fs") then
                  yield full ]

let private lineCount (path: string) : int =
    let content = readFileSync path "utf8"
    content.Split('\n').Length

let private failIf (cond: bool) (msg: string) =
    if cond then
        failwith msg

let private checkProductionLineLimits (srcRoot: string) =
    for path in collectFsFiles srcRoot do
        let n = lineCount path
        failIf (n > 300) (sprintf "production file >300 lines (%d): %s" n path)

let private checkKernelNoFable (kernelRoot: string) =
    let re = Regex(@"\bFable\.Core\b|\bJsInterop\b")

    for path in collectFsFiles kernelRoot do
        let content = readFileSync path "utf8"
        failIf (re.IsMatch content) (sprintf "Kernel must not reference Fable.Core/JsInterop: %s" path)

let private checkKernelNoRuntimeOrHosts (kernelRoot: string) =
    let re =
        Regex(@"open\s+Wanxiangshu\.(Runtime|Hosts)\b|Wanxiangshu\.(Runtime|Hosts)\.")

    for path in collectFsFiles kernelRoot do
        let content = readFileSync path "utf8"
        failIf (re.IsMatch content) (sprintf "Kernel must not reference Runtime/Hosts: %s" path)

let private checkRuntimeNoHosts (runtimeRoot: string) =
    let re = Regex(@"open\s+Wanxiangshu\.Hosts\b|Wanxiangshu\.Hosts\.")

    for path in collectFsFiles runtimeRoot do
        let content = readFileSync path "utf8"
        failIf (re.IsMatch content) (sprintf "Runtime must not reference Hosts: %s" path)

let private checkNoCrossHost (hostsRoot: string) =
    // Host dir name -> forbidden foreign host namespace fragments
    let rules =
        [ "Mux", [ "Hosts.Omp"; "Hosts.OpenCode"; "Hosts.Opencode" ]
          "Omp", [ "Hosts.Mux"; "Hosts.OpenCode"; "Hosts.Opencode" ]
          "OpenCode", [ "Hosts.Mux"; "Hosts.Omp" ] ]

    for hostDir, forbidden in rules do
        let dir = pathJoin hostsRoot hostDir

        if existsSync dir then
            for path in collectFsFiles dir do
                let content = readFileSync path "utf8"

                for frag in forbidden do
                    failIf (content.Contains("Wanxiangshu." + frag)) (sprintf "cross-host reference %s in %s" frag path)

let private checkForbiddenFileNames (srcRoot: string) =
    let catalogRe = Regex(@"Catalog[0-9]+\.fs$")
    let versionRe = Regex(@"(^|/)[^/]*V[0-9]+[^/]*\.fs$")

    for path in collectFsFiles srcRoot do
        let norm = path.Replace("\\", "/")
        let name = norm.Split('/') |> Array.last
        failIf (catalogRe.IsMatch name) (sprintf "forbidden CatalogN filename: %s" path)
        failIf (versionRe.IsMatch norm) (sprintf "forbidden V-number filename: %s" path)

let run () : unit =
    let cwd = unbox<string> (nodeProcess?cwd ())
    let srcRoot = pathJoin cwd "src"
    checkProductionLineLimits srcRoot
    checkKernelNoFable (pathJoin srcRoot "Kernel")
    checkKernelNoRuntimeOrHosts (pathJoin srcRoot "Kernel")
    checkRuntimeNoHosts (pathJoin srcRoot "Runtime")
    checkNoCrossHost (pathJoin srcRoot "Hosts")
    checkForbiddenFileNames srcRoot
    check "architecture gates passed" true

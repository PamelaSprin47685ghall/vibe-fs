module Wanxiangshu.Tests.ArchitectureGatesTests

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureGatesFs

[<Import("join", "node:path")>]
let private pathJoin (path: string) (seg: string) : string = jsNative

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let private violations = ResizeArray<string>()

let private failIf (cond: bool) (msg: string) =
    if cond then
        violations.Add msg

let private lineCount (path: string) : int =
    let content = readFileSync path "utf8"
    content.Split('\n').Length

let private checkProductionLineLimits (srcRoot: string) =
    for path in collectFsFiles srcRoot do
        let n = lineCount path
        failIf (n > 300) (sprintf "production file >300 lines (%d): %s" n path)

// Data directory files may only contain types / literal tables. They must
// not define member-bound functions or `let rec` (no business orchestration).
let private checkDataDirectoryIsDataOnly (root: string) =
    let memberRe = Regex(@"^\s*member\s", RegexOptions.Multiline)
    let letRecRe = Regex(@"^\s*let\s+rec\s", RegexOptions.Multiline)

    for path in collectFsFiles root do
        let content = readFileSync path "utf8"
        failIf (memberRe.IsMatch content) (sprintf "Data file must not contain 'member': %s" path)
        failIf (letRecRe.IsMatch content) (sprintf "Data file must not contain 'let rec': %s" path)

let private functionBodyLineCount (lines: string[]) (bodyStart: int) (headerIndent: int) : int =
    let mutable n = 0
    let mutable i = bodyStart
    let len = lines.Length

    while i < len do
        let line = lines.[i]

        if line.TrimStart() = "" then
            i <- i + 1
        else
            let indent = line.Length - line.TrimStart().Length

            if
                indent <= headerIndent
                && i <> bodyStart
                && (line.TrimStart().StartsWith("let ")
                    || line.TrimStart().StartsWith("member ")
                    || line.TrimStart().StartsWith("and ")
                    || line.TrimStart().StartsWith("type ")
                    || line.TrimStart().StartsWith("module "))
            then
                i <- len
            else
                n <- n + 1
                i <- i + 1

    n

let private findFunctionBodies (path: string) : (int * int) list =
    let content = readFileSync path "utf8"
    let lines = content.Split('\n')
    let len = lines.Length
    let mutable bodies = []
    let mutable i = 0

    while i < len do
        let line = lines.[i]
        let trimmed = line.TrimStart()

        if trimmed.StartsWith("let ") || trimmed.StartsWith("member ") then
            if trimmed.Contains("=") && not (trimmed.EndsWith("=")) then
                let headerIndent = line.Length - line.TrimStart().Length
                let bodyLen = functionBodyLineCount lines (i + 1) headerIndent
                bodies <- (i, bodyLen) :: bodies
                i <- i + 1 + bodyLen
            else
                i <- i + 1
        else
            i <- i + 1

    bodies

let private checkFunctionLengths (root: string) (failLimit: int) =
    for path in collectFsFiles root do
        try
            for (start, bodyLen) in findFunctionBodies path do
                failIf (bodyLen > failLimit) (sprintf "function body >%d lines (%d) at %s:%d" failLimit bodyLen path (start + 1))
        with ex ->
            failIf true (sprintf "function-length parse failed for %s: %s" path ex.Message)

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

let private checkForbiddenFileNames (root: string) =
    let catalogRe = Regex(@"Catalog[0-9]+\.fs$")
    let versionRe = Regex(@"(^|/)[^/]*V[0-9]+[^/]*\.fs$")
    let partRe = Regex(@"(^|/)[^/]*Part[0-9]+\.fs$|(^|/)[^/]*Parts[0-9]+\.fs$")

    for path in collectFsFiles root do
        let norm = path.Replace("\\", "/")
        let name = norm.Split('/') |> Array.last
        failIf (catalogRe.IsMatch name) (sprintf "forbidden CatalogN filename: %s" path)
        failIf (versionRe.IsMatch norm) (sprintf "forbidden V-number filename: %s" path)
        failIf (partRe.IsMatch norm) (sprintf "forbidden PartN/PartsN filename: %s" path)

let private checkForbiddenTestNames (root: string) =
    let forbiddenModules = [ "Phase0"; "CoverageFill"; "Shell" ]
    let re = Regex("(^|/)[^/]*(" + (String.concat "|" forbiddenModules) + ")[^/]*\\.fs$")

    for path in collectFsFiles root do
        let norm = path.Replace("\\", "/")
        failIf (re.IsMatch norm) (sprintf "forbidden legacy test filename (Phase0/CoverageFill/Shell): %s" path)

let run () : unit =
    let cwd = unbox<string> (nodeProcess?cwd ())
    let srcRoot = pathJoin cwd "src"
    let testsRoot = pathJoin cwd "tests"
    let integrationRoot = pathJoin cwd "integration"
    let e2eRoot = pathJoin cwd "e2e"
    violations.Clear()

    checkProductionLineLimits srcRoot

    let kernelDataRoot = pathJoin srcRoot "Kernel/Data"

    if existsSync kernelDataRoot then
        checkDataDirectoryIsDataOnly kernelDataRoot

    checkKernelNoFable (pathJoin srcRoot "Kernel")
    checkKernelNoRuntimeOrHosts (pathJoin srcRoot "Kernel")
    checkRuntimeNoHosts (pathJoin srcRoot "Runtime")
    checkNoCrossHost (pathJoin srcRoot "Hosts")
    checkForbiddenFileNames srcRoot
    checkFunctionLengths srcRoot 60

    for testRoot in [ testsRoot; integrationRoot; e2eRoot ] do
        if existsSync testRoot then
            checkForbiddenFileNames testRoot
            checkForbiddenTestNames testRoot

    if violations.Count > 0 then
        let summary = String.concat "\n" (Seq.cast<string> violations)
        failwith (sprintf "architecture gate violations (%d):\n%s" violations.Count summary)
    else
        check "architecture gates passed" true

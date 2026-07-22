module Wanxiangshu.Tests.ArchitectureGatesSize

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Wanxiangshu.Tests.ArchitectureGatesFs

let private violations = ResizeArray<string>()

let private failIf (cond: bool) (msg: string) =
    if cond then
        violations.Add msg

let private lineCount (path: string) : int =
    let content = readFileSync path "utf8"
    content.Split('\n').Length

let checkProductionLineLimits (srcRoot: string) =
    let mutable over250 = 0
    let mutable in200to250 = 0
    let over200Orchestration = ResizeArray<string>()

    for path in collectFsFiles srcRoot do
        let n = lineCount path
        failIf (n > 300) (sprintf "production file >300 lines (%d): %s" n path)

        if n > 250 then
            over250 <- over250 + 1
        elif n > 200 then
            in200to250 <- in200to250 + 1
            let content = readFileSync path "utf8"

            let hasOrchestration =
                Regex(@"(\blet\s+rec\b|\band\s+\w+\s+->|\bdo!|\blet!)", RegexOptions.Multiline)
                    .IsMatch
                    content

            if hasOrchestration then
                over200Orchestration.Add path

    failIf (over250 > 0) (sprintf "production files >250 lines: %d (must be 0)" over250)
    failIf (in200to250 > 50) (sprintf "production files 200-250 lines: %d (limit 50)" in200to250)

    if over200Orchestration.Count > 10 then
        let listed = String.concat ", " (Seq.cast<string> over200Orchestration)

        failIf
            true
            (sprintf
                "production files >200 lines that look like business orchestration (must be data/protocol): %s"
                listed)

let checkDataDirectoryIsDataOnly (root: string) =
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

let checkFunctionLengths (root: string) (failLimit: int) =
    for path in collectFsFiles root do
        try
            for (start, bodyLen) in findFunctionBodies path do
                if bodyLen > failLimit then
                    failIf
                        (bodyLen > failLimit)
                        (sprintf "function body >%d lines (%d) at %s:%d" failLimit bodyLen path (start + 1))
                elif bodyLen >= 50 then
                    printfn "[function-length warning] %d lines (50-60 warning zone) at %s:%d" bodyLen path (start + 1)
        with ex ->
            failIf true (sprintf "function-length parse failed for %s: %s" path ex.Message)

let run (srcRoot: string) : ResizeArray<string> =
    violations.Clear()
    checkProductionLineLimits srcRoot

    let kernelDataRoot = pathJoin srcRoot "Kernel/Data"

    if existsSync kernelDataRoot then
        checkDataDirectoryIsDataOnly kernelDataRoot

    checkFunctionLengths srcRoot 60
    violations

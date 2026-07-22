module Wanxiangshu.Tests.ArchitectureGatesProject

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Wanxiangshu.Tests.ArchitectureGatesFs

let private violations = ResizeArray<string>()

let private failIf (cond: bool) (msg: string) =
    if cond then
        violations.Add msg

let checkForbiddenFileNames (root: string) =
    let catalogRe = Regex(@"Catalog[0-9]+\.fs$")
    let versionRe = Regex(@"(^|/)[^/]*V[0-9]+[^/]*\.fs$")
    let partRe = Regex(@"(^|/)[^/]*Part[0-9]+\.fs$|(^|/)[^/]*Parts[0-9]+\.fs$")

    for path in collectFsFiles root do
        let norm = path.Replace("\\", "/")
        let name = norm.Split('/') |> Array.last
        failIf (catalogRe.IsMatch name) (sprintf "forbidden CatalogN filename: %s" path)
        failIf (versionRe.IsMatch norm) (sprintf "forbidden V-number filename: %s" path)
        failIf (partRe.IsMatch norm) (sprintf "forbidden PartN/PartsN filename: %s" path)

let checkForbiddenTestNames (root: string) =
    let forbiddenModules = [ "Phase0"; "CoverageFill"; "Shell" ]

    let re =
        Regex("(^|/)[^/]*(" + (String.concat "|" forbiddenModules) + ")[^/]*\\.fs$")

    for path in collectFsFiles root do
        let norm = path.Replace("\\", "/")
        failIf (re.IsMatch norm) (sprintf "forbidden legacy test filename (Phase0/CoverageFill/Shell): %s" path)

let checkFsprojCompileOrder (cwd: string) =
    let fsprojPath = pathJoin cwd "wanxiangshu.fsproj"
    failIf (not (existsSync fsprojPath)) "wanxiangshu.fsproj missing"
    let content = readFileSync fsprojPath "utf8"
    let re = Regex(@"Compile Include=""([^""]+)""")

    let includes =
        [ for m in re.Matches content -> m.Groups.[1].Value.Replace("\\", "/") ]

    let testsRoot = pathJoin cwd "tests"
    let integrationRoot = pathJoin cwd "integration"
    let e2eRoot = pathJoin cwd "e2e"
    let seen = System.Collections.Generic.HashSet<string>()

    for path in includes do
        seen.Add path |> ignore

    for rootDir in [ testsRoot; integrationRoot; e2eRoot ] do
        if existsSync rootDir then
            for path in collectFsFiles rootDir do
                let rel = path.Replace("\\", "/").Replace(cwd.Replace("\\", "/") + "/", "")

                if not (rel.EndsWith("KernelCoverageTests.fs")) then
                    failIf (not (seen.Contains rel)) (sprintf "Test file missing from fsproj: %s" rel)

    let layerOf (path: string) =
        if path.StartsWith "src/Kernel/" then 0
        elif path.StartsWith "src/Runtime/" then 1
        elif path.StartsWith "src/Hosts/" then 2
        elif path.StartsWith "tests/" then 3
        elif path.StartsWith "integration/" then 4
        elif path.StartsWith "e2e/" then 5
        else -1

    let seenOrder = System.Collections.Generic.HashSet<string>()

    for path in includes do
        failIf (not (seenOrder.Add path)) (sprintf "duplicate Compile Include: %s" path)

    let mutable maxProd = -1

    for path in includes do
        let layer = layerOf path

        if layer >= 0 && layer <= 2 then
            failIf
                (layer < maxProd)
                (sprintf "production layer regression at %s (layer %d after %d)" path layer maxProd)

            if layer > maxProd then
                maxProd <- layer

    let mutable leftSrc = false

    for path in includes do
        if path.StartsWith "src/" then
            failIf leftSrc (sprintf "src Compile after test-layer started: %s" path)
        else
            leftSrc <- true

    let hostFiles =
        includes
        |> List.choose (fun p ->
            if p.StartsWith "src/Hosts/" then
                let parts = p.Split('/')
                if parts.Length >= 3 then Some(parts.[2], p) else None
            else
                None)

    let hostOrder = ResizeArray<string>()
    let mutable prevHost: string option = None

    for host, _ in hostFiles do
        match prevHost with
        | Some h when h = host -> ()
        | Some h when hostOrder.Contains host ->
            failIf true (sprintf "Hosts/%s is not contiguous (reappears after other host)" host)
        | _ ->
            hostOrder.Add host
            prevHost <- Some host

    let expectedHosts = [ "Mux"; "OpenCode"; "Omp" ]

    failIf
        (List.ofSeq hostOrder <> expectedHosts)
        (sprintf "Hosts order must be Mux→OpenCode→Omp, got %A" (List.ofSeq hostOrder))

    let integIdx =
        includes
        |> List.mapi (fun i p -> i, p)
        |> List.filter (fun (_, p) -> p.StartsWith "integration/")
        |> List.map fst

    if not (List.isEmpty integIdx) then
        let lo = List.min integIdx
        let hi = List.max integIdx
        failIf (hi - lo + 1 <> integIdx.Length) "integration/ Compile entries must be one contiguous block"

    let srcRoot = pathJoin cwd "src"

    for path in collectFsFiles srcRoot do
        let rel = path.Replace("\\", "/").Replace(cwd.Replace("\\", "/") + "/", "")

        failIf (not (seenOrder.Contains rel)) (sprintf "src file missing from fsproj: %s" rel)

let run
    (cwd: string)
    (srcRoot: string)
    (testsRoot: string)
    (integrationRoot: string)
    (e2eRoot: string)
    : ResizeArray<string> =
    violations.Clear()
    checkForbiddenFileNames srcRoot
    checkFsprojCompileOrder cwd

    for testRoot in [ testsRoot; integrationRoot; e2eRoot ] do
        if existsSync testRoot then
            checkForbiddenFileNames testRoot
            checkForbiddenTestNames testRoot

    violations

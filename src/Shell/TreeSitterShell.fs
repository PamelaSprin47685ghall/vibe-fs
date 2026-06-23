module VibeFs.Shell.TreeSitterShell

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell.Dyn

open VibeFs.Kernel.TreeSitterKernel

let extractFilePaths (args: obj) : string list =
    if isNullish args then []
    else
        let path =
            [ "path"; "file_path"; "filePath" ]
            |> List.tryPick (fun key ->
                let value = get args key
                if isNullish value then None else Some(string value))
        match path with
        | Some p when p <> "" -> [ p ]
        | _ ->
            let patchText = get args "patchText"
            if isNullish patchText then []
            else pathsFromPatchText (string patchText)

let extractFilePath (args: obj) : string option =
    extractFilePaths args |> List.tryHead

type private Position = { row: int; column: int }

[<Import("createRequire", "node:module")>]
let private createRequire (url: string) : (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let private importMetaUrl : string = string importMeta?url

[<Import("default", "highlight.js")>]
let private hljs : {| highlightAuto: string -> {| language: string; relevance: float |} |} = jsNative

let private callOrGet (o: obj) (key: string) (call: unit -> obj) : obj =
    if Dyn.typeIs (Dyn.get o key) "function" then call () else Dyn.get o key

let private getOrCall (o: obj) (key: string) : obj =
    callOrGet o key (fun () -> o?(key)())

let private getOrCallWith (o: obj) (key: string) (arg: obj) : obj =
    callOrGet o key (fun () -> o?(key)(arg))

[<Global("process")>]
let private nodeProcess : obj = jsNative

[<Import("resolve", "node:path")>]
let private pathResolve (cwd: string) (filePath: string) : string = jsNative

let private processPlatform () : string = nodeProcess?platform
let private processArch () : string = nodeProcess?arch

let private platformSuffix (platform: string) (arch: string) : string option =
    match platform, arch with
    | "darwin", "arm64" -> Some "darwin-arm64"
    | "darwin", "x64" -> Some "darwin-x64"
    | "linux", "x64" -> Some "linux-x64-gnu"
    | "linux", "arm64" -> Some "linux-arm64-gnu"
    | "win32", "x64" -> Some "win32-x64-msvc"
    | "win32", "arm64" -> Some "win32-arm64-msvc"
    | _ -> None

let private nodeRequire = createRequire importMetaUrl

let private tryGetPack () : Result<obj, string> =
    let warmPack (pack: obj) =
        try
            getOrCall pack "downloadAll" |> ignore
        with _ -> ()
        pack

    let loadFromRootPackage () =
        try
            Result.Ok (nodeRequire "@kreuzberg/tree-sitter-language-pack" |> warmPack)
        with e ->
            Result.Error $"root package load failed: {e.Message}"

    let loadFromNativePath () =
        match platformSuffix (processPlatform()) (processArch()) with
        | None -> Result.Error "Unsupported platform"
        | Some suffix ->
            try
                let nativePath = nodeRequire?resolve($"@kreuzberg/tree-sitter-language-pack/ts-pack-core-node.{suffix}.node")
                Result.Ok (nodeRequire nativePath |> warmPack)
            with e ->
                Result.Error $"native pack load failed: {e.Message}"

    match loadFromRootPackage () with
    | Result.Ok pack -> Result.Ok pack
    | Result.Error rootError ->
        match loadFromNativePath () with
        | Result.Ok pack -> Result.Ok pack
        | Result.Error nativeError -> Result.Error (rootError + "; " + nativeError)

let private detectLanguage (pack: obj) (content: string) (filePath: string) : string =
    let probePath () : string option =
        try
            let r = getOrCallWith pack "detectLanguageFromPath" filePath
            if isNullish r then None else Some (string r)
        with _ -> None

    let probeContent () : string option =
        try
            let r = getOrCallWith pack "detectLanguageFromContent" content
            if isNullish r then None else Some (string r)
        with _ -> None

    let probeHljs () : string option =
        try
            let hl = hljs.highlightAuto content
            if not (isNullish hl) && not (isNullish hl.language) && hl.relevance >= 5.0 then
                let hasLang = getOrCallWith pack "hasLanguage" hl.language
                if truthy hasLang then Some (string hl.language) else None
            else None
        with _ -> None

    [ probePath; probeContent; probeHljs ]
    |> List.tryPick (fun probe -> probe ())
    |> Option.defaultValue ""

let private nodeField<'T> (node: obj) (key: string) (defaultValue: 'T) : 'T =
    let v = getOrCall node key
    if isNullish v then defaultValue else unbox<'T> v

let private nodeChildCount (node: obj) : int =
    nodeField node "childCount" 0

let private nodeChild (node: obj) (i: int) : obj option =
    let c = getOrCallWith node "child" (box i)
    if isNullish c then None else Some c

let private nodeBool (node: obj) (key: string) : bool =
    getOrCall node key |> truthy

let private nodePosition (node: obj) (key: string) : Position =
    nodeField node key { row = 0; column = 0 }

let private nodeKind (node: obj) (isMissing: bool) : string =
    let v = getOrCall node "kind"
    if isNullish v then (if isMissing then "MISSING" else "ERROR") else string v

let rec private collectDiagnostics (node: obj) (acc: SyntaxDiagnostic list) : SyntaxDiagnostic list * bool =
    let count = nodeChildCount node
    let rec loop i currentAcc hasInner =
        if i >= count then currentAcc, hasInner
        else
            match nodeChild node i with
            | None -> loop (i + 1) currentAcc hasInner
            | Some c ->
                let subAcc, subHasInner = collectDiagnostics c currentAcc
                loop (i + 1) subAcc (hasInner || subHasInner)
    let innerAcc, innerHas = loop 0 acc false
    let isMissing = nodeBool node "isMissing"
    let isError = nodeBool node "isError"
    if isMissing || (isError && not innerHas) then
        let startPos = nodePosition node "startPosition"
        let endPos = nodePosition node "endPosition"
        let kind = nodeKind node isMissing
        let diag =
            { line = startPos.row + 1
              column = startPos.column + 1
              endLine = endPos.row + 1
              endColumn = endPos.column + 1
              severity = "warning"
              message = if isMissing then $"Missing: {kind}" else kind }
        diag :: innerAcc, true
    else
        innerAcc, (isMissing || isError || innerHas)

let checkSyntax (content: string) (filePath: string) : JS.Promise<SyntaxCheckResult> =
    promise {
        match tryGetPack() with
        | Result.Error reason -> return Failed("", reason)
        | Result.Ok pack ->
            let lang = detectLanguage pack content filePath
            if lang = "" then return Ok("", Array.empty)
            else
                let parserResult =
                    try
                        Result.Ok (getOrCallWith pack "getParser" lang)
                    with e ->
                        Result.Error $"parser load failed: {e.Message}"
                match parserResult with
                | Result.Error reason -> return Failed(lang, reason)
                | Result.Ok parser ->
                    let treeResult =
                        try
                            let tree = getOrCallWith parser "parse" content
                            if isNullish tree then Result.Error "parser returned undefined" else Result.Ok tree
                        with e ->
                            Result.Error $"parse failed: {e.Message}"
                    match treeResult with
                    | Result.Error reason -> return Failed(lang, reason)
                    | Result.Ok tree ->
                        let rootNode = getOrCall tree "rootNode"
                        let errors, _ = collectDiagnostics rootNode []
                        return Ok(lang, errors |> List.rev |> Array.ofList)
    }

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

let readAndCheckSyntax (filePath: string) (cwd: string) (includeOk: bool) : JS.Promise<string option> =
    promise {
        try
            let abs = pathResolve cwd filePath
            let! (content: string) = fsPromises?readFile(abs, "utf-8")
            let! result = checkSyntax content filePath
            return formatSyntaxDiagnostics filePath result includeOk
        with ex ->
            return Some $"[tree-sitter] check failed for {filePath}: {ex.Message}"
    }

let appendSyntaxDiagnostics (filePath: string) (content: string) (includeOk: bool)
                            : JS.Promise<string option> =
    promise {
        let! result = checkSyntax content filePath
        return formatSyntaxDiagnostics filePath result includeOk
    }

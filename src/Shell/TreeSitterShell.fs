module VibeFs.Shell.TreeSitterShell

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Shell.Path

type private Position = { row: int; column: int }

[<Import("createRequire", "node:module")>]
let private createRequire (url: string) : (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta : obj = jsNative

let private importMetaUrl : string = string importMeta?url

[<Import("default", "highlight.js")>]
let private hljs : {| highlightAuto: string -> {| language: string; relevance: float |} |} = jsNative

let private getOrCall0 (o: obj) (key: string) : obj =
    if Dyn.typeIs (Dyn.get o key) "function" then o?(key)() else Dyn.get o key

let private getOrCall1 (o: obj) (key: string) (arg: obj) : obj =
    if Dyn.typeIs (Dyn.get o key) "function" then o?(key)(arg) else Dyn.get o key

[<Global("process")>]
let private nodeProcess : obj = jsNative

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
    match platformSuffix (processPlatform()) (processArch()) with
    | None -> Result.Error "Unsupported platform"
    | Some suffix ->
        try
            let nativePath = nodeRequire?resolve($"@kreuzberg/tree-sitter-language-pack/ts-pack-core-node.{suffix}.node")
            let pack = nodeRequire nativePath
            try
                getOrCall0 pack "downloadAll" |> ignore
            with _ -> ()
            Result.Ok pack
        with e ->
            Result.Error $"native pack load failed: {e.Message}"

let private detectLanguage (pack: obj) (content: string) (filePath: string) : string =
    let fromPath =
        try
            let r = getOrCall1 pack "detectLanguageFromPath" filePath
            if isNullish r then "" else string r
        with _ -> ""
    if fromPath <> "" then fromPath
    else
        let fromContent =
            try
                let r = getOrCall1 pack "detectLanguageFromContent" content
                if isNullish r then "" else string r
            with _ -> ""
        if fromContent <> "" then fromContent
        else
            try
                let hl = hljs.highlightAuto content
                if not (isNullish hl) && not (isNullish hl.language) && hl.relevance >= 5.0 then
                    let hasLang = getOrCall1 pack "hasLanguage" hl.language
                    if truthy hasLang then string hl.language else ""
                else ""
            with _ -> ""

let private nodeChildCount (node: obj) : int =
    let v = getOrCall0 node "childCount"
    if isNullish v then 0 else unbox<int> v

let private nodeChild (node: obj) (i: int) : obj option =
    let c = getOrCall1 node "child" (box i)
    if isNullish c then None else Some c

let private nodeBool (node: obj) (key: string) : bool =
    let v = getOrCall0 node key
    truthy v

let private nodePosition (node: obj) (key: string) : Position =
    let v = getOrCall0 node key
    if isNullish v then { row = 0; column = 0 } else unbox<Position> v

let private nodeKind (node: obj) (isMissing: bool) : string =
    let v = getOrCall0 node "kind"
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
    async {
        match tryGetPack() with
        | Result.Error reason -> return Failed("", reason)
        | Result.Ok pack ->
            let lang = detectLanguage pack content filePath
            if lang = "" then return Ok("", Array.empty)
            else
                let parserResult =
                    try
                        Result.Ok (getOrCall1 pack "getParser" lang)
                    with e ->
                        Result.Error $"parser load failed: {e.Message}"
                match parserResult with
                | Result.Error reason -> return Failed(lang, reason)
                | Result.Ok parser ->
                    let treeResult =
                        try
                            let tree = getOrCall1 parser "parse" content
                            if isNullish tree then Result.Error "parser returned undefined" else Result.Ok tree
                        with e ->
                            Result.Error $"parse failed: {e.Message}"
                    match treeResult with
                    | Result.Error reason -> return Failed(lang, reason)
                    | Result.Ok tree ->
                        let rootNode = getOrCall0 tree "rootNode"
                        let errors, _ = collectDiagnostics rootNode []
                        return Ok(lang, errors |> List.rev |> Array.ofList)
    }
    |> Async.StartAsPromise

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> o

let readAndCheckSyntax (filePath: string) (cwd: string) (includeOk: bool) : JS.Promise<string option> =
    async {
        try
            let abs = resolve cwd filePath
            let! content = fsPromises?readFile(abs, "utf-8") |> asPromise<string> |> Async.AwaitPromise
            let! result = checkSyntax content filePath |> Async.AwaitPromise
            return formatSyntaxDiagnostics filePath result includeOk
        with _ -> return None
    }
    |> Async.StartAsPromise

let appendSyntaxDiagnostics (filePath: string) (content: string) (includeOk: bool)
                            : JS.Promise<string option> =
    async {
        let! result = checkSyntax content filePath |> Async.AwaitPromise
        return formatSyntaxDiagnostics filePath result includeOk
    }
    |> Async.StartAsPromise
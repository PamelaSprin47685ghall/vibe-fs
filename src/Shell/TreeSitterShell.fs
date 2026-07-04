module Wanxiangshu.Shell.TreeSitterShell

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.TreeSitterPlatform

open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Kernel.PatchParser

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

let rec private collectAstNodes (node: obj) (acc: AstNodeInfo list) : AstNodeInfo list =
    let startPos = nodePosition node "startPosition"
    let endPos = nodePosition node "endPosition"
    let isMissing = nodeBool node "isMissing"
    let kind = nodeKind node isMissing
    let info = {
        kind = kind
        startLine = startPos.row + 1
        endLine = endPos.row + 1
    }
    let nextAcc = info :: acc
    let count = nodeChildCount node
    let rec loop i currentAcc =
        if i >= count then currentAcc
        else
            match nodeChild node i with
            | None -> loop (i + 1) currentAcc
            | Some c ->
                let subAcc = collectAstNodes c currentAcc
                loop (i + 1) subAcc
    loop 0 nextAcc

let private runGeneralStyleChecks (content: string) : SyntaxDiagnostic[] =
    let lineErrors = checkLineLengths defaultStyleLimits content
    let fileErrors = checkFileLineCount defaultStyleLimits content
    Array.append lineErrors fileErrors

let checkSyntax (content: string) (filePath: string) : JS.Promise<SyntaxCheckResult> =
    promise {
        match tryGetPack() with
        | Result.Error reason -> return Failed("", reason)
        | Result.Ok pack ->
            let lang = detectLanguage pack content filePath
            if lang = "" then
                let styleErrors = runGeneralStyleChecks content
                return Ok("", styleErrors)
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
                        let astErrors = errors |> List.rev |> Array.ofList
                        let astNodes = collectAstNodes rootNode [] |> Array.ofList
                        let styleErrors =
                            Array.concat [
                                checkLineLengths defaultStyleLimits content
                                checkFileLineCount defaultStyleLimits content
                                checkFunctionLengths defaultStyleLimits astNodes
                            ]
                        return Ok(lang, Array.append astErrors styleErrors)
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

module Wanxiangshu.Runtime.TreeSitterDiagnostics

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.TreeSitterPlatform
open Wanxiangshu.Kernel.TreeSitterKernel

let private nodeField<'T> (node: obj) (key: string) (defaultValue: 'T) : 'T =
    let v = getOrCall node key
    if isNullish v then defaultValue else unbox<'T> v

let private nodeChildCount (node: obj) : int = nodeField node "childCount" 0

let private nodeChild (node: obj) (i: int) : obj option =
    let c = getOrCallWith node "child" (box i)
    if isNullish c then None else Some c

let private nodeBool (node: obj) (key: string) : bool = getOrCall node key |> truthy

let private nodePosition (node: obj) (key: string) : Position =
    nodeField node key { row = 0; column = 0 }

let private nodeKind (node: obj) (isMissing: bool) : string =
    let v = getOrCall node "kind"

    if isNullish v then
        (if isMissing then "MISSING" else "ERROR")
    else
        string v

let rec collectDiagnostics (node: obj) (acc: SyntaxDiagnostic list) : SyntaxDiagnostic list * bool =
    let count = nodeChildCount node

    let rec loop i currentAcc hasInner =
        if i >= count then
            currentAcc, hasInner
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

let rec collectAstNodes (node: obj) (acc: AstNodeInfo list) : AstNodeInfo list =
    let startPos = nodePosition node "startPosition"
    let endPos = nodePosition node "endPosition"
    let isMissing = nodeBool node "isMissing"
    let kind = nodeKind node isMissing

    let info =
        { kind = kind
          startLine = startPos.row + 1
          endLine = endPos.row + 1 }

    let nextAcc = info :: acc
    let count = nodeChildCount node

    let rec loop i currentAcc =
        if i >= count then
            currentAcc
        else
            match nodeChild node i with
            | None -> loop (i + 1) currentAcc
            | Some c ->
                let subAcc = collectAstNodes c currentAcc
                loop (i + 1) subAcc

    loop 0 nextAcc

let runGeneralStyleChecks (content: string) : SyntaxDiagnostic[] = [||]

let isExcludedPath (filePath: string) : bool =
    let lowerPath =
        if System.String.IsNullOrEmpty filePath then
            ""
        else
            filePath.ToLowerInvariant()

    lowerPath.EndsWith(".md")
    || lowerPath.EndsWith(".markdown")
    || lowerPath.EndsWith(".txt")

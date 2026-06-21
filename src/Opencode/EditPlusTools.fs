module VibeFs.Opencode.EditPlusTools

open System
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Opencode.EditPlusState
open VibeFs.Opencode.ToolSchema
open VibeFs.Shell.TreeSitterShell

[<Import("promises", "node:fs")>]
let private fsPromises : obj = jsNative

[<Import("resolve", "node:path")>]
let private pathResolve (cwd: string) (filePath: string) : string = jsNative

[<Global("process")>]
let private nodeProcess : obj = jsNative

let private MAX_READ_SIZE = 50 * 1024 * 1024

let private sizeStr (b: float) : string =
    if b >= 1e9 then (b / 1e9).ToString("F1") + "G"
    elif b >= 1e6 then (b / 1e6).ToString("F1") + "M"
    elif b >= 1024.0 then (b / 1024.0).ToString("F1") + "K"
    else string (int b) + "B"

type private LoadedFile =
    { path: string
      mtimeMs: float
      wholeContent: string
      lines: string[] }

let private inspectPath (input: string) (cwd: string) : JS.Promise<Result<string * string option, string>> =
    promise {
        let resolved = if input.StartsWith("/") then input else pathResolve (if cwd = "" then unbox<string> (nodeProcess?cwd()) else cwd) input
        try
            let! st = fsPromises?stat(resolved)
            if unbox<bool> (st?isFile()) then return Result.Ok(resolved, None)
            elif unbox<bool> (st?isDirectory()) then return Result.Ok(resolved, Some "isDirectory")
            else return Result.Ok(resolved, None)
        with _ ->
            return Result.Error ""
    }

let private readFileRaw (path: string) : JS.Promise<LoadedFile> =
    promise {
        let! (st: obj) = fsPromises?stat(path)
        let size = unbox<float> (st?size)
        if size > float MAX_READ_SIZE then
            return! Promise.reject(exn $"File too large ({size / 1024.0 / 1024.0:F1}MB). Maximum read size is {MAX_READ_SIZE / 1024 / 1024}MB.")
        let! content = fsPromises?readFile(path, "utf-8")
        let mtimeMs = unbox<float> (st?mtimeMs)
        return { path = path; mtimeMs = mtimeMs; wholeContent = string content; lines = splitLines (string content) }
    }

let private generateFileListing (dir: string) : JS.Promise<string> =
    promise {
        try
            let! entries = fsPromises?readdir(dir, {| withFileTypes = true |})
            let arr = unbox<obj[]> entries
            let! lines =
                arr
                |> Array.map (fun entry ->
                    promise {
                        let name = unbox<string> (entry?name)
                        let fp = pathResolve dir name
                        try
                            let! st = fsPromises?stat(fp)
                            let isDir = unbox<bool> (st?isDirectory())
                            let sz = if isDir then 0.0 else unbox<float> (st?size)
                            let suffix = if isDir then "/" else ""
                            return $"{(sizeStr sz).PadLeft(8)}  {name}{suffix}"
                        with _ ->
                            return $"         {name}"
                    })
                |> Promise.all
            return String.concat "\n" (Array.toList lines)
        with _ -> return ""
    }

let private detectStructure (filePath: string) (content: string) : JS.Promise<int[] option> =
    promise {
        try
            let! result = checkSyntax content filePath
            match result with
            | Ok _ | Failed _ -> return None
        with _ -> return None
    }

let private loadFile (state: EditPlusState) (path: string) (cwd: string) (failOnExt: bool) : JS.Promise<EpResult<LoadedFile * int[] option>> =
    promise {
        let! inspect = inspectPath path cwd
        match inspect with
        | Result.Error _ ->
            return EpError($"{path} does not exist.", None, None)
        | Result.Ok(resolvedPath, Some "isDirectory") ->
            let! listing = generateFileListing resolvedPath
            return EpError($"$ du -hxd1\n{listing}", None, Some "isDirectory")
        | Result.Ok(resolvedPath, _) ->
            try
                let! file = readFileRaw resolvedPath
                if state.Registry.MtimeChanged(resolvedPath, file.mtimeMs) then
                    state.Registry.RemoveFile(resolvedPath)
                    if failOnExt then
                        return EpError("File changed outside editplus. Re-read the full file before reading a tag range.", None, Some "EXTERNAL_CHANGE")
                    else
                        state.Registry.NoteMtime(resolvedPath, file.mtimeMs)
                        let! struct_ = detectStructure file.path file.wholeContent
                        return EpOk(file, struct_)
                else
                    state.Registry.NoteMtime(resolvedPath, file.mtimeMs)
                    let! struct_ = detectStructure file.path file.wholeContent
                    return EpOk(file, struct_)
            with ex ->
                return EpError($"Failed to read {path}: {ex.Message}", None, None)
    }

let private renderFileSummary (state: EditPlusState) (file: LoadedFile) (struct_: int[] option) (params': obj) : string option =
    if not (state.Registry.HasFile(file.path)) then
        state.Registry.Assign(file.path, 0, file.lines.Length + 1) |> ignore
    let cursor = state.Registry.CreateCursor(file.path)
    let getS (i: int) : string =
        match cursor with
        | Some c ->
            match c.TagForLine(i) with
            | Some t -> numToTag t
            | None -> ""
        | None ->
            match state.Registry.TagForLine(file.path, i) with
            | Some t -> numToTag t
            | None -> ""
    if file.lines.Length = 0 then
        Some($"{getS(0)}|\n")
    else
        let range = readRange state.Registry params' file.path file.lines.Length struct_
        match range with
        | EpError _ -> None
        | EpOk r ->
            let linesWithEnd = Array.append file.lines [|""|]
            let idxes =
                match r.indexes with
                | Some idx -> Array.append idx [|file.lines.Length|]
                | None ->
                    let arr = ResizeArray<int>()
                    let limit = if isNullish (Dyn.get params' "begin") then linesWithEnd.Length else r.toLine
                    for i = r.fromLine to limit - 1 do arr.Add(i)
                    arr.ToArray()
            let text = formatTag getS linesWithEnd idxes
            match r.indexes with
            | Some _ -> Some($"{r.heading}\n\n{text}\n\n{r.hint}")
            | None -> Some(text)

let private appendSummary (state: EditPlusState) (path: string) (errorMsg: string) (projectDir: string) (preloaded: (LoadedFile * int[] option) option) : JS.Promise<EpResult<string>> =
    promise {
        if path = "" then return EpError(errorMsg, None, None)
        else
            let! loaded =
                match preloaded with
                | Some f -> Promise.lift (EpOk f : EpResult<LoadedFile * int[] option>)
                | None -> loadFile state (stripAt path) projectDir false
            match loaded with
            | EpError(e, p, c) -> return EpError(errorMsg, p, c)
            | EpOk(file, struct_) ->
                let summary = renderFileSummary state file struct_ (createObj ["path", box path])
                match summary with
                | Some s -> return EpError($"{errorMsg}\n\n--- Auto-attached current file summary ---\n{s}", None, None)
                | None -> return EpError(errorMsg, None, None)
    }

let private resolveReadPath (state: EditPlusState) (params': obj) : EpResult<unit> =
    let pathVal = Dyn.get params' "path"
    if not (isNullish pathVal) then EpOk ()
    else
        let s = Dyn.get params' "begin"
        if isNullish s then
            params'?("path") <- box "."
            EpOk ()
        else
            let num = if Dyn.typeIs s "string" then tagToNum (string s) else unbox<int> s
            match state.Registry.Resolve(num) with
            | TagValid(resolvedPath, _) ->
                params'?("path") <- box resolvedPath
                EpOk ()
            | _ -> EpError("Invalid or expired tag provided.", None, None)

let handleRead (state: EditPlusState) (params': obj) (cwd: string) : JS.Promise<EpResult<string>> =
    promise {
        match resolveReadPath state params' with
        | EpError(e, p, c) -> return EpError(e, p, c)
        | EpOk () ->
            let path = stripAt (Dyn.str params' "path")
            let! file = loadFile state path cwd (not (isNullish (Dyn.get params' "begin")))
            match file with
            | EpError(e, _, Some "EXTERNAL_CHANGE") ->
                let! retry = loadFile state path cwd false
                match retry with
                | EpError(e2, p2, c2) -> return EpError(e2, p2, c2)
                | EpOk(f2, s2) ->
                    match renderFileSummary state f2 s2 params' with
                    | Some s -> return EpOk s
                    | None -> return! appendSummary state (Dyn.str params' "path") e cwd (Some(f2, s2))
            | EpError(e, _, Some "isDirectory") -> return EpError(e, None, Some "isDirectory")
            | EpError(e, p2, c2) -> return EpError(e, p2, c2)
            | EpOk(f, s) ->
                match renderFileSummary state f s params' with
                | Some summary -> return EpOk summary
                | None ->
                    let rangeErr = match readRange state.Registry params' f.path f.lines.Length s with EpError(e,_,_) -> e | _ -> "Range error"
                    return! appendSummary state (Dyn.str params' "path") rangeErr cwd (Some(f, s))
    }

type private EditBounds = { bPath: string; bLine: int; ePath: string; eLine: int }

let private resolveEditBounds (state: EditPlusState) (params': obj) (cwd: string) : JS.Promise<Result<EditBounds, EpResult<string>>> =
    promise {
        let beginRes = resolveTag state.Registry (Dyn.get params' "begin") "editing" "begin tag"
        let endRes : EpResult<{| path: string; line: int |}> =
            let endInc = Dyn.get params' "endInclusive"
            if not (isNullish endInc) then
                match resolveTag state.Registry endInc "editing" "endInclusive tag" with
                | EpOk e -> EpOk {| path = e.path; line = e.line + 1 |}
                | err -> err
            else
                resolveTag state.Registry (Dyn.get params' "endExclusive") "editing" "endExclusive tag"
        match beginRes, endRes with
        | EpError(e, p, c), _ ->
            match p with
            | Some path ->
                let! r = appendSummary state path e cwd None
                return Result.Error r
            | None -> return Result.Error(EpError(e, p, c))
        | _, EpError(e, p, c) ->
            match p with
            | Some path ->
                let! r = appendSummary state path e cwd None
                return Result.Error r
            | None -> return Result.Error(EpError(e, p, c))
        | EpOk b, EpOk e ->
            if b.path <> e.path then
                let! r = appendSummary state b.path "Tag range spans multiple files." cwd None
                return Result.Error r
            elif e.line < b.line then
                let! r = appendSummary state b.path "Tag range is reversed." cwd None
                return Result.Error r
            else
                return Result.Ok { bPath = b.path; bLine = b.line; ePath = e.path; eLine = e.line }
    }

let private buildDiffOutput (oldLines: string[]) (newLines: string[]) (b: int) (e: int) (ins: string[]) : string =
    let diffLines = ResizeArray<string>()
    let maxLen = max oldLines.Length newLines.Length
    let widthStr = string maxLen
    let w = widthStr.Length
    let pad (n: int) = (string n).PadLeft(w)
    let strip (s: string) = s.TrimEnd('\r', '\n')
    let sc = max 0 (b - 4)
    let ec = min oldLines.Length (e + 4)
    if sc > 0 then diffLines.Add(" " + "".PadLeft(w) + " ...")
    for i = sc to b - 1 do
        diffLines.Add(" " + pad(i + 1) + " " + strip oldLines.[i])
    for i = b to e - 1 do
        diffLines.Add("-" + pad(i + 1) + " " + strip oldLines.[i])
    for i = 0 to ins.Length - 1 do
        diffLines.Add("+" + pad(b + i + 1) + " " + strip ins.[i])
    let mutable o = b + ins.Length
    for i = e to ec - 1 do
        diffLines.Add(" " + pad(o + 1) + " " + strip oldLines.[i])
        o <- o + 1
    if ec < oldLines.Length then diffLines.Add(" " + "".PadLeft(w) + " ...")
    String.concat "\n" diffLines

let private applyEdit (state: EditPlusState) (params': obj) (b: EditBounds) (cwd: string) (endExc: obj) (beginTag: obj) (beginNum: int) : JS.Promise<EpResult<string>> =
    promise {
        try
            let! file = readFileRaw b.bPath
            if state.Registry.MtimeChanged(b.bPath, file.mtimeMs) then
                let! struct_ = detectStructure file.path file.wholeContent
                return! appendSummary state b.bPath "File changed outside editplus." cwd (Some(file, struct_))
            else
                let content = Dyn.str params' "content"
                let ins =
                    if content = "" then [||]
                    else
                        let ending =
                            if b.bLine < file.lines.Length then
                                let bl = file.lines.[b.bLine]
                                if bl.EndsWith("\r\n") then "\r\n"
                                elif bl.EndsWith("\n") then "\n"
                                elif bl.EndsWith("\r") then "\r"
                                else ""
                            else ""
                        let c = if System.Text.RegularExpressions.Regex.IsMatch(content, @"[\n\r]$") then content else content + (if ending = "" then "\n" else ending)
                        splitLines c
                let newLines = Array.concat [file.lines.[0..b.bLine - 1]; ins; file.lines.[b.eLine..]]
                do! fsPromises?writeFile(b.bPath, newLines |> String.concat "", "utf-8")
                try
                    let! updated = readFileRaw b.bPath
                    state.Registry.NoteMtime(b.bPath, updated.mtimeMs)
                with _ -> ()
                let newTags = state.Registry.Edit(b.bPath, b.bLine, b.eLine, ins.Length)
                let dispEnd =
                    if not (isNullish endExc) then
                        if Dyn.typeIs endExc "string" then string endExc
                        else numToTag (unbox<int> endExc)
                    else
                        match state.Registry.TagForLine(b.bPath, b.eLine) with
                        | Some t -> numToTag t
                        | None -> ""
                let beginDisp = if Dyn.typeIs beginTag "string" then string beginTag else numToTag beginNum
                let tagsList = newTags |> Array.map numToTag |> String.concat ", "
                let tagsMsg = if newTags.Length > 0 then " New tags: " + tagsList + "." else ""
                let diff = buildDiffOutput file.lines newLines b.bLine b.eLine ins
                let msg = "Edited " + b.bPath + " at [" + beginDisp + ", " + dispEnd + ")." + tagsMsg + "\n\n" + diff
                return EpOk(msg)
        with ex ->
            return EpError("Edit failed: " + ex.Message, None, None)
    }

let handleEdit (state: EditPlusState) (params': obj) (cwd: string) : JS.Promise<EpResult<string>> =
    promise {
        let beginTag = Dyn.get params' "begin"
        if isNullish beginTag then return EpError("begin is required.", None, None)
        else
            let endExc = Dyn.get params' "endExclusive"
            let endInc = Dyn.get params' "endInclusive"
            if isNullish endExc && isNullish endInc then
                return EpError("Either endExclusive or endInclusive is required.", None, None)
            elif not (isNullish endExc) && not (isNullish endInc) then
                return EpError("Provide either endExclusive or endInclusive, not both.", None, None)
            elif isNullish (Dyn.get params' "content") then
                return EpError("content is required.", None, None)
            else
                let beginNum = if Dyn.typeIs beginTag "string" then tagToNum (string beginTag) else unbox<int> beginTag
                let pathEntry = state.Registry.Resolve(beginNum)
                match pathEntry with
                | TagValid(path, _) ->
                    let! bounds = resolveEditBounds state params' cwd
                    match bounds with
                    | Result.Error r -> return r
                    | Result.Ok b ->
                        let! editResult = state.Locks.WithLock(path, 30000, fun () ->
                            applyEdit state params' b cwd endExc beginTag beginNum)
                        return editResult
                | _ ->
                    let! bounds = resolveEditBounds state params' cwd
                    match bounds with
                    | Result.Error r -> return r
                    | Result.Ok _ -> return EpError("Edit failed: tag not found.", None, None)
    }

let editPlusReadTool (state: EditPlusState) : obj =
    define "Read a file with stable tag-based line addressing. Tags persist across reads and edits within a session. Use begin/endExclusive or begin/endInclusive to read a specific range. Without a range, returns a structural summary for large files or the full tagged view for small files."
        (box {| path = strOpt "File path or directory to read."
                begin_ = strOpt "Inclusive start tag."
                endExclusive = strOpt "Exclusive end tag. The line at this tag is omitted."
                endInclusive = strOpt "Inclusive end tag. Mutually exclusive with endExclusive." |})
        (fun args context ->
            promise {
                let! result = handleRead state args (Dyn.str context "directory")
                match result with
                | EpOk s -> return s
                | EpError(e, _, _) -> return e
            })

let editPlusEditTool (state: EditPlusState) : obj =
    define "Edit a file by tag range. begin is required. Use endExclusive to preserve the end line, or endInclusive to replace it. content is the replacement text; empty content means deletion. begin == endExclusive means insertion. Tags are invalidated for edited lines; unedited lines keep their tags."
        (box {| begin_ = strReq "Inclusive start tag."
                endExclusive = strOpt "Exclusive end tag. The line at this tag is PRESERVED."
                endInclusive = strOpt "Inclusive end tag. This line IS REPLACED."
                content = strReq "Replacement text." |})
        (fun args context ->
            promise {
                let mapped = createObj [
                    "begin", Dyn.get args "begin_"
                    "endExclusive", Dyn.get args "endExclusive"
                    "endInclusive", Dyn.get args "endInclusive"
                    "content", Dyn.get args "content"
                ]
                let! result = handleEdit state mapped (Dyn.str context "directory")
                match result with
                | EpOk s -> return s
                | EpError(e, _, _) -> return e
            })

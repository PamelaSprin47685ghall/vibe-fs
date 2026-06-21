module VibeFs.Opencode.EditPlusState

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Shell.PromiseQueue

let numToTag (n: int) : string =
    let mutable n = n
    let mutable res = ""
    while n > 0 do
        let off = (n - 1) % 52
        let c = if off < 26 then char (65 + off) else char (71 + off)
        res <- string c + res
        n <- (n - 1) / 52
    res

let tagToNum (s: string) : int =
    let mutable res = 0
    let mutable valid = true
    for c in s do
        let code = int c
        if code >= 65 && code <= 90 then res <- res * 52 + code - 64
        elif code >= 97 && code <= 122 then res <- res * 52 + code - 70
        else valid <- false
    if valid then res else -1

let splitLines (text: string) : string[] =
    let res = ResizeArray<string>()
    let mutable start = 0
    let mutable i = 0
    while i < text.Length do
        let c = text.[i]
        if c = '\n' || (c = '\r' && (i + 1 >= text.Length || text.[i + 1] <> '\n')) then
            res.Add(text.[start..i])
            start <- i + 1
        elif c = '\r' && i + 1 < text.Length && text.[i + 1] = '\n' then
            res.Add(text.[start..i + 1])
            i <- i + 1
            start <- i + 1
        i <- i + 1
    if start < text.Length then res.Add(text.[start..])
    res.ToArray()

let stripAt (p: string) : string = if p.StartsWith("@") then p.[1..] else p

type EpResult<'T> =
    | EpOk of 'T
    | EpError of error: string * path: string option * code: string option

[<AllowNullLiteral>]
type Segment(x: int, z: int, len: int) =
    member val X = x with get, set
    member val Z = z with get, set
    member val Len = len with get, set

type FileState() =
    member val ZList = ResizeArray<Segment>()
    member val XList = ResizeArray<Segment>()
    member val LastZ = 0 with get, set
    member val LastX = 0 with get, set

type TagCursor(state: FileState) =
    let zList = state.ZList
    let mutable zIndex = 0
    member _.TagForLine(line: int) : int option =
        if zList.Count = 0 then
            None
        else
            let mutable i = min zIndex (zList.Count - 1)
            while i > 0 && line < zList.[i].Z do
                i <- i - 1
            while i < zList.Count - 1 && line >= zList.[i + 1].Z do
                i <- i + 1
            zIndex <- i
            let seg = zList.[i]
            if line >= seg.Z && line < seg.Z + seg.Len then
                Some(seg.X + (line - seg.Z))
            else
                None

type TagEntry =
    | TagNotFound
    | TagExpired
    | TagStale of path: string * isExternal: bool
    | TagValid of path: string * line: int

type Allocation = { X: int; Count: int; Path: string }

let private classifyZSegments (zList: ResizeArray<Segment>) (lo: int) (hi: int) (delta: int) =
    let left = ResizeArray<Segment>()
    let right = ResizeArray<Segment>()
    let splits = Dictionary<Segment, Segment>()
    let drops = HashSet<Segment>()
    for seg in zList do
        let segEnd = seg.Z + seg.Len
        if segEnd <= lo then
            left.Add(seg)
        elif seg.Z >= hi then
            seg.Z <- seg.Z + delta
            right.Add(seg)
        elif seg.Z < lo && segEnd > hi then
            let rightSeg = Segment(seg.X + (hi - seg.Z), hi + delta, segEnd - hi)
            seg.Len <- lo - seg.Z
            left.Add(seg)
            right.Add(rightSeg)
            splits.[seg] <- rightSeg
        elif seg.Z < lo then
            seg.Len <- lo - seg.Z
            left.Add(seg)
        elif segEnd > hi then
            seg.X <- seg.X + (hi - seg.Z)
            seg.Len <- segEnd - hi
            seg.Z <- hi + delta
            right.Add(seg)
        else
            drops.Add(seg) |> ignore
    left, right, splits, drops

let private mergeXList (xList: ResizeArray<Segment>) (drops: HashSet<Segment>) (splits: Dictionary<Segment, Segment>) (midSeg: Segment option) =
    let baseList = ResizeArray<Segment>()
    let inserts = ResizeArray<Segment>()
    for seg in xList do
        if not (drops.Contains(seg)) then
            baseList.Add(seg)
            match splits.TryGetValue(seg) with
            | true, rightSeg -> inserts.Add(rightSeg)
            | false, _ -> ()
    match midSeg with
    | Some s -> inserts.Add(s)
    | None -> ()
    let merged = ResizeArray<Segment>()
    let mutable bi = 0
    let mutable ii = 0
    while bi < baseList.Count && ii < inserts.Count do
        if baseList.[bi].X <= inserts.[ii].X then
            merged.Add(baseList.[bi])
            bi <- bi + 1
        else
            merged.Add(inserts.[ii])
            ii <- ii + 1
    while bi < baseList.Count do
        merged.Add(baseList.[bi])
        bi <- bi + 1
    while ii < inserts.Count do
        merged.Add(inserts.[ii])
        ii <- ii + 1
    merged

type TagRegistry() =
    let mutable nextTag = 1
    let states = Dictionary<string, FileState>()
    let mtimes = Dictionary<string, float>()
    let allocations = ResizeArray<Allocation>()
    let mutable minAllocatedTag = 1
    let cleared = Dictionary<string, int>()

    member _.NextTag = nextTag
    member _.MinAllocatedTag = minAllocatedTag

    member _.HasFile(path: string) = states.ContainsKey(path)

    member _.RemoveFile(path: string) =
        states.Remove(path) |> ignore
        mtimes.Remove(path) |> ignore
        cleared.[path] <- nextTag
        if cleared.Count > 100000 then
            let keys = Seq.toArray cleared.Keys
            for i = 0 to min (keys.Length - 1) 49999 do
                cleared.Remove(keys.[i]) |> ignore

    member _.Assign(path: string, line: int, count: int) : int[] =
        if count <= 0 then
            [||]
        else
            let state =
                match states.TryGetValue(path) with
                | true, s -> s
                | false, _ ->
                    let s = FileState()
                    states.[path] <- s
                    s
            let start = nextTag
            nextTag <- nextTag + count
            let seg = Segment(start, line, count)
            state.XList.Add(seg)
            let mutable inserted = false
            for i = 0 to state.ZList.Count - 1 do
                if not inserted && state.ZList.[i].Z > seg.Z then
                    state.ZList.Insert(i, seg)
                    inserted <- true
            if not inserted then state.ZList.Add(seg)
            allocations.Add({ X = start; Count = count; Path = path })
            if allocations.Count > 1000000 then
                let kept = allocations |> Seq.skip (allocations.Count - 500000) |> Seq.toArray
                allocations.Clear()
                for a in kept do allocations.Add(a)
                minAllocatedTag <- allocations.[0].X
            Array.init count (fun i -> start + i)

    member _.TagForLine(path: string, line: int) : int option =
        match states.TryGetValue(path) with
        | false, _ -> None
        | true, state when state.ZList.Count = 0 -> None
        | true, state ->
            let zList = state.ZList
            let mutable i = min state.LastZ (zList.Count - 1)
            while i > 0 && line < zList.[i].Z do
                i <- i - 1
            while i < zList.Count - 1 && line >= zList.[i + 1].Z do
                i <- i + 1
            state.LastZ <- i
            let seg = zList.[i]
            if line >= seg.Z && line < seg.Z + seg.Len then
                Some(seg.X + (line - seg.Z))
            else
                None

    member _.CreateCursor(path: string) : TagCursor option =
        match states.TryGetValue(path) with
        | false, _ -> None
        | true, state ->
            Some(TagCursor(state))

    member private this.GetPathForTag(tag: int) : string option =
        if allocations.Count = 0 then
            None
        else
            let mutable low = 0
            let mutable high = allocations.Count - 1
            let mutable found = None
            while low <= high do
                let mid = (low + high) >>> 1
                let alloc = allocations.[mid]
                if tag < alloc.X then
                    high <- mid - 1
                elif tag >= alloc.X + alloc.Count then
                    low <- mid + 1
                else
                    found <- Some alloc.Path
                    low <- mid + 1
            found

    member this.Resolve(tag: int) : TagEntry =
        match this.GetPathForTag tag with
        | None -> TagNotFound
        | Some path ->
            if minAllocatedTag > 1 && tag < minAllocatedTag then
                TagExpired
            else
                match cleared.TryGetValue(path) with
                | true, clearedAt when tag < clearedAt -> TagStale(path, true)
                | _ ->
                    match states.TryGetValue(path) with
                    | false, _ -> TagNotFound
                    | true, state when state.XList.Count = 0 -> TagNotFound
                    | true, state ->
                        let xList = state.XList
                        let mutable i = min state.LastX (xList.Count - 1)
                        while i > 0 && tag < xList.[i].X do
                            i <- i - 1
                        while i < xList.Count - 1 && tag >= xList.[i + 1].X do
                            i <- i + 1
                        state.LastX <- i
                        let seg = xList.[i]
                        if tag >= seg.X && tag < seg.X + seg.Len then
                            TagValid(path, seg.Z + (tag - seg.X))
                        else
                            TagStale(path, false)

    member _.Edit(path: string, lo: int, hi: int, insertedLineCount: int) : int[] =
        match states.TryGetValue(path) with
        | false, _ -> [||]
        | true, state ->
            let delta = insertedLineCount - (hi - lo)
            let left, right, splits, drops = classifyZSegments state.ZList lo hi delta
            let mutable newTags = [||]
            let mutable midSeg = None
            if insertedLineCount > 0 then
                let start = nextTag
                nextTag <- nextTag + insertedLineCount
                let seg = Segment(start, lo, insertedLineCount)
                allocations.Add({ X = start; Count = insertedLineCount; Path = path })
                if allocations.Count > 1000000 then
                    let kept = allocations |> Seq.skip (allocations.Count - 500000) |> Seq.toArray
                    allocations.Clear()
                    for a in kept do allocations.Add(a)
                    minAllocatedTag <- allocations.[0].X
                midSeg <- Some seg
                newTags <- Array.init insertedLineCount (fun i -> start + i)
            state.ZList.Clear()
            for s in left do state.ZList.Add(s)
            match midSeg with
            | Some s -> state.ZList.Add(s)
            | None -> ()
            for s in right do state.ZList.Add(s)
            let merged = mergeXList state.XList drops splits midSeg
            state.XList.Clear()
            for s in merged do state.XList.Add(s)
            state.LastZ <- 0
            state.LastX <- 0
            newTags

    member _.NoteMtime(path: string, mtimeMs: float) : unit = mtimes.[path] <- mtimeMs

    member _.MtimeChanged(path: string, mtimeMs: float) : bool =
        match mtimes.TryGetValue(path) with
        | true, oldMs -> abs(oldMs - mtimeMs) > 100.0
        | false, _ -> false

    member _.Reset() =
        nextTag <- 1
        states.Clear()
        mtimes.Clear()
        allocations.Clear()
        minAllocatedTag <- 1
        cleared.Clear()

let resolveTag (registry: TagRegistry) (tag: obj) (action: string) (role: string) : EpResult<{| path: string; line: int |}> =
    let isNumericString (s: string) =
        let t = s.Trim()
        t.Length > 0 && t |> Seq.forall (fun c -> c >= '0' && c <= '9')
    match tag with
    | :? string as s when isNumericString s ->
        EpError("Raw numeric tags are not allowed.", None, None)
    | _ ->
        let num, disp =
            match tag with
            | :? string as s -> tagToNum s, s
            | _ ->
                let n = unbox<int> tag
                n, numToTag n
        if num < 0 then
            EpError($"{role} {disp} is not a valid tag.", None, None)
        else
            match registry.Resolve(num) with
            | TagNotFound -> EpError($"{role} {disp} does not exist. Re-read the file and copy a current tag.", None, None)
            | TagExpired -> EpError($"{role} {disp} has expired (allocations exceeded). Re-read the file and copy a current tag.", None, None)
            | TagStale(path, true) -> EpError($"File changed outside editplus since {role} {disp} was generated. Re-read the file before {action}.", Some path, Some "EXTERNAL_CHANGE")
            | TagStale(path, false) -> EpError($"{role} {disp} is stale (line was edited or deleted). Re-read the file before {action}.", Some path, None)
            | TagValid(path, line) -> EpOk {| path = path; line = line |}

type ReadRangeResult =
    { fromLine: int
      toLine: int
      indexes: int[] option
      heading: string
      hint: string }

let readRange (registry: TagRegistry) (params': obj) (filePath: string) (lineCount: int) (structure: int[] option) : EpResult<ReadRangeResult> =
    let beginTag = Dyn.get params' "begin"
    if not (isNullish beginTag) then
        let beginRes = resolveTag registry beginTag "reading" "begin tag"
        match beginRes with
        | EpError(e, p, c) -> EpError(e, p, c)
        | EpOk b ->
            let endRes : EpResult<{| path: string; line: int |}> =
                let endInc = Dyn.get params' "endInclusive"
                if not (isNullish endInc) then
                    match resolveTag registry endInc "reading" "endInclusive tag" with
                    | EpOk e -> EpOk {| path = e.path; line = e.line + 1 |}
                    | err -> err
                else
                    let endExc = Dyn.get params' "endExclusive"
                    if not (isNullish endExc) then
                        resolveTag registry endExc "reading" "endExclusive tag"
                    else
                        EpOk {| path = b.path; line = b.line + 1 |}
            match endRes with
            | EpError(e, p, c) -> EpError(e, p, c)
            | EpOk e ->
                if b.path <> filePath || e.path <> filePath then
                    EpError("Requested tag range does not belong to this path.", None, None)
                elif e.line < b.line then
                    EpError("Tag range is reversed.", None, None)
                else
                    EpOk { fromLine = b.line; toLine = e.line; indexes = None; heading = ""; hint = "" }
    else
        let shown = HashSet<int>([0; 1; 2; max 0 (lineCount - 2); max 0 (lineCount - 1)])
        match structure with
        | Some s -> for l in s do shown.Add(l) |> ignore
        | None -> ()
        let indexes =
            match structure with
            | None | Some [||] -> None
            | Some _ when lineCount <= 80 -> None
            | Some s ->
                shown
                |> Seq.filter (fun l -> l >= 0 && l < lineCount)
                |> Seq.sort
                |> Seq.toArray
                |> Some
        let hint =
            match structure with
            | Some s when s.Length > 0 -> "This is a structural summary. Use begin/endExclusive tags to read an exact range."
            | _ -> "Use begin/endExclusive tags to read an exact range."
        EpOk { fromLine = 0; toLine = lineCount; indexes = indexes
               heading = $"Summary for {filePath}"
               hint = hint }

let formatTag (getTag: int -> string) (lines: string[]) (indexes: int[]) : string =
    let labels = indexes |> Array.map getTag
    let width = if labels.Length = 0 then 1 else labels |> Array.map (fun s -> s.Length) |> Array.max
    let padLeft (s: string) (w: int) = s.PadLeft(w)
    indexes
    |> Array.mapi (fun i idx ->
        let line = if idx < lines.Length then lines.[idx] else ""
        let suffix = if line.EndsWith("\n") || line.EndsWith("\r") then line else line + "\n"
        $"{padLeft labels.[i] width}|{suffix}")
    |> String.concat ""

type PathLocks() =
    let locks = Dictionary<string, SerialQueue>()
    member _.WithLock<'T>(path: string, timeoutMs: int, fn: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        let queue =
            match locks.TryGetValue(path) with
            | true, q -> q
            | false, _ ->
                let q = SerialQueue()
                locks.[path] <- q
                q
        let work = queue.Enqueue(fn)
        promise {
            let! result = withTimeout timeoutMs work
            match result with
            | Some v -> return v
            | None -> return! Promise.reject(exn $"Lock timeout on {path}")
        }

type EditPlusState() =
    member val Registry = TagRegistry()
    member val Locks = PathLocks()
    member this.Reset() = this.Registry.Reset()

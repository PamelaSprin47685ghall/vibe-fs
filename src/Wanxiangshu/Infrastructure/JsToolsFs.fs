namespace Wanxiangshu.Infrastructure

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain

/// JS-005/007/008/009/012: the filesystem adapter behind the js-* runtime
/// bindings — strict UTF-8 reads, bounded deterministic glob, ordered anchor
/// matching, ephemeral staging, all-or-nothing commit with rollback. Durable
/// facts never live here; EventStore owns them (JS-012).
module JsToolsFs =

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string) (options: obj) : obj = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readFileBuffer (path: string) : obj = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileSync (path: string) (data: string) : unit = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("unlinkSync", "node:fs")>]
    let private unlinkSync (path: string) : unit = jsNative

    [<Import("readdirSync", "node:fs")>]
    let private readdirSync (path: string) : string array = jsNative

    [<Import("statSync", "node:fs")>]
    let private statSync (path: string) : obj = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

    [<Import("relative", "node:path")>]
    let private pathRelative (from: string) (toPath: string) : string = jsNative

    [<Import("resolve", "node:path")>]
    let private pathResolve (path: string) : string = jsNative

    [<Import("isAbsolute", "node:path")>]
    let private pathIsAbsolute (path: string) : bool = jsNative

    [<Emit("new TextDecoder('utf-8', { fatal: true }).decode($0)")>]
    let private decodeUtf8 (buffer: obj) : string = jsNative

    [<Emit("new RegExp($0, 'g')")>]
    let private regexGlobal (pattern: string) : obj = jsNative

    [<Emit("""
        ((text, re, occurrence) => {
          let index = 0;
          let m;
          while ((m = re.exec(text)) !== null) {
            index += 1;
            if (index === occurrence) {
              const matched = m[0] === undefined ? '' : m[0];
              return { found: true, start: m.index, end: m.index + matched.length };
            }
            if (m.index === re.lastIndex) re.lastIndex += 1;
          }
          return { found: false };
        })($0, $1, $2)
    """)>]
    let private findRegexOccurrence (text: string) (re: obj) (occurrence: int) : obj = jsNative

    [<Emit("$0.isDirectory()")>]
    let private isDirectory (stat: obj) : bool = jsNative

    [<Emit("$0.found === true")>]
    let private matchFound (m: obj) : bool = jsNative

    [<Emit("$0.start")>]
    let private matchStart (m: obj) : int = jsNative

    [<Emit("$0.end")>]
    let private matchEnd (m: obj) : int = jsNative

    /// JS-005: strict UTF-8 read. ENOENT → FILE_NOT_FOUND; fatal decode error
    /// → INVALID_UTF8 (never silent replacement chars).
    let readUtf8 (path: string) : Result<string, JsFailure> =
        try
            let buffer = readFileBuffer path
            Ok(decodeUtf8 buffer)
        with ex ->
            let code = string (ex?code)

            if code = "ENOENT" then
                Error(JsFailure.FileNotFound path)
            else
                Error(JsFailure.FileReadFailed path)

    /// JS-005: read with invalid-UTF-8 classification; used by file() so the
    /// code is distinct from generic read failures.
    let readUtf8Classified (path: string) : Result<string, JsFailure> =
        try
            let buffer = readFileBuffer path

            try
                Ok(decodeUtf8 buffer)
            with _ ->
                Error(JsFailure.InvalidUtf8 path)
        with ex ->
            let code = string (ex?code)

            if code = "ENOENT" then
                Error(JsFailure.FileNotFound path)
            else
                Error(JsFailure.FileReadFailed path)

    /// JS-007: glob pattern → RegExp. Supports `**` (any depth), `*` (within a
    /// segment), `?` (one char). Invalid patterns fail closed.
    let private patternToRegex (pattern: string) : Result<string, JsFailure> =
        let rec convert (chars: char list) (acc: string) : Result<string, JsFailure> =
            match chars with
            | [] -> Ok acc
            | '*' :: '*' :: rest -> convert rest (acc + ".*")
            | '*' :: rest -> convert rest (acc + "[^/]*")
            | '?' :: rest -> convert rest (acc + "[^/]")
            | c :: rest -> convert rest (acc + System.Text.RegularExpressions.Regex.Escape(string c))

        if System.String.IsNullOrEmpty pattern then
            Error(JsFailure.AnchorInvalidPattern)
        else
            convert (List.ofSeq pattern) "^" |> Result.map (fun body -> body + "$")

    /// JS-007: bounded deterministic enumeration under root. maxEntries caps
    /// the result; maxDepth caps recursion. Results are sorted.
    let private walk (root: string) (maxEntries: int) (maxDepth: int) : string list =
        let rec go (dir: string) (depth: int) (acc: string list) : string list =
            if depth > maxDepth || List.length acc >= maxEntries then
                acc
            else
                try
                    let entries = readdirSync dir |> Array.toList |> List.sort

                    entries
                    |> List.fold
                        (fun state entry ->
                            if List.length state >= maxEntries then
                                state
                            else
                                let full = pathJoin dir entry

                                if isDirectory (statSync full) then
                                    go full (depth + 1) state
                                else
                                    state @ [ full ])
                        acc
                with _ ->
                    acc

        go root 0 []

    /// JS-007: glob — pattern is relative to root; results are relative paths,
    /// sorted, bounded by maxEntries matches. The enumeration walk uses a
    /// wider bound (defense against explosion) so non-matching files cannot
    /// starve the match quota; the result is truncated to maxEntries.
    let glob (root: string) (pattern: string) (maxEntries: int) (maxDepth: int) : Result<string list, JsFailure> =
        match patternToRegex pattern with
        | Error failure -> Error failure
        | Ok regex ->
            let re = regexGlobal regex
            let enumerateBound = max (maxEntries * 4 + 8) 64
            let files = walk root enumerateBound maxDepth

            files
            |> List.choose (fun full ->
                let rel = pathRelative root full

                let matched =
                    try
                        let probe = regexGlobal regex
                        let m = findRegexOccurrence rel probe 1
                        matchFound m
                    with _ ->
                        false

                if matched then Some rel else None)
            |> List.truncate maxEntries
            |> Ok

    /// JS-006: ordered string/RegExp anchor matching. `occurrence` is 1-based.
    /// Returns (start, end); zero-width matches yield start = end.
    let findAnchor (text: string) (spec: AnchorSpec) (occurrence: int) : Result<int * int, JsFailure> =
        /// Index of the nth occurrence of an exact needle, scanning forward
        /// from fromIndex (ordinal). -1 when absent.
        let rec nthIndex (needle: string) (fromIndex: int) (n: int) : int =
            if n <= 0 then
                -1
            else
                let idx = text.IndexOf(needle, fromIndex, System.StringComparison.Ordinal)

                if idx < 0 then -1
                elif n = 1 then idx
                else nthIndex needle (idx + needle.Length) (n - 1)

        if occurrence < 1 then
            Error JsFailure.AnchorInvalidPattern
        else
            match spec with
            | AnchorSpec.Exact needle ->
                if System.String.IsNullOrEmpty needle then
                    Error JsFailure.AnchorEmptyContent
                else
                    let start = nthIndex needle 0 occurrence

                    if start >= 0 then
                        Ok(start, start + needle.Length)
                    else
                        Error(JsFailure.AnchorNotFound occurrence)
            | AnchorSpec.Regex pattern ->
                if System.String.IsNullOrEmpty pattern then
                    Error JsFailure.AnchorEmptyContent
                else
                    try
                        let re = regexGlobal pattern
                        let m = findRegexOccurrence text re occurrence

                        if matchFound m then
                            Ok(matchStart m, matchEnd m)
                        else
                            Error(JsFailure.AnchorNotFound occurrence)
                    with _ ->
                        Error JsFailure.AnchorInvalidPattern

    /// JS-006: uniqueness refusal — when no occurrence is declared, the anchor
    /// must match exactly once.
    let requireUnique (text: string) (spec: AnchorSpec) : Result<int * int, JsFailure> =
        match findAnchor text spec 1 with
        | Error failure -> Error failure
        | Ok first ->
            match findAnchor text spec 2 with
            | Ok _ -> Error JsFailure.AnchorNotUnique
            | Error _ -> Ok first

    /// Resolve a tool path under root: relative paths join root; absolute
    /// paths resolve as-is (the bindings enforce the inside-root boundary).
    let resolveToolPath (root: string) (path: string) : string =
        if pathIsAbsolute path then
            pathResolve path
        else
            pathResolve (pathJoin root path)

    /// Existence probe used by preflight (JS-013).
    let existsPath (path: string) : bool =
        try
            existsSync path
        with _ ->
            false

    /// JS-013: apply a commit plan under root — two phases. Phase 1 reads every
    /// original snapshot; any read failure aborts BEFORE any write (a target
    /// that cannot be snapshotted cannot be rolled back). Phase 2 writes all
    /// files; a write failure rolls back every already-written path
    /// (rewrites restored, creates removed) — all-or-nothing.
    let commitPlan (root: string) (plan: (string * string) list) : Result<unit, JsFailure> =
        let resolvePath (path: string) =
            if pathIsAbsolute path then
                pathResolve path
            else
                pathResolve (pathJoin root path)

        // Phase 1 — snapshot every target before touching anything.
        let snapshots =
            plan
            |> List.map (fun (path, _) ->
                let full = resolvePath path

                let existed =
                    try
                        existsSync full
                    with _ ->
                        false

                if existed then
                    try
                        Ok(path, Some(decodeUtf8 (readFileBuffer full)))
                    with _ ->
                        Error(JsFailure.FileReadFailed path)
                else
                    Ok(path, None))

        match
            List.tryPick
                (function
                | Error failure -> Some failure
                | Ok _ -> None)
                snapshots
        with
        | Some failure -> Error failure
        | None ->
            let snapshotList =
                snapshots
                |> List.choose (function
                    | Ok(path, original) -> Some(path, original)
                    | Error _ -> None)

            // Phase 2 — write all; roll back on the first failure.
            let rec apply (remaining: (string * string) list) (doneList: (string * string option) list) =
                match remaining with
                | [] -> Ok()
                | (path, newText) :: rest ->
                    let full = resolvePath path

                    try
                        writeFileSync full newText
                        apply rest ((path, snd (List.find (fun (p, _) -> p = path) snapshotList)) :: doneList)
                    with _ ->
                        // roll back everything applied so far
                        for (appliedPath, appliedOriginal) in doneList do
                            try
                                let appliedFull = resolvePath appliedPath

                                match appliedOriginal with
                                | Some text -> writeFileSync appliedFull text
                                | None ->
                                    if existsSync appliedFull then
                                        unlinkSync appliedFull
                            with _ ->
                                ()

                        Error JsFailure.TransactionCommitFailed

            apply plan []

    /// JS-015: rollback — restore originals / remove creates, reversed order.
    let rollbackPlan (root: string) (plan: (string * string option) list) : unit =
        for (path, original) in plan do
            try
                let full =
                    if pathIsAbsolute path then
                        pathResolve path
                    else
                        pathResolve (pathJoin root path)

                match original with
                | Some text -> writeFileSync full text
                | None ->
                    if existsSync full then
                        unlinkSync full
            with _ ->
                ()

    /// JS-015: undo one mutation only when the disk still holds the text we
    /// wrote (expectedCurrent). If the file was changed by someone else, or we
    /// never wrote it, nothing is touched — recovery never clobbers external
    /// edits.
    let undoIfMatches (root: string) (path: string) (expectedCurrent: string) (restoreTo: string option) : unit =
        let full = resolveToolPath root path

        match readUtf8Classified full with
        | Ok current when current = expectedCurrent ->
            try
                match restoreTo with
                | Some text -> writeFileSync full text
                | None ->
                    if existsSync full then
                        unlinkSync full
            with _ ->
                ()
        | _ -> ()

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

    [<Import("lstatSync", "node:fs")>]
    let private lstatSync (path: string) : obj = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

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

    [<Emit("""
        ((text, re) => {
          const hits = [];
          let m;
          while ((m = re.exec(text)) !== null) {
            hits.push({ index: m.index, text: m[0] === undefined ? '' : m[0] });
            if (m.index === re.lastIndex) re.lastIndex += 1;
          }
          return hits;
        })($0, $1)
    """)>]
    let private allRegexHits (text: string) (re: obj) : obj array = jsNative

    [<Emit("$0.isDirectory()")>]
    let private isDirectory (stat: obj) : bool = jsNative

    [<Emit("$0.isSymbolicLink()")>]
    let private isSymbolicLink (stat: obj) : bool = jsNative

    [<Emit("$0.isFile()")>]
    let private isFile (stat: obj) : bool = jsNative

    [<Emit("new RegExp($0)")>]
    let private regexExact (source: string) : obj = jsNative

    [<Emit("$0.test($1)")>]
    let private regexTest (re: obj) (text: string) : bool = jsNative

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

    type JsGlobListing = { Paths: string list; Truncated: bool }

    type JsGrepHit =
        { Path: string
          Line: int
          Column: int
          Text: string }

    type JsGrepListing =
        { Matches: JsGrepHit list
          Truncated: bool }

    type private IgnoreRule =
        { Base: string
          Regex: obj
          Negated: bool
          DirectoryOnly: bool }

    let private splitTopCommas (inner: string) : string list =
        let rec go (chars: char list) (depth: int) (buf: string) (acc: string list) =
            match chars with
            | [] -> List.rev (buf :: acc)
            | '{' :: rest -> go rest (depth + 1) (buf + "{") acc
            | '}' :: rest -> go rest (max 0 (depth - 1)) (buf + "}") acc
            | ',' :: rest when depth = 0 -> go rest 0 "" (buf :: acc)
            | c :: rest -> go rest depth (buf + string c) acc

        go (List.ofSeq inner) 0 "" []

    let private findBraceGroup (s: string) : (string * string list * string) option =
        let rec scan (i: int) (depth: int) (start: int) =
            if i >= s.Length then
                None
            else
                match s.[i] with
                | '{' when depth = 0 -> scan (i + 1) 1 i
                | '{' -> scan (i + 1) (depth + 1) start
                | '}' when depth = 1 && start >= 0 ->
                    let inner = s.Substring(start + 1, i - start - 1)
                    Some(s.Substring(0, start), splitTopCommas inner, s.Substring(i + 1))
                | '}' when depth > 1 -> scan (i + 1) (depth - 1) start
                | _ -> scan (i + 1) depth start

        scan 0 0 -1

    let rec private expandBraces (pattern: string) : string list =
        match findBraceGroup pattern with
        | None -> [ pattern ]
        | Some(before, alts, after) -> alts |> List.collect (fun alt -> expandBraces (before + alt + after))

    let private wildmatchRegex (pattern: string) : Result<obj, JsFailure> =
        let rec convert (chars: char list) (acc: string) : Result<string, JsFailure> =
            match chars with
            | [] -> Ok acc
            | '*' :: '*' :: '/' :: rest -> convert rest (acc + "(?:.*/)?")
            | '*' :: '*' :: rest -> convert rest (acc + ".*")
            | '*' :: rest -> convert rest (acc + "[^/]*")
            | '?' :: rest -> convert rest (acc + "[^/]")
            | '[' :: rest ->
                let rec takeClass (xs: char list) (buf: string) (count: int) : Result<string, JsFailure> =
                    match xs with
                    | [] -> Error JsFailure.AnchorInvalidPattern
                    | ']' :: more when count > 0 -> convert more (acc + "[" + buf + "]")
                    | '!' :: more when count = 0 -> takeClass more "^" 1
                    | c :: more ->
                        let piece = if c = '\\' then "\\\\" else string c

                        takeClass more (buf + piece) (count + 1)

                takeClass rest "" 0
            | c :: rest -> convert rest (acc + System.Text.RegularExpressions.Regex.Escape(string c))

        if System.String.IsNullOrEmpty pattern then
            Error JsFailure.AnchorInvalidPattern
        else
            convert (List.ofSeq pattern) "^"
            |> Result.map (fun body -> regexExact (body + "$"))

    let private compilePathPattern (pattern: string) : Result<obj, JsFailure> =
        let leading = pattern.StartsWith("/")
        let rest = if leading then pattern.Substring(1) else pattern

        if System.String.IsNullOrEmpty rest then
            Error JsFailure.AnchorInvalidPattern
        else
            let body =
                if leading then rest
                elif rest.Contains("/") then rest
                else "**/" + rest

            wildmatchRegex body

    let private compileUserPatterns (pattern: string) : Result<obj list, JsFailure> =
        if System.String.IsNullOrEmpty pattern then
            Error JsFailure.AnchorInvalidPattern
        else
            let rec go remaining acc =
                match remaining with
                | [] -> Ok(List.rev acc)
                | piece :: rest ->
                    match compilePathPattern piece with
                    | Error failure -> Error failure
                    | Ok re -> go rest (re :: acc)

            go (expandBraces pattern) []

    let private parseIgnoreLine (baseRel: string) (raw: string) : IgnoreRule option =
        let line =
            if raw.EndsWith("\r") then
                raw.Substring(0, raw.Length - 1)
            else
                raw

        let trimmed = line.Trim()

        if trimmed = "" || trimmed.StartsWith("#") then
            None
        else
            let negated = trimmed.StartsWith("!")
            let body = if negated then trimmed.Substring(1) else trimmed

            if body = "" then
                None
            else
                let directoryOnly = body.EndsWith("/")

                let spec =
                    if directoryOnly then
                        body.Substring(0, body.Length - 1)
                    else
                        body

                match compilePathPattern spec with
                | Error _ -> None
                | Ok re ->
                    Some
                        { Base = baseRel
                          Regex = re
                          Negated = negated
                          DirectoryOnly = directoryOnly }

    let private loadIgnoreFile (filePath: string) (baseRel: string) : IgnoreRule list =
        match readUtf8 filePath with
        | Error _ -> []
        | Ok text -> text.Split([| '\n' |]) |> Array.toList |> List.choose (parseIgnoreLine baseRel)

    let private isIgnored (rules: IgnoreRule list) (rel: string) (isDir: bool) : bool =
        let rec go remaining ignored =
            match remaining with
            | [] -> ignored
            | rule :: rest ->
                if rule.DirectoryOnly && not isDir then
                    go rest ignored
                else
                    let local =
                        if rule.Base = "" then
                            Some rel
                        elif rel = rule.Base then
                            Some ""
                        elif rel.StartsWith(rule.Base + "/") then
                            Some(rel.Substring(rule.Base.Length + 1))
                        else
                            None

                    match local with
                    | None
                    | Some "" -> go rest ignored
                    | Some path when regexTest rule.Regex path -> go rest (not rule.Negated)
                    | Some _ -> go rest ignored

        go rules false

    let private collectVisibleFiles (root: string) (collectCap: int) (visitCap: int) : string list * bool =
        let files = ResizeArray<string>()
        let mutable truncated = false
        let mutable visits = 0

        let rec walk (dir: string) (rel: string) (rules: IgnoreRule list) =
            if truncated then
                ()
            elif visits >= visitCap then
                truncated <- true
            else
                visits <- visits + 1

                let nested = loadIgnoreFile (pathJoin dir ".gitignore") rel

                let extra =
                    if rel = "" then
                        loadIgnoreFile (pathJoin root ".git/info/exclude") "" @ nested
                    else
                        nested

                let rules' = rules @ extra

                try
                    let entries = readdirSync dir |> Array.toList |> List.sort

                    for entry in entries do
                        if not truncated && entry <> ".git" then
                            let full = pathJoin dir entry
                            let childRel = if rel = "" then entry else rel + "/" + entry

                            try
                                let st = lstatSync full

                                if isSymbolicLink st then
                                    ()
                                elif isDirectory st then
                                    if not (isIgnored rules' childRel true) then
                                        walk full childRel rules'
                                elif isFile st then
                                    if not (isIgnored rules' childRel false) then
                                        if files.Count >= collectCap then
                                            truncated <- true
                                        else
                                            files.Add(childRel.Replace('\\', '/'))
                            with _ ->
                                ()
                with _ ->
                    ()

        walk root "" []
        List.ofSeq files, truncated

    /// JS-007: gitignore-style glob. maxEntries bounds the returned match
    /// count; truncated is true when matches or traversal hit a cap.
    let glob (root: string) (pattern: string) (maxEntries: int) : Result<JsGlobListing, JsFailure> =
        match compileUserPatterns pattern with
        | Error failure -> Error failure
        | Ok matchers ->
            let cap = max maxEntries 0
            let collectCap = min 8192 (max cap (cap * 8 + 8))
            let files, walkTruncated = collectVisibleFiles root collectCap 100000

            let matched =
                files
                |> List.filter (fun rel -> List.exists (fun re -> regexTest re rel) matchers)
                |> List.sort

            let truncated = walkTruncated || matched.Length > cap

            Ok
                { Paths = List.truncate cap matched
                  Truncated = truncated }

    let private lineColumn (text: string) (index: int) : int * int =
        let rec loop i line col =
            if i >= index || i >= text.Length then line, col
            elif text.[i] = '\n' then loop (i + 1) (line + 1) 1
            else loop (i + 1) line (col + 1)

        loop 0 1 1

    let private exactHits (text: string) (needle: string) : (int * string) list =
        let rec go fromIndex =
            let idx = text.IndexOf(needle, fromIndex, System.StringComparison.Ordinal)

            if idx < 0 then
                []
            else
                (idx, needle) :: go (idx + max needle.Length 1)

        if needle = "" then [] else go 0

    let private regexHits (text: string) (source: string) : (int * string) list =
        try
            allRegexHits text (regexGlobal source)
            |> Array.toList
            |> List.map (fun hit -> (int (hit?index), string (hit?text)))
        with _ ->
            []

    /// JS-020: Host grep over gitignore-selected UTF-8 files.
    let grep (root: string) (spec: AnchorSpec) (pattern: string) (maxMatches: int) : Result<JsGrepListing, JsFailure> =
        let globPattern =
            if System.String.IsNullOrEmpty pattern then
                "**/*"
            else
                pattern

        let cap = max maxMatches 0

        match glob root globPattern 4096 with
        | Error failure -> Error failure
        | Ok listing ->
            let acc = ResizeArray<JsGrepHit>()
            let mutable truncated = listing.Truncated

            for rel in listing.Paths do
                if acc.Count >= cap then
                    truncated <- true
                else
                    match readUtf8Classified (pathJoin root rel) with
                    | Error _ -> ()
                    | Ok text ->
                        let hits =
                            match spec with
                            | AnchorSpec.Exact needle -> exactHits text needle
                            | AnchorSpec.Regex source -> regexHits text source

                        for (index, matched) in hits do
                            if acc.Count >= cap then
                                truncated <- true
                            else
                                let line, column = lineColumn text index

                                acc.Add
                                    { Path = rel
                                      Line = line
                                      Column = column
                                      Text = matched }

            Ok
                { Matches = List.ofSeq acc
                  Truncated = truncated }

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

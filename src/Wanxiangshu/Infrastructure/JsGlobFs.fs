namespace Wanxiangshu.Infrastructure

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain

/// JS-007: the bounded, deterministic gitignore-aware glob adapter behind the
/// js-* runtime bindings. Durable facts never live here; EventStore owns them
/// (JS-012).
module JsGlobFs =

    [<Import("readdirSync", "node:fs")>]
    let private readdirSync (path: string) : string array = jsNative

    [<Import("lstatSync", "node:fs")>]
    let private lstatSync (path: string) : obj = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

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

    type JsGlobListing = { Paths: string list; Truncated: bool }

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
        match JsUtf8Fs.readUtf8 filePath with
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
        // DSL-MUTABLE: resource — file collector truncation and visit counters
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

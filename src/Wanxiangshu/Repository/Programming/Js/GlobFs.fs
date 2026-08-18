namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// JS-007: the deterministic gitignore-aware glob adapter behind the js-*
/// runtime bindings. Durable facts never live here; EventStore owns them
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

    type JsGlobListing = { Paths: string list }

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

    type private BraceScan =
        | BraceDone of (string * string list * string) option
        | BraceContinue of i: int * depth: int * start: int

    let private braceCharStep (s: string) (i: int) (depth: int) (start: int) : BraceScan =
        match s.[i] with
        | '{' when depth = 0 -> BraceContinue(i + 1, 1, i)
        | '{' -> BraceContinue(i + 1, depth + 1, start)
        | '}' when depth = 1 && start >= 0 ->
            let inner = s.Substring(start + 1, i - start - 1)
            BraceDone(Some(s.Substring(0, start), splitTopCommas inner, s.Substring(i + 1)))
        | '}' when depth > 1 -> BraceContinue(i + 1, depth - 1, start)
        | _ -> BraceContinue(i + 1, depth, start)

    let private findBraceGroup (s: string) : (string * string list * string) option =
        let rec scan (i: int) (depth: int) (start: int) =
            if i >= s.Length then
                None
            else
                continueBraceScan i depth start

        and continueBraceScan (i: int) (depth: int) (start: int) =
            match braceCharStep s i depth start with
            | BraceDone result -> result
            | BraceContinue(i', depth', start') -> scan i' depth' start'

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
            | '[' :: rest -> takeClass rest "" 0 acc
            | c :: rest -> convert rest (acc + System.Text.RegularExpressions.Regex.Escape(string c))

        and takeClass (xs: char list) (buf: string) (count: int) (acc: string) : Result<string, JsFailure> =
            match xs with
            | [] -> Error JsFailure.AnchorInvalidPattern
            | ']' :: more when count > 0 -> convert more (acc + "[" + buf + "]")
            | '!' :: more when count = 0 -> takeClass more "^" 1 acc
            | c :: more ->
                let piece = if c = '\\' then "\\\\" else string c
                takeClass more (buf + piece) (count + 1) acc

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

    let matchesPathPattern (pattern: string) (path: string) : Result<bool, JsFailure> =
        compilePathPattern pattern |> Result.map (fun regex -> regexTest regex path)

    let private compileUserPatterns (pattern: string) : Result<obj array, JsFailure> =
        if System.String.IsNullOrEmpty pattern then
            Error JsFailure.AnchorInvalidPattern
        else
            expandBraces pattern
            |> List.traverseResultM compilePathPattern
            |> Result.map Array.ofList

    let private parseIgnoreBody (baseRel: string) (negated: bool) (body: string) : IgnoreRule option =
        let directoryOnly = body.EndsWith("/")

        let spec =
            if directoryOnly then
                body.Substring(0, body.Length - 1)
            else
                body

        compilePathPattern spec
        |> Result.toOption
        |> Option.map (fun re ->
            { Base = baseRel
              Regex = re
              Negated = negated
              DirectoryOnly = directoryOnly })

    let private parseIgnoreLine (baseRel: string) (raw: string) : IgnoreRule option =
        let line =
            if raw.EndsWith("\r") then
                raw.Substring(0, raw.Length - 1)
            else
                raw

        let trimmed = line.Trim()
        let negated = trimmed.StartsWith("!")
        let body = if negated then trimmed.Substring(1) else trimmed

        if trimmed = "" || trimmed.StartsWith("#") || body = "" then
            None
        else
            parseIgnoreBody baseRel negated body

    let private loadIgnoreFile (filePath: string) (baseRel: string) : IgnoreRule list =
        match JsUtf8Fs.readUtf8 filePath with
        | Error _ -> []
        | Ok text -> text.Split([| '\n' |]) |> Array.toList |> List.choose (parseIgnoreLine baseRel)

    let private localIgnorePath (ruleBase: string) (rel: string) : string option =
        if ruleBase = "" then
            Some rel
        elif rel = ruleBase then
            Some ""
        elif rel.StartsWith(ruleBase + "/") then
            Some(rel.Substring(ruleBase.Length + 1))
        else
            None

    let private isIgnored (rules: ResizeArray<IgnoreRule>) (rel: string) (isDir: bool) : bool =
        let rec go i ignored =
            if i >= rules.Count then ignored else applyRule i ignored

        and applyRule i ignored =
            let rule = rules.[i]

            match rule.DirectoryOnly && not isDir, localIgnorePath rule.Base rel with
            | true, _ -> go (i + 1) ignored
            | _, None
            | _, Some "" -> go (i + 1) ignored
            | _, Some path when regexTest rule.Regex path -> go (i + 1) (not rule.Negated)
            | _, Some _ -> go (i + 1) ignored

        go 0 false

    type private VisibleEntry =
        | SkipEntry
        | RecurseDirectory of full: string * childRel: string
        | EmitFile of childRel: string

    let private tryListDirectory (dir: string) : string list =
        try
            readdirSync dir |> Array.toList |> List.sort
        with _ ->
            []

    let private tryLstat (full: string) : obj option =
        try
            Some(lstatSync full)
        with _ ->
            None

    let private classifyStatKind
        (rules: ResizeArray<IgnoreRule>)
        (childRel: string)
        (full: string)
        (st: obj)
        : VisibleEntry =
        if isSymbolicLink st then
            SkipEntry
        elif isDirectory st && isIgnored rules childRel true then
            SkipEntry
        elif isDirectory st then
            RecurseDirectory(full, childRel)
        elif isFile st && isIgnored rules childRel false then
            SkipEntry
        elif isFile st then
            EmitFile childRel
        else
            SkipEntry

    let private classifyNonGitEntry
        (rules: ResizeArray<IgnoreRule>)
        (rel: string)
        (dir: string)
        (entry: string)
        : VisibleEntry =
        let full = pathJoin dir entry
        let childRel = if rel = "" then entry else rel + "/" + entry

        match tryLstat full with
        | None -> SkipEntry
        | Some st -> classifyStatKind rules childRel full st

    let private classifyVisibleEntry
        (rules: ResizeArray<IgnoreRule>)
        (rel: string)
        (dir: string)
        (entry: string)
        : VisibleEntry =
        if entry = ".git" then
            SkipEntry
        else
            classifyNonGitEntry rules rel dir entry

    let private applyVisibleEntry (files: ResizeArray<string>) (walk: string -> string -> unit) (action: VisibleEntry) =
        match action with
        | SkipEntry -> ()
        | RecurseDirectory(full, childRel) -> walk full childRel
        | EmitFile childRel -> files.Add(childRel.Replace('\\', '/'))

    let private processDirectoryEntries
        (rules: ResizeArray<IgnoreRule>)
        (files: ResizeArray<string>)
        (walk: string -> string -> unit)
        (dir: string)
        (rel: string)
        (entries: string list)
        =
        for entry in entries do
            classifyVisibleEntry rules rel dir entry |> applyVisibleEntry files walk

    let private collectVisibleFiles (root: string) : string list =
        // DSL-MUTABLE: algorithm-scratch — visible file accumulator
        let files = ResizeArray<string>()
        // DSL-MUTABLE: algorithm-scratch — ignore rule accumulator
        let rules = ResizeArray<IgnoreRule>()

        let rec walk (dir: string) (rel: string) =
            let nested = loadIgnoreFile (pathJoin dir ".gitignore") rel
            let mark = rules.Count

            let rootExcludes =
                if rel = "" then
                    loadIgnoreFile (pathJoin root ".git/info/exclude") ""
                else
                    []

            for rule in rootExcludes do
                rules.Add(rule)

            for rule in nested do
                rules.Add(rule)

            try
                processDirectoryEntries rules files walk dir rel (tryListDirectory dir)
            finally
                rules.RemoveRange(mark, rules.Count - mark)

        walk root ""
        List.ofSeq files

    /// JS-007: gitignore-style glob. Full deterministic enumeration — no
    /// internal bound. An oversized result is tail-kept once, by the Host
    /// tool-result bound at the final boundary.
    let glob (root: string) (pattern: string) : Result<JsGlobListing, JsFailure> =
        result {
            let! matchers = compileUserPatterns pattern

            let paths =
                collectVisibleFiles root
                |> List.filter (fun rel -> Array.exists (fun re -> regexTest re rel) matchers)
                |> List.sort

            return { Paths = paths }
        }

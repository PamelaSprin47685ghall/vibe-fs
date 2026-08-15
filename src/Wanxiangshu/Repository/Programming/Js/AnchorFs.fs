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

/// JS-006/020: the ordered anchor matching and host grep adapter behind the
/// js-* runtime bindings. Durable facts never live here; EventStore owns them
/// (JS-012).
module JsAnchorFs =

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

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

    [<Emit("$0.found === true")>]
    let private matchFound (m: obj) : bool = jsNative

    [<Emit("$0.start")>]
    let private matchStart (m: obj) : int = jsNative

    [<Emit("$0.end")>]
    let private matchEnd (m: obj) : int = jsNative

    type JsGrepHit =
        { Path: string
          Line: int
          Column: int
          Text: string }

    type JsGrepListing = { Matches: JsGrepHit list }

    let private appendLineStartIfNewline (offsets: ResizeArray<int>) (text: string) (i: int) =
        if text.[i] = '\n' then
            offsets.Add(i + 1)

    /// 每行起始偏移索引。lineOffsets[0]=0；每个 '\n' 之后紧跟下一行起始。
    /// 只认 '\n'（不做 CRLF 修正），与历史 lineColumn 计法一致。
    let private lineOffsets (text: string) : int array =
        let offsets = ResizeArray<int>()
        offsets.Add 0

        for i in 0 .. text.Length - 1 do
            appendLineStartIfNewline offsets text i

        offsets.ToArray()

    /// (line, column) 均 1-based，从 lineOffsets 二分定位 index 所在行。
    /// index 落在 '\n' 上时，该换行属于当前行（line 不递增），与旧逐字扫描等价。
    let private lineColumn (offsets: int array) (index: int) : int * int =
        // 二分找最后一个 lineStart <= index
        // DSL-MUTABLE: algorithm-scratch — binary-search lower bound
        let mutable lo = 0
        // DSL-MUTABLE: algorithm-scratch — binary-search upper bound
        let mutable hi = offsets.Length - 1

        while lo < hi do
            let mid = (lo + hi + 1) / 2

            if offsets.[mid] <= index then lo <- mid else hi <- mid - 1

        lo + 1, index - offsets.[lo] + 1

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

    let private locateHits (text: string) (spec: AnchorSpec) : (int * string) list =
        match spec with
        | AnchorSpec.Exact needle -> exactHits text needle
        | AnchorSpec.Regex source -> regexHits text source

    let private grepHitAt (rel: string) (offsets: int array) (index: int) (matched: string) : JsGrepHit =
        let line, column = lineColumn offsets index

        { Path = rel
          Line = line
          Column = column
          Text = matched }

    let private grepHitsOnText (rel: string) (text: string) (spec: AnchorSpec) : JsGrepHit list =
        let offsets = lineOffsets text
        locateHits text spec
        |> List.map (fun (index, matched) -> grepHitAt rel offsets index matched)

    let private grepHitsOnPath (root: string) (spec: AnchorSpec) (rel: string) : JsGrepHit list =
        match JsUtf8Fs.readUtf8Classified (pathJoin root rel) with
        | Error _ -> []
        | Ok text -> grepHitsOnText rel text spec

    /// JS-020: Host grep over gitignore-selected UTF-8 files. Full result —
    /// no internal bound; the Host tool-result bound tail-keeps the tail.
    let grep (root: string) (spec: AnchorSpec) (pattern: string) : Result<JsGrepListing, JsFailure> =
        let globPattern =
            if System.String.IsNullOrEmpty pattern then
                "**/*"
            else
                pattern

        result {
            let! listing = JsGlobFs.glob root globPattern
            let matches = listing.Paths |> List.collect (grepHitsOnPath root spec)
            return { Matches = matches }
        }

    /// Index of the nth occurrence of an exact needle, scanning forward
    /// from fromIndex (ordinal). -1 when absent.
    let rec private nthIndex (text: string) (needle: string) (fromIndex: int) (n: int) : int =
        if n <= 0 then
            -1
        else
            let idx = text.IndexOf(needle, fromIndex, System.StringComparison.Ordinal)

            if idx < 0 then -1
            elif n = 1 then idx
            else nthIndex text needle (idx + needle.Length) (n - 1)

    let private findExactAnchor (text: string) (needle: string) (occurrence: int) : Result<int * int, JsFailure> =
        result {
            do!
                if System.String.IsNullOrEmpty needle then
                    Error JsFailure.AnchorEmptyContent
                else
                    Ok()

            let start = nthIndex text needle 0 occurrence

            if start < 0 then
                return!
                    Error(
                        JsFailure.AnchorNotFound(
                            "anchor did not match at occurrence " + string occurrence
                        )
                    )
            else
                return start, start + needle.Length
        }

    let private regexOccurrenceOrMissing (text: string) (pattern: string) (occurrence: int) : Result<int * int, JsFailure> =
        let re = regexGlobal pattern
        let m = findRegexOccurrence text re occurrence

        if matchFound m then
            Ok(matchStart m, matchEnd m)
        else
            Error(JsFailure.AnchorNotFound("anchor did not match at occurrence " + string occurrence))

    let private findRegexAnchor (text: string) (pattern: string) (occurrence: int) : Result<int * int, JsFailure> =
        result {
            do!
                if System.String.IsNullOrEmpty pattern then
                    Error JsFailure.AnchorEmptyContent
                else
                    Ok()

            try
                return! regexOccurrenceOrMissing text pattern occurrence
            with _ ->
                return! Error JsFailure.AnchorInvalidPattern
        }

    let private findAnchorBySpec (text: string) (spec: AnchorSpec) (occurrence: int) : Result<int * int, JsFailure> =
        match spec with
        | AnchorSpec.Exact needle -> findExactAnchor text needle occurrence
        | AnchorSpec.Regex pattern -> findRegexAnchor text pattern occurrence

    /// JS-006: ordered string/RegExp anchor matching. `occurrence` is 1-based.
    /// Returns (start, end); zero-width matches yield start = end.
    let findAnchor (text: string) (spec: AnchorSpec) (occurrence: int) : Result<int * int, JsFailure> =
        if occurrence < 1 then
            Error JsFailure.AnchorInvalidPattern
        else
            findAnchorBySpec text spec occurrence

    let private uniqueAfterFirst (text: string) (spec: AnchorSpec) (first: int * int) : Result<int * int, JsFailure> =
        match findAnchor text spec 2 with
        | Ok _ -> Error JsFailure.AnchorNotUnique
        | Error _ -> Ok first

    /// JS-006: uniqueness refusal — when no occurrence is declared, the anchor
    /// must match exactly once.
    let requireUnique (text: string) (spec: AnchorSpec) : Result<int * int, JsFailure> =
        result {
            let! first = findAnchor text spec 1
            return! uniqueAfterFirst text spec first
        }

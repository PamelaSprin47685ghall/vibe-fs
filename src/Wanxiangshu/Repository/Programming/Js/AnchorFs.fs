namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// Ordered anchor matching and host grep, bridged to `wanxiangli/fs`
/// (CUT-24). The matching algorithm lives in the library; this module keeps
/// the main-repo signature (AnchorSpec DU, JsGrepHit/JsGrepListing records)
/// stable for existing consumers.
module JsAnchorFs =

    [<Import("Filesystem", "wanxiangli/fs")>]
    let private Filesystem: obj = jsNative

    [<Emit("$0({ root: $1 })")>]
    let private createFilesystem (factory: obj) (root: string) : obj = jsNative

    let private api = createFilesystem Filesystem ""

    let private specToJs (spec: AnchorSpec) : obj =
        match spec with
        | AnchorSpec.Exact text -> createObj [ "kind" ==> "exact"; "text" ==> text ]
        | AnchorSpec.Regex text -> createObj [ "kind" ==> "regex"; "text" ==> text ]

    let private failureOf (result: obj) : JsFailure =
        JsFailure.ofWire (string (result?code)) (string (result?reason))

    let private spanOf (result: obj) : Result<int * int, JsFailure> =
        if unbox<bool> (result?ok) then
            let value = unbox<obj array> (result?value)
            Ok(unbox<int> (value.[0]), unbox<int> (value.[1]))
        else
            Error(failureOf result)

    type JsGrepHit =
        { Path: string
          Line: int
          Column: int
          Text: string }

    type JsGrepListing =
        { Matches: JsGrepHit list
          ReadSnapshots: JsReadSnapshot list }

    /// JS-020: Host grep over gitignore-selected UTF-8 files. Full result —
    /// no internal bound; the Host tool-result bound tail-keeps the tail.
    let grep (root: string) (spec: AnchorSpec) (pattern: string) : Task<Result<JsGrepListing, JsFailure>> =
        let rootApi = createFilesystem Filesystem root

        task {
            let! (result: obj) = unbox<Task<obj>> (rootApi?grep (specToJs spec, pattern))

            if not (unbox<bool> (result?ok)) then
                return Error(failureOf result)
            else
                let value = result?value
                let matches = unbox<obj array> (value?matches)

                let hits =
                    matches
                    |> Array.map (fun hit ->
                        { Path = string (hit?path)
                          Line = int (hit?line)
                          Column = int (hit?column)
                          Text = string (hit?text) })
                    |> Array.toList

                let snapshots = unbox<obj array> (value?readSnapshots)

                let readSnapshots =
                    snapshots
                    |> Array.map (fun snapshot ->
                        { Path = string (snapshot?path)
                          Text = string (snapshot?text) })
                    |> Array.toList

                return
                    Ok
                        { Matches = hits
                          ReadSnapshots = readSnapshots }
        }

    /// JS-006: ordered string/RegExp anchor matching. `occurrence` is 1-based.
    /// Returns (start, end); zero-width matches yield start = end.
    let findAnchor (text: string) (spec: AnchorSpec) (occurrence: int) : Result<int * int, JsFailure> =
        spanOf (api?findAnchor (text, specToJs spec, float occurrence))

    /// JS-006: uniqueness refusal — when no occurrence is declared, the anchor
    /// must match exactly once.
    let requireUnique (text: string) (spec: AnchorSpec) : Result<int * int, JsFailure> =
        spanOf (api?requireUnique (text, specToJs spec))

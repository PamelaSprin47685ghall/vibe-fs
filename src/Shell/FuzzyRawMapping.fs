module VibeFs.Shell.FuzzyRawMapping

open VibeFs.Kernel
open VibeFs.Kernel.FuzzyFormat

/// The single source of truth for reading the fff-node raw item shape into
/// typed F# values.  Both find and grep commands share these so the host→typed
/// mapping exists exactly once (DRY) and can never drift between them.

let optStr (o: obj) (key: string) : string option =
    let v = Dyn.get o key
    if Dyn.isNullish v then None else Some(string v)

let optInt (o: obj) (key: string) : int option =
    let v = Dyn.get o key
    if Dyn.isNullish v then None else Some(unbox<int> v)

/// Read a raw `items` array, treating null/undefined/non-array as empty so a
/// malformed host response never crashes the cast.
let itemsOf (value: obj) : obj array =
    let items = Dyn.get value "items"
    if Dyn.isNullish items || not (Dyn.isArray items) then [||] else items :?> obj array

/// Read a string-list field (e.g. contextBefore), empty when absent/non-array.
let stringListOf (o: obj) (key: string) : string list =
    let v = Dyn.get o key
    if Dyn.isNullish v || not (Dyn.isArray v) then [] else (v :?> obj array) |> Array.map string |> List.ofArray

/// Build a FileAnnotation from a raw item's git/frecency fields (None when none present).
let annotationOf (item: obj) : FileAnnotation option =
    let git = optStr item "gitStatus"
    let total = optInt item "totalFrecencyScore"
    let access = optInt item "accessFrecencyScore"
    if git.IsSome || total.IsSome || access.IsSome then
        Some { gitStatus = git; totalFrecencyScore = total; accessFrecencyScore = access }
    else None

/// Map a raw find item to a typed FindMatch.
let toFindMatch (item: obj) : FindMatch =
    { relativePath = Dyn.str item "relativePath"; annotation = annotationOf item }

/// Map a raw grep match to a typed GrepMatch, preserving context lines + annotation.
let toGrepMatch (m: obj) : GrepMatch =
    { relativePath = Dyn.str m "relativePath"
      lineNumber = optInt m "lineNumber" |> Option.defaultValue 0
      lineContent = Dyn.str m "lineContent"
      contextBefore = stringListOf m "contextBefore"
      contextAfter = stringListOf m "contextAfter"
      annotation = annotationOf m }

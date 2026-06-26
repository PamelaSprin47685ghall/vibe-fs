module Wanxiangshu.Kernel.KnowledgeGraph.Id

open System.Text.RegularExpressions
open Wanxiangshu.Kernel.KnowledgeGraph.Types

let private idRe = Regex("^[0-9a-f]{4}$")

let tryParseId (s: string) : KnowledgeGraphId option =
    if idRe.IsMatch s then Some(KnowledgeGraphId s) else None

let idValue (KnowledgeGraphId s) : string = s

let allocateRandomHexId (randomInt: unit -> int) (existingIds: Set<string>) : Result<string, string> =
    let rec loop attempts =
        if attempts > 65536 then Error "knowledge graph id space exhausted"
        else
            let candidate = sprintf "%04x" (randomInt() % 65536)
            if not (Set.contains candidate existingIds) then Ok candidate
            else loop (attempts + 1)
    loop 0

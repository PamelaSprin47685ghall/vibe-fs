module Wanxiangshu.Shell.ReadDedupCore

open Wanxiangshu.Kernel.MessageDedup

type ReadHit =
    { msgIndex: int
      partIndex: int
      payload: ReadPayload }

let processDedupHits
    (seenByPath: Map<string, string list>)
    (messages: obj array)
    (getParts: obj -> obj array)
    (getHit: int -> int -> obj -> ReadHit option)
    : (ReadHit * DedupVerdict) list * Map<string, string list> =
    let hits = ResizeArray<ReadHit>()

    for i in 0 .. messages.Length - 1 do
        let parts = getParts messages.[i]

        for j in 0 .. parts.Length - 1 do
            match getHit i j parts.[j] with
            | Some hit -> hits.Add(hit)
            | None -> ()

    let nextSeen, (outputs, replaced) =
        foldDedup seenByPath (List.ofSeq hits |> List.map (fun h -> h.payload))

    let verdicts =
        replaced
        |> List.mapi (fun idx rep -> if rep then AlreadySeen else NewContent hits.[idx].payload)

    List.zip (List.ofSeq hits) verdicts, nextSeen

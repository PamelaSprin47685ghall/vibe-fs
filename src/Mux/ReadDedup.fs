module VibeFs.Mux.ReadDedup

open VibeFs.Kernel

let collectReadOutputs (messages: obj array) : string[] =
    VibeFs.Kernel.MessageDedup.collectReadOutputs messages |> Array.ofList

let private muxReadToolNames = Set [ "read"; "file_read" ]

let private wrapDedupedMuxReadOutput (originalPart: obj) (dedupedPart: obj) : obj =
    let originalOutput = Dyn.get originalPart "output"
    let dedupedOutput = Dyn.get dedupedPart "output"
    let isReadPart =
        Dyn.str dedupedPart "type" = "dynamic-tool"
        && Set.contains (Dyn.str dedupedPart "toolName") muxReadToolNames
        && Dyn.str dedupedPart "state" = "output-available"

    if isReadPart
       && not (Dyn.isNullish originalOutput)
       && not (Dyn.typeIs originalOutput "string")
       && Dyn.typeIs dedupedOutput "string"
       && string dedupedOutput = VibeFs.Kernel.Dedup.dedupMarker then
        Dyn.withKey dedupedPart "output" (box (Dyn.withKey originalOutput "content" (box VibeFs.Kernel.Dedup.dedupMarker)))
    else
        dedupedPart

let private wrapDedupedMuxReadParts (originalMessage: obj) (dedupedMessage: obj) : obj =
    let originalParts = Dyn.get originalMessage "parts"
    let dedupedParts = Dyn.get dedupedMessage "parts"

    if Dyn.isNullish originalParts
       || Dyn.isNullish dedupedParts
       || not (Dyn.isArray originalParts)
       || not (Dyn.isArray dedupedParts) then
        dedupedMessage
    else
        let originalArray = originalParts :?> obj array
        let dedupedArray = dedupedParts :?> obj array

        if originalArray.Length <> dedupedArray.Length then
            dedupedMessage
        else
            let wrappedParts = Array.map2 wrapDedupedMuxReadOutput originalArray dedupedArray
            let unchanged =
                wrappedParts.Length = dedupedArray.Length
                && Array.forall2 (fun left right -> obj.ReferenceEquals(left, right)) wrappedParts dedupedArray
            if unchanged then dedupedMessage else Dyn.withKey dedupedMessage "parts" (box wrappedParts)

let private wrapDedupedMessages (originalMessages: obj array) (dedupedMessages: obj array) : obj array =
    if originalMessages.Length <> dedupedMessages.Length then
        dedupedMessages
    else
        Array.map2 wrapDedupedMuxReadParts originalMessages dedupedMessages

let deduplicateReadOutputsWithSeenByPath
    (seenByPath: Map<string, string list>)
    (messages: obj array)
    : obj[] =
    let deduped = VibeFs.Kernel.MessageDedup.deduplicateReadOutputsWithSeenByPath seenByPath messages |> snd
    wrapDedupedMessages messages deduped

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj[] =
    let deduped = VibeFs.Kernel.MessageDedup.deduplicateReadOutputsWithSeen (List.ofArray seenOutputs) messages |> snd
    wrapDedupedMessages messages deduped

let deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj[] =
    let seen, deduped = VibeFs.Kernel.MessageDedup.deduplicateModelReadOutputsWithSeen (List.ofArray seenOutputs) messages
    Array.ofList seen, deduped

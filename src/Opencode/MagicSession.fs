module VibeFs.Opencode.MagicSession

open System.Collections.Generic
open VibeFs.Kernel.MagicTypes
open VibeFs.Kernel.MagicReplay

type MagicSession() =
    let cache = Dictionary<string, BacklogEntry list>()

    member _.GetOrRebuildBacklog(sessionID: string, messages: obj array) : BacklogEntry list =
        if messages.Length > 0 then
            let backlog = replayBacklog messages
            cache.[sessionID] <- backlog
            backlog
        else
            match cache.TryGetValue sessionID with
            | true, backlog -> backlog
            | false, _ -> []

    member _.Invalidate(sessionID: string) =
        cache.Remove(sessionID) |> ignore

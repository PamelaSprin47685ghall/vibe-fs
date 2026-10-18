namespace Wanxiangshu.OpenCode

open System.Collections.Generic

/// The provider's own billing for a session's most recent assistant turn: the
/// tokens it served from the cache prefix and the tokens it recomputed. Read
/// from the host's message events (assistant `info.tokens`), never derived from
/// our own accounting, and disclosed to the model as one datum about its state
/// (context-compression [001]).
module ProviderTokenUse =

    type Usage = { Cached: int; Uncached: int }

    // DSL-MUTABLE: resource — sessionId → last assistant turn usage
    let private uses = Dictionary<string, Usage>()

    let record (sessionId: string) (cached: int) (uncached: int) : unit =
        uses.[sessionId] <- { Cached = cached; Uncached = uncached }

    let tryFind (sessionId: string) : Usage option =
        match uses.TryGetValue sessionId with
        | true, usage -> Some usage
        | false, _ -> None

    let clearSession (sessionId: string) : unit = uses.Remove sessionId |> ignore

    let clearAll () : unit = uses.Clear()

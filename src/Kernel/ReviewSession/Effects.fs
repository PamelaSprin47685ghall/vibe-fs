module Wanxiangshu.Kernel.ReviewSession.Effects

open Wanxiangshu.Kernel.ReviewSession.Types

type SessionEffects =
    { pendingResolutions: Map<string, ReviewResult -> unit>
      abortSuppressors: Map<string, unit -> unit> }

let emptyEffects: SessionEffects =
    { pendingResolutions = Map.empty
      abortSuppressors = Map.empty }

let setPending effects sessionId resolve =
    { effects with
        pendingResolutions = Map.add sessionId resolve effects.pendingResolutions }

let private fireClear effects id result =
    match Map.tryFind id effects.pendingResolutions with
    | None -> None
    | Some resolve ->
        resolve result

        let without =
            { effects with
                pendingResolutions = Map.remove id effects.pendingResolutions }

        match Map.tryFind id without.abortSuppressors with
        | Some suppress ->
            suppress ()

            Some
                { without with
                    abortSuppressors = Map.remove id without.abortSuppressors }
        | None -> Some without

let resolvePending (effects: SessionEffects) sessionId result : SessionEffects * bool =
    match fireClear effects sessionId result with
    | None -> effects, false
    | Some next -> next, true

let private clearOrphanSuppressor (effects: SessionEffects) id : SessionEffects =
    match Map.tryFind id effects.abortSuppressors with
    | None -> effects
    | Some suppress ->
        suppress ()

        { effects with
            abortSuppressors = Map.remove id effects.abortSuppressors }

let disposeSessionTree (effects: SessionEffects) sessionIds : SessionEffects =
    sessionIds
    |> List.fold
        (fun acc id ->
            let afterPending =
                if not (Map.containsKey id acc.pendingResolutions) then
                    acc
                else
                    match fireClear acc id Terminated with
                    | None -> acc
                    | Some next -> next

            clearOrphanSuppressor afterPending id)
        effects

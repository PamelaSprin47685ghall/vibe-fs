namespace Wanxiangshu.Enforcer.InstitutionalLearning

open Wanxiangshu.Foundation.Identity

type LearningRecord =
    { OccurrenceId: string
      Kind: ExperienceKind
      Experience: string
      RulebookRevision: string
      Disposition: LearningDisposition
      FrozenResult: string
      ResurfacedDeferredWorkIds: string list }

type InstitutionalLearningProjectionState =
    { BySession: Map<SessionId, Map<string, LearningRecord>> }

[<RequireQualifiedAccess>]
module InstitutionalLearningProjection =

    let empty = { BySession = Map.empty }

    let tryFind sessionId occurrenceId state =
        state.BySession
        |> Map.tryFind sessionId
        |> Option.bind (Map.tryFind occurrenceId)

    let apply fact state =
        match fact with
        | InstitutionalLearningFactCases.LearningDispositionCommitted payload ->
            let current = Map.tryFind payload.SessionId state.BySession |> Option.defaultValue Map.empty

            if Map.containsKey payload.OccurrenceId current then
                state
            else
                let record =
                    { OccurrenceId = payload.OccurrenceId
                      Kind = payload.Kind
                      Experience = payload.Experience
                      RulebookRevision = payload.RulebookRevision
                      Disposition = payload.Disposition
                      FrozenResult = payload.FrozenResult
                      ResurfacedDeferredWorkIds = payload.ResurfacedDeferredWorkIds }

                { state with
                    BySession = Map.add payload.SessionId (Map.add payload.OccurrenceId record current) state.BySession }


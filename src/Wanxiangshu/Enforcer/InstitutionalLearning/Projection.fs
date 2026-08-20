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

    let private applyCommitted
        sessionId
        occurrenceId
        kind
        experience
        rulebookRevision
        disposition
        frozenResult
        resurfacedDeferredWorkIds
        state
        =
        let current = Map.tryFind sessionId state.BySession |> Option.defaultValue Map.empty

        if Map.containsKey occurrenceId current then
            state
        else
            let record =
                { OccurrenceId = occurrenceId
                  Kind = kind
                  Experience = experience
                  RulebookRevision = rulebookRevision
                  Disposition = disposition
                  FrozenResult = frozenResult
                  ResurfacedDeferredWorkIds = resurfacedDeferredWorkIds }

            { state with
                BySession = Map.add sessionId (Map.add occurrenceId record current) state.BySession }

    let apply fact state =
        match fact with
        | InstitutionalLearningFactCases.LearningDispositionCommitted payload ->
            applyCommitted
                payload.SessionId
                payload.OccurrenceId
                payload.Kind
                payload.Experience
                payload.RulebookRevision
                payload.Disposition
                payload.FrozenResult
                payload.ResurfacedDeferredWorkIds
                state

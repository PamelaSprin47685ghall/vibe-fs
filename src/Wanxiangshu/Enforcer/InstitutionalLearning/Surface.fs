namespace Wanxiangshu.Enforcer.InstitutionalLearning

open Wanxiangshu.Enforcer
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module InstitutionalLearningSurface =

    type private BoxedState(state: InstitutionalLearningProjectionState) =
        member _.State = state

    let private stateOf (value: obj) = (unbox<BoxedState> value).State
    let private boxed state = BoxedState(state) :> obj

    let private dispositionName disposition =
        match disposition with
        | LearningDisposition.Absorb _ -> "ABSORB"
        | LearningDisposition.Birth _ -> "BIRTH"
        | LearningDisposition.Discard _ -> "DISCARD"

    let evaluate (experience: string) (ruleNames: string array) : obj =
        let rules =
            ruleNames
            |> Array.mapi (fun index name ->
                { Name = name
                  EnforcerText = "rule"
                  MainText = "main"
                  RuleId = name
                  FieldName = name
                  LexicalOrder = index + 1 })
            |> Array.toList

        let disposition = InstitutionalEnhancer.evaluate experience rules
        box {| disposition = dispositionName disposition |}

    let revision (ruleNames: string array) =
        ruleNames
        |> Array.mapi (fun index name ->
            { Name = name
              EnforcerText = "rule"
              MainText = "main"
              RuleId = name
              FieldName = name
              LexicalOrder = index + 1 })
        |> Array.toList
        |> InstitutionalEnhancer.rulebookRevision

    let empty () = boxed InstitutionalLearningProjection.empty

    let commit session occurrence kind experience revision disposition frozen resurfaced state =
        let kind = if kind = "celebrate" then ExperienceKind.Celebrate else ExperienceKind.Regret
        let disposition =
            if disposition = "ABSORB" then LearningDisposition.Absorb "existing"
            elif disposition = "BIRTH" then LearningDisposition.Birth "candidate"
            else LearningDisposition.Discard "discarded"

        let fact =
            InstitutionalLearningFactCases.LearningDispositionCommitted
                {| SessionId = SessionId.create session
                   OccurrenceId = occurrence
                   Kind = kind
                   Experience = experience
                   RulebookRevision = revision
                   Disposition = disposition
                   FrozenResult = frozen
                   ResurfacedDeferredWorkIds = Array.toList resurfaced |}

        stateOf state |> InstitutionalLearningProjection.apply fact |> boxed

    let frozen session occurrence state : obj =
        match InstitutionalLearningProjection.tryFind (SessionId.create session) occurrence (stateOf state) with
        | Some record -> box record.FrozenResult
        | None -> null


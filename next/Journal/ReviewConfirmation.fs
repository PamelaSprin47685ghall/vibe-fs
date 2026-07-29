namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode

module internal ReviewConfirmation =

    let isSecondPerfectConfirmed
        (projection: AgentProjectionSet)
        (existing: ReviewGuardProjection)
        (reviewerSessionId: SessionId)
        (providerRunId: string)
        (userMessageId: string option)
        =
        let providerRunUsed = List.contains providerRunId existing.RecentProviderRunIds
        let hasValidProviderRunId = not (String.IsNullOrWhiteSpace providerRunId)

        let reviewerAuthority =
            Map.tryFind reviewerSessionId projection.Sessions
            |> Option.bind (fun reviewer -> reviewer.PromptAuthority)

        let physicalConfirmationMatched =
            match userMessageId with
            | Some userMsg when not (String.IsNullOrWhiteSpace userMsg) ->
                let messageId = MessageId.create userMsg

                let acceptedReviewConfirmation =
                    reviewerAuthority
                    |> Option.bind (fun authority -> Map.tryFind messageId authority.AcceptedContinuationIds)
                    |> Option.exists ((=) PromptAuthority.ReviewConfirmation)

                let confirmationRootMatchesFirst =
                    match existing.AuthorityRootUserMessageId, reviewerAuthority with
                    | Some firstRoot, Some authority ->
                        match Map.tryFind messageId authority.AcceptedContinuationRoots with
                        | Some root -> MessageId.value root = firstRoot
                        | None -> false
                    | _ -> false

                acceptedReviewConfirmation
                || confirmationRootMatchesFirst
                || (existing.ConfirmationPhysicalMessageId
                    |> Option.exists (fun confirmMsg ->
                        not (String.IsNullOrWhiteSpace confirmMsg) && userMsg = confirmMsg))
            | _ -> false

        // Same-root reevaluation is valid only when the second physical user
        // message is still the first PERFECT root. A ReviewConfirmation has a
        // distinct physical id and must go through the confirmation path above.
        let samePhysicalRootReevaluationMatched =
            match existing.AuthorityRootUserMessageId, userMessageId with
            | Some firstRoot, Some currentRoot ->
                ReviewWitness.isPerfectPending existing.Witness
                && not (String.IsNullOrWhiteSpace firstRoot)
                && firstRoot = currentRoot
            | _ -> false

        hasValidProviderRunId
        && not providerRunUsed
        && ReviewWitness.isPerfectPending existing.Witness
        && (physicalConfirmationMatched || samePhysicalRootReevaluationMatched)

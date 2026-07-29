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

        let physicalConfirmationMatched =
            match userMessageId with
            | Some userMsg when not (String.IsNullOrWhiteSpace userMsg) ->
                let acceptedReviewConfirmation =
                    Map.tryFind reviewerSessionId projection.Sessions
                    |> Option.bind (fun reviewer -> reviewer.PromptAuthority)
                    |> Option.bind (fun authority ->
                        Map.tryFind (MessageId.create userMsg) authority.AcceptedContinuationIds)
                    |> Option.exists ((=) PromptAuthority.ReviewConfirmation)

                acceptedReviewConfirmation
                || (existing.ConfirmationPhysicalMessageId
                    |> Option.exists (fun confirmMsg ->
                        not (String.IsNullOrWhiteSpace confirmMsg) && userMsg = confirmMsg))
            | _ -> false

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

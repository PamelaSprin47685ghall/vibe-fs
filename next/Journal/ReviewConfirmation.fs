namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode

module internal ReviewConfirmation =

    let isSecondPerfectConfirmed
        (projection: AgentProjectionSet)
        (existing: ReviewGuardProjection)
        (reviewerSessionId: SessionId)
        (providerRunId: string)
        (userPromptText: string option)
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

        let confirmationPending =
            match existing.AcceptedGuardKey with
            | Some key when key.IndexOf("confirm-perfect", StringComparison.OrdinalIgnoreCase) >= 0 -> true
            | _ -> existing.ConfirmationPhysicalMessageId.IsSome

        let markerConfirmationMatched =
            match userPromptText with
            | Some text when
                not (String.IsNullOrWhiteSpace text)
                && confirmationPending
                && text.IndexOf("PERFECT requires confirmation", StringComparison.Ordinal) >= 0
                ->
                true
            | _ -> false

        let acceptedConfirmSecondPerfect =
            confirmationPending && existing.ConsecutivePerfects = 1 && not providerRunUsed

        hasValidProviderRunId
        && not providerRunUsed
        && (physicalConfirmationMatched || markerConfirmationMatched || acceptedConfirmSecondPerfect)

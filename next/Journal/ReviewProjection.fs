namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity

type GitTreeHash = private GitTreeHash of string

module GitTreeHash =
    let create value = GitTreeHash value
    let value (GitTreeHash value) = value

type ReviewGuardProjection =
    { LastGitTreeHash: GitTreeHash option
      Witness: ReviewWitness
      ConfirmedReviewerSessionId: SessionId option
      ConfirmedProviderRunId: string option
      AcceptedGuardKey: string option
      RecentToolCallIds: string list
      RecentProviderRunIds: string list
      ConfirmationPhysicalMessageId: string option
      AuthorityRootUserMessageId: string option
      CurrentBarrierKey: string option }

    member this.IsConfirmed = ReviewWitness.isConfirmed this.Witness

type ReviewRequirementInput =
    { SourceSessionId: SessionId
      MessageId: MessageId }

type ReviewRequirementProjection =
    { HumanPromptInputs: ReviewRequirementInput list
      LastConfirmedIdleAssistantMessageId: MessageId option }

/// Double-PERFECT witnesses for one review barrier and Git tree.
module ReviewProjection =

    let private recentWindowSize = 2

    let private remember value values =
        if List.contains value values then
            values
        else
            let next = values @ [ value ]

            if List.length next > recentWindowSize then
                List.skip (List.length next - recentWindowSize) next
            else
                next

    let empty barrierKey =
        { LastGitTreeHash = None
          Witness = ReviewWitness.NoReview
          ConfirmedReviewerSessionId = None
          ConfirmedProviderRunId = None
          AcceptedGuardKey = None
          RecentToolCallIds = []
          RecentProviderRunIds = []
          ConfirmationPhysicalMessageId = None
          AuthorityRootUserMessageId = None
          CurrentBarrierKey = barrierKey }

    let startBarrier barrierKey current =
        match current with
        | Some existing when existing.CurrentBarrierKey = Some barrierKey -> existing
        | _ -> empty (Some barrierKey)

    let private revision tree projection =
        { projection with
            LastGitTreeHash = Some(GitTreeHash.create tree)
            Witness = ReviewWitness.RevisionWitness {| Report = ""; GitTreeHash = tree |}
            ConfirmedReviewerSessionId = None
            ConfirmedProviderRunId = None
            ConfirmationPhysicalMessageId = None
            AuthorityRootUserMessageId = None }

    let private firstPerfect providerRunId toolCallId treeHash userMessageId projection =
        { projection with
            LastGitTreeHash = Some(GitTreeHash.create treeHash)
            Witness =
                ReviewWitness.PerfectPending
                    { ProviderRunId = providerRunId
                      ToolCallId = toolCallId
                      GitTreeHash = treeHash
                      AuthorityRootUserMessageId = userMessageId
                      UserMessageId = userMessageId }
            ConfirmedReviewerSessionId = None
            ConfirmedProviderRunId = None
            ConfirmationPhysicalMessageId = None
            AuthorityRootUserMessageId = userMessageId }

    let recordVerdict
        secondPerfectConfirmed
        (p:
            {| ManagerSessionId: SessionId
               ReviewerSessionId: SessionId
               ProviderRunId: string
               UserPromptText: string option
               UserMessageId: string option
               ToolCallId: string
               GitTreeHash: string
               Verdict: ReviewGuardVerdict |})
        current
        =
        let existing = defaultArg current (empty None)

        if List.contains p.ToolCallId existing.RecentToolCallIds then
            existing
        else
            let providerRunUsed = List.contains p.ProviderRunId existing.RecentProviderRunIds

            let baseline =
                { existing with
                    Witness = ReviewWitness.invalidateByTreeChange existing.Witness p.GitTreeHash
                    LastGitTreeHash = Some(GitTreeHash.create p.GitTreeHash)
                    RecentToolCallIds = remember p.ToolCallId existing.RecentToolCallIds
                    RecentProviderRunIds = remember p.ProviderRunId existing.RecentProviderRunIds }

            match p.Verdict with
            | ReviewGuardVerdict.Revise -> revision p.GitTreeHash baseline
            | ReviewGuardVerdict.Perfect ->
                match baseline.Witness with
                | ReviewWitness.Confirmed _ -> baseline
                | ReviewWitness.PerfectPending first when secondPerfectConfirmed && not providerRunUsed ->
                    let barrier =
                        baseline.CurrentBarrierKey
                        |> Option.defaultValue (
                            sprintf "implicit:%s:%s" (SessionId.value p.ManagerSessionId) first.ToolCallId
                        )

                    { baseline with
                        Witness =
                            ReviewWitness.Confirmed
                                {| BarrierId = ReviewBarrierId barrier
                                   First = first
                                   Second =
                                    { ProviderRunId = p.ProviderRunId
                                      ToolCallId = p.ToolCallId
                                      GitTreeHash = p.GitTreeHash
                                      AuthorityRootUserMessageId = p.UserMessageId
                                      UserMessageId = p.UserMessageId }
                                   TreeHash = p.GitTreeHash |}
                        ConfirmedReviewerSessionId = Some p.ReviewerSessionId
                        ConfirmedProviderRunId = Some p.ProviderRunId }
                | ReviewWitness.PerfectPending _ -> baseline
                | _ when providerRunUsed -> baseline
                | _ -> firstPerfect p.ProviderRunId p.ToolCallId p.GitTreeHash p.UserMessageId baseline

    let acceptGuard guardKey hostMessageId isConfirmation current =
        let existing = defaultArg current (empty None)

        let isAdmission (value: string option) =
            match value with
            | Some text -> text.StartsWith("accepted-")
            | None -> false

        let confirmationId =
            if not isConfirmation then
                existing.ConfirmationPhysicalMessageId
            elif isAdmission hostMessageId then
                // Keep any real Host message id already recorded; admission ids
                // are temporary transport tokens and must not replace them.
                match existing.ConfirmationPhysicalMessageId with
                | Some currentId when not (isAdmission (Some currentId)) -> Some currentId
                | _ -> hostMessageId
            else
                hostMessageId

        { existing with
            AcceptedGuardKey = Some guardKey
            ConfirmationPhysicalMessageId = confirmationId }

    let addRequirement sourceSessionId messageId current =
        let input =
            { SourceSessionId = sourceSessionId
              MessageId = MessageId.create messageId }

        match current with
        | Some existing when List.contains input existing.HumanPromptInputs -> existing
        | Some existing ->
            { existing with
                HumanPromptInputs = existing.HumanPromptInputs @ [ input ] }
        | None ->
            { HumanPromptInputs = [ input ]
              LastConfirmedIdleAssistantMessageId = None }

    let confirmIdle assistantMessageId current =
        let assistant = MessageId.create assistantMessageId

        match current with
        | Some existing when existing.LastConfirmedIdleAssistantMessageId = Some assistant -> existing
        | Some existing ->
            { existing with
                HumanPromptInputs = []
                LastConfirmedIdleAssistantMessageId = Some assistant }
        | None ->
            { HumanPromptInputs = []
              LastConfirmedIdleAssistantMessageId = Some assistant }

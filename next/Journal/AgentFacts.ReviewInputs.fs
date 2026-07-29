namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open AgentFactsFoldHelpers

module internal AgentFactsReviewInputs =

    let foldHumanPromptAccepted
        (proj: AgentProjectionSet)
        (p:
            {| SessionId: SessionId
               SourceSessionId: SessionId
               MessageId: string |})
        : AgentProjectionSet =
        let input =
            { SourceSessionId = p.SourceSessionId
              MessageId = MessageId.create p.MessageId }

        let sessions =
            updateSession
                p.SessionId
                (fun session ->
                    let requirements =
                        match session.ReviewRequirements with
                        | Some existing when List.contains input existing.HumanPromptInputs -> existing
                        | Some existing ->
                            { existing with
                                HumanPromptInputs = existing.HumanPromptInputs @ [ input ] }
                        | None ->
                            { HumanPromptInputs = [ input ]
                              LastConfirmedIdleAssistantMessageId = None }

                    { session with ReviewRequirements = Some requirements })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldReviewConfirmedIdle
        (proj: AgentProjectionSet)
        (p:
            {| SessionId: SessionId
               ReviewerSessionId: SessionId
               AssistantMessageId: string |})
        : AgentProjectionSet =
        let assistantMessageId = MessageId.create p.AssistantMessageId

        let sessions =
            updateSession
                p.SessionId
                (fun session ->
                    let requirements =
                        match session.ReviewRequirements with
                        | Some existing when existing.LastConfirmedIdleAssistantMessageId = Some assistantMessageId ->
                            existing
                        | Some existing ->
                            { existing with
                                HumanPromptInputs = []
                                LastConfirmedIdleAssistantMessageId = Some assistantMessageId }
                        | None ->
                            { HumanPromptInputs = []
                              LastConfirmedIdleAssistantMessageId = Some assistantMessageId }

                    { session with ReviewRequirements = Some requirements })
                proj.Sessions

        { proj with Sessions = sessions }

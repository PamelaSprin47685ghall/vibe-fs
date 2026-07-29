namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Identity

/// Prompt Authority projection entry points. Host I/O and journal append remain
/// outside; every operation is a pure fold over one session's projection.
module AuthorityProjection =

    let empty = PromptAuthorityLedger.empty

    let acceptRoot
        current
        (p:
            {| SessionId: SessionId
               LogicalRunId: string
               HostMessageId: string
               AuthorityKind: string
               SelectedAgent: string
               PeerAgent: string
               CanonicalRole: string
               SelectedTier: string |})
        =
        PromptAuthorityLedger.foldAuthorityRootAccepted (defaultArg current empty) p

    let claim
        current
        (p:
            {| PromptKey: string
               SessionId: SessionId
               LogicalRunId: string
               AuthorityRootUserMessageId: string
               ContinuationKind: string
               EffectiveAgent: string option |})
        =
        PromptAuthorityLedger.foldPluginPromptClaimed (defaultArg current empty) p

    let accept
        current
        (p:
            {| PromptKey: string
               SessionId: SessionId
               HostMessageId: string |})
        =
        PromptAuthorityLedger.foldPluginPromptAccepted (defaultArg current empty) p

    let abandon
        current
        (p:
            {| PromptKey: string
               SessionId: SessionId
               Reason: string |})
        =
        PromptAuthorityLedger.foldPluginPromptAbandoned (defaultArg current empty) p

    let claimRepair
        current
        (p:
            {| SessionId: SessionId
               LogicalRunId: string
               AuthorityRootUserMessageId: string
               TerminalAssistantMessageId: string
               RepairKind: string |})
        =
        PromptAuthorityLedger.foldInteractionRepairClaimed (defaultArg current empty) p

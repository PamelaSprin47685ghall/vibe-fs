namespace Wanxiangshu.Composition.Turn

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

/// Consumer-side journal reads needed by OrdinaryTurnWorkflow.
/// Each member performs exactly one journal snapshot read at call time;
/// multiple fields within one member share that snapshot.
/// `PromptContinuationKind` is the type aliased as
/// `PromptAuthority.ContinuationKind`.
type TurnObservationJournalPort =
    {
        TryBloggerReceiptKind: SessionId -> ProviderRunIdentity -> BlogFrameKind option
        TryContinuationKind: SessionId -> PhysicalUserMessageId -> PromptContinuationKind option
        /// interaction-authority-023: exact durable settlement of the attempt owning
        /// this physical user message — a `ChatExecution` Terminal fact exists.
        HasExecutionTerminal: SessionId -> PhysicalUserMessageId -> bool
        IsFissionActive: SessionId -> bool
    }

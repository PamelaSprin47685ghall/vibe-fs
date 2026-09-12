namespace Wanxiangshu.Composition.Turn

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

/// Consumer-side journal reads needed by OrdinaryTurnWorkflow.
/// Each member derives from exactly one Journal Snapshot revision captured
/// when the port is built (see AgentJournalPortAdapter.forTurnObservation).
/// `PromptContinuationKind` is the type aliased as
/// `PromptAuthority.ContinuationKind`.
type TurnObservationJournalPort =
    { TryBloggerReceiptKind: SessionId -> ProviderRunIdentity -> BlogFrameKind option
      TryContinuationKind: SessionId -> PhysicalUserMessageId -> PromptContinuationKind option
      IsFissionActive: SessionId -> bool }

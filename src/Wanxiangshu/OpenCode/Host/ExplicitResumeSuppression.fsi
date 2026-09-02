namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch.OpenCode

/// CRASH-018 marker for the exact Host user material produced by `/continue`.
///
/// A SessionId is a reusable container and is therefore not a valid suppression
/// lifetime. The durable semantic marker rides on the visible text part itself.
/// The process-local registry below only remembers which exact physical material
/// carried that marker so later reconcile/idle passes can recognize the same turn;
/// a new unmarked PhysicalUserMessageId for the same SessionId clears it immediately.
/// No session-end/idle/abort signal is required for correctness.
module ExplicitResumeSuppression =

    [<RequireQualifiedAccess>]
    type PhysicalMaterialObservation =
        | ExplicitResume
        | ReplacedExplicitResume
        | Ordinary

    [<RequireQualifiedAccess>]
    type BriefingMaterialization =
        | ExplicitResume
        | Ordinary

    [<Literal>]
    val MetadataKey: string = "wanxiangshu_explicit_resume"

    val markedTextPart: text: string -> obj

    val stageBriefing: sessionId: SessionId -> materialWitness: string -> text: string -> unit

    /// Materialize the staged disclosure on the real chat.message material.
    /// Hosts that already forwarded command output carry the marker themselves;
    /// in that case the pending handoff is only consumed, never duplicated.
    val materializePendingBriefing: sessionId: SessionId -> output: obj -> BriefingMaterialization

    /// chat.message is the physical-material boundary. Same marked material keeps
    /// its suppression across provider retries; a later ordinary user material on
    /// the same SessionId removes it immediately.
    val observePhysicalMaterial:
        sessionId: SessionId -> physicalId: PhysicalUserMessageId -> output: obj -> PhysicalMaterialObservation

    val isPhysicalMaterial: sessionId: SessionId -> physicalId: PhysicalUserMessageId -> bool

    val hasMarkedPhysicalMaterial: sessionId: SessionId -> bool

    /// CRASH-018 chat.message classification. Materialization and exact-physical
    /// replay knowledge are one owner decision; Host wiring must not reconstruct
    /// the precedence between them.
    val classifyChatMessage: decoded: PromptIngressCodec.DecodedMessage -> output: obj -> bool

    /// The exact physical material registry decides whether reconciliation must
    /// bind this user material. Both a marked resume and the first ordinary
    /// replacement change the binding boundary.
    val requiresPhysicalBinding: sessionId: SessionId -> physicalId: PhysicalUserMessageId -> output: obj -> bool

    /// CRASH-018: Check if the trailing user message in the transform output
    /// is an explicit resume binding for the given session.
    /// Domain decision: determines whether material is /continue disclosure.
    val isExplicitResumeBinding: projectionSessionIdOpt: string option -> outObj: obj -> bool

    /// Cleanup only. Exact-new-material replacement is the correctness boundary.
    val dropSession: sessionId: SessionId -> unit

    val isCurrentMaterial: output: obj -> bool

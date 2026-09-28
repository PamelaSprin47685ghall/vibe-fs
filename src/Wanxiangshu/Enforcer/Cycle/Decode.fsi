namespace Wanxiangshu.Enforcer.Cycle

open Wanxiangshu.Foundation.Identity

module EnforcerCycleDecode =

    [<Literal>]
    val EmptyTextError: string = "blog cycle text is empty after canonicalisation (ENFORCER-043)"

    /// One Host assistant message: the provider step it recorded.
    type AssistantStep =
        { MessageId: string
          Parts: obj list
          Completed: bool }

    /// Where the provider step being built stands relative to the physical
    /// user message it answers (Host `MessageV2.latest` + assistant parentID).
    [<RequireQualifiedAccess>]
    type StepPosition =
        | NoRequest
        | First of physical: PhysicalUserMessageId
        | After of physical: PhysicalUserMessageId * previous: AssistantStep

    val stepPosition: obj list -> StepPosition

    /// The history-tail assistant, whatever physical message it answers.
    val latestAssistant: obj list -> AssistantStep option

    val callsOf:
        emitDiagnostic: (string -> (string * string) list -> unit) ->
        step: AssistantStep ->
            (int * ToolCallId * Wanxiangshu.Enforcer.EnforcerCodec.CanonicalBlogCall) list

    val validateCycle:
        string ->
        (int * ToolCallId * Wanxiangshu.Enforcer.EnforcerCodec.CanonicalBlogCall) list ->
            Result<(Wanxiangshu.Enforcer.Cycle.EnforcerCycle.CanonicalCycle * ToolCallId list), string>

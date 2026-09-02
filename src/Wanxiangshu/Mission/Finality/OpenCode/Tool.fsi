namespace Wanxiangshu.Mission.Finality.OpenCode

open Wanxiangshu.OpenCode

/// GLORY-034/035/037/041: the Manager's end-of-life tool.
///
/// The tool is deliberately opaque to the Manager: the description never
/// mentions review, the reviewer, PERFECT, or the barrier (SURFACE-005). A
/// legal call validates the immediate contract, persists submitted last_words,
/// builds Host ports, and enters Application `FinalityWorkflow`; Application owns
/// `FinalityRequested` and every later lifecycle fact. Every precondition failure
/// returns a narrative refusal before a new Finality request is admitted
/// (GLORY-038/039).
module FinalityTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/suicide/description"

        [<Literal>]
        val TryAgainLater: string = "tool/suicide/try-again-later"

        [<Literal>]
        val ContinueWorking: string = "tool/suicide/continue-working"

        [<Literal>]
        val CallAgainWithLastWords: string = "tool/suicide/call-again-with-last-words"

        [<Literal>]
        val CallJoinBeforeEnd: string = "tool/suicide/call-join-before-end"

        [<Literal>]
        val SeekEndWhenReady: string = "tool/suicide/seek-end-when-ready"

        [<Literal>]
        val WaitForCurrentEnding: string = "tool/suicide/wait-for-current-ending"

        [<Literal>]
        val WrongRole: string = "tool/suicide/wrong-role"

    val admission: ToolAdmission

    val spec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec

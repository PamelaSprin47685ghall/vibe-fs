namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Participant.Cognition

module AssumeTool =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/assume/description"

        [<Literal>]
        val ArgUpdate: string = "tool/assume/arg-update"

        [<Literal>]
        val ArgTodos: string = "tool/assume/arg-todos"

    val admission: ToolAdmission

    val executeWith:
        runtime: CognitiveRuntime ->
        resolveOwner: (HostToolContext -> CognitiveOwner.T option) ->
        HostToolArguments ->
        HostToolContext ->
            Task<string>

    val spec:
        factory: HostToolFactory ->
        runtime: CognitiveRuntime ->
        resolveOwner: (HostToolContext -> CognitiveOwner.T option) ->
            ToolSpec

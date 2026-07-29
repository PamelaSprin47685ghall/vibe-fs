namespace Wanxiangshu.Next.OpenCode

open Thoth.Json

/// Synchronous read-only Inspector delegation used by Coder, Reviewer,
/// Meditator, and DevOps. The child is always disposed after one terminal.
module InspectorTool =

    let private encode (outcome: OneShotAgentTool.Outcome) =
        let managed = outcome.Managed

        ToolHostCodec.jsonObject
            [ "inspectorId", Encode.string outcome.ChildId
              "agent", Encode.string managed.Name
              "tier", Encode.string (ManagedAgent.tierName managed.Tier)
              "fallbackPeer", Encode.string (ManagedAgent.peer managed).Name
              "parentBDigest",
              outcome.ParentBackgroundDigest
              |> Option.map Encode.string
              |> Option.defaultValue (Encode.string "")
              "output", Encode.string outcome.Output ]

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        let request: OneShotAgentTool.Request =
            { Agent = args.Text "agent"
              Prompt = OneShotAgentTool.promptFrom args }

        task {
            match!
                OneShotAgentTool.run
                    scope
                    context
                    request
                    ManagedAgent.inspectorToolNames
                    "Inspector"
            with
            | Ok outcome -> return encode outcome
            | Error error -> return ToolHostCodec.jsonObject [ "error", Encode.string error ]
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "inspector"
          Description = "One-shot read-only investigation; session is disposed after return"
          Arguments =
            [ "agent", ToolHostCodec.enumSchema ManagedAgent.inspectorToolNames factory
              "prompt", ToolHostCodec.optionalStringSchema factory
              "prompts", ToolHostCodec.optionalStringArraySchema factory ]
          Execute = execute scope }

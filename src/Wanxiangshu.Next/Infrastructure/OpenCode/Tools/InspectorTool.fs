namespace Wanxiangshu.Next.OpenCode

open ToolHostCodec

/// Synchronous read-only Inspector delegation used by Coder, Reviewer,
/// Meditator, and DevOps. The child is always disposed after one terminal.
module InspectorTool =

    let private encode (outcome: OneShotAgentTool.Outcome) =
        let managed = outcome.Managed
        let report = outcome.Output

        let instructions =
            if System.String.IsNullOrWhiteSpace report then
                []
            else
                [ report ]

        tomlObjectWithInstructions
            instructions
            [ "inspector_id", TString outcome.ChildId
              "agent", TString managed.Name
              "tier", TString(ManagedAgent.tierName managed.Tier)
              "fallback_peer", TString (ManagedAgent.peer managed).Name
              "parent_b_digest",
              outcome.ParentBackgroundDigest
              |> Option.map TString
              |> Option.defaultValue (TString "") ]

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        let request: OneShotAgentTool.Request =
            { Agent = args.Text "agent"
              Prompt = OneShotAgentTool.promptFrom args }

        task {
            match! OneShotAgentTool.run scope context request ManagedAgent.inspectorToolNames "Inspector" with
            | Ok outcome -> return encode outcome
            | Error error -> return tomlObject [ "error", TString error ]
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "inspector"
          Description = "One-shot read-only investigation; session is disposed after return"
          Arguments =
            [ "agent", ToolHostCodec.enumSchema ManagedAgent.inspectorToolNames factory
              "prompt", ToolHostCodec.optionalStringSchema factory
              "prompts", ToolHostCodec.optionalStringArraySchema factory ]
          Execute = execute scope }

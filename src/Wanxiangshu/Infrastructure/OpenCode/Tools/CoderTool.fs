namespace Wanxiangshu.OpenCode

open ToolHostCodec

/// DevOps synchronous Coder delegation. Host argument decoding and JS schema
/// assembly stay in ToolHostCodec; this file owns the Coder contract.
module CoderTool =

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
            [ "coder_id", TString outcome.ChildId
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
            match! OneShotAgentTool.run scope context request ManagedAgent.coderToolNames "Coder" with
            | Ok outcome -> return encode outcome
            | Error error -> return tomlObject [ "error", TString error ]
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "coder"
          Description =
            "One-shot Coder implementation; session is disposed after return. Use this instead of write/edit."
          Arguments =
            [ "agent", ToolHostCodec.enumSchema ManagedAgent.coderToolNames factory
              "prompt", ToolHostCodec.optionalStringSchema factory
              "prompts", ToolHostCodec.optionalStringArraySchema factory ]
          Execute = execute scope }

namespace Wanxiangshu.Next.OpenCode

open Thoth.Json

/// DevOps synchronous Coder delegation. Host argument decoding and JS schema
/// assembly stay in ToolHostCodec; this file owns the Coder contract.
module CoderTool =

    let private encode (outcome: OneShotAgentTool.Outcome) =
        let managed = outcome.Managed

        ToolHostCodec.jsonObject
            [ "coderId", Encode.string outcome.ChildId
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
                    ManagedAgent.coderToolNames
                    "Coder"
            with
            | Ok outcome -> return encode outcome
            | Error error -> return ToolHostCodec.jsonObject [ "error", Encode.string error ]
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "coder"
          Description = "One-shot Coder implementation; session is disposed after return. Use this instead of write/edit."
          Arguments =
            [ "agent", ToolHostCodec.enumSchema ManagedAgent.coderToolNames factory
              "prompt", ToolHostCodec.optionalStringSchema factory
              "prompts", ToolHostCodec.optionalStringArraySchema factory ]
          Execute = execute scope }

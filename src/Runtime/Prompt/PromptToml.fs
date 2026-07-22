namespace Wanxiangshu.Runtime.Prompt

open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Serialization.TomlValue
open Wanxiangshu.Runtime.Serialization.Toml

module PromptToml =

    let agentRoleText =
        function
        | AgentRole.Implementation -> "Implementation Agent (mutating)"
        | AgentRole.CodebaseSearch -> "Codebase Search Agent (read-only)"
        | AgentRole.BrowserAutomation -> "Browser Automation Agent (read-only)"
        | AgentRole.CodeReview -> "Code Reviewer (read-only)"
        | AgentRole.ExecutorSummarization -> "Executor Output Summarizer (read-only)"
        | AgentRole.WebSearchSummarization -> "Web Search Summarizer (read-only)"
        | AgentRole.MethodologyReasoning -> "Methodology Reasoning Agent (read-only)"
        | AgentRole.NudgeSupervisor -> "Nudge Supervisor (synthetic)"
        | AgentRole.SquadWorker -> "Wanxiangzhen Slave Agent (mutating)"

    let timeoutKindText =
        function
        | TimeoutKind.Short -> "short"
        | TimeoutKind.Long -> "long"

    let boundaryTargetWire =
        function
        | BoundaryTarget.File path -> "file", path
        | BoundaryTarget.Directory path -> "dir", path
        | BoundaryTarget.PathOrSymbol value -> "path", value

    let target =
        function
        | FileTarget(path, guide, draft) ->
            let fields = [ "kind", String "file"; "value", String path; "hint", String guide ]

            let fields =
                match draft with
                | Some d -> fields @ [ "draft", String d ]
                | None -> fields

            Table fields
        | FileReference path -> Table [ "kind", String "file"; "value", String path ]
        | EntryTarget entry -> Table [ "kind", String "path"; "value", String entry ]
        | QueryTarget query -> Table [ "kind", String "query"; "value", String query ]
        | CommandTarget(language, program, dependencies, timeoutKind) ->
            Table
                [ "kind", String "command"
                  "language", String language
                  "program", String program
                  "dependencies", StringArray dependencies
                  "timeout_type", String(timeoutKindText timeoutKind) ]
        | EvidenceTarget(label, content) ->
            Table [ "kind", String "evidence"; "value", String label; "content", String content ]
        | TodoTarget content -> Table [ "kind", String "todo"; "value", String content ]

    let boundary =
        function
        | DoNotRead b ->
            let k, v = boundaryTargetWire b
            Table [ "kind", String k; "value", String v; "action", String "read" ]
        | DoNotModify b ->
            let k, v = boundaryTargetWire b
            Table [ "kind", String k; "value", String v; "action", String "modify" ]
        | DoNotExecute act -> Table [ "kind", String "action"; "value", String act; "action", String "execute" ]
        | DoNotTouch b ->
            let k, v = boundaryTargetWire b
            Table [ "kind", String k; "value", String v; "action", String "all" ]

    let rule =
        function
        | Policy text -> Table [ "kind", String "policy"; "text", String text ]
        | Constraint text -> Table [ "kind", String "constraint"; "text", String text ]
        | Criterion text -> Table [ "kind", String "criterion"; "text", String text ]
        | Question text -> Table [ "kind", String "question"; "text", String text ]
        | Contract text -> Table [ "kind", String "contract"; "text", String text ]

    let outcome (o: PromptOutcome) =
        Table [ "label", String o.label; "text", String o.text ]

    let document (doc: PromptDocument) : TomlValue =
        let v = PromptDocument.view doc
        let mutable fields = [ "objective", String v.objective ]

        match v.background with
        | Some bg -> fields <- fields @ [ "background", String bg ]
        | None -> ()

        fields <- fields @ [ "agent_role", String(agentRoleText v.agentRole) ]

        if not (List.isEmpty v.targets) then
            let tables =
                v.targets
                |> List.map (
                    target
                    >> function
                        | Table t -> t
                        | _ -> []
                )

            fields <- fields @ [ "targets", TableArray tables ]

        if not (List.isEmpty v.boundaries) then
            let tables =
                v.boundaries
                |> List.map (
                    boundary
                    >> function
                        | Table t -> t
                        | _ -> []
                )

            fields <- fields @ [ "boundaries", TableArray tables ]

        if not (List.isEmpty v.rules) then
            let tables =
                v.rules
                |> List.map (
                    rule
                    >> function
                        | Table t -> t
                        | _ -> []
                )

            fields <- fields @ [ "rules", TableArray tables ]

        if not (List.isEmpty v.outcomes) then
            let tables =
                v.outcomes
                |> List.map (
                    outcome
                    >> function
                        | Table t -> t
                        | _ -> []
                )

            fields <- fields @ [ "outcomes", TableArray tables ]

        Table fields

    let render (doc: PromptDocument) : string = document doc |> stringify

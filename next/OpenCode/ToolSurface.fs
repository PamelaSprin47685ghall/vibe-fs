namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

module ToolSurface =

    [<Emit("$0.schema.string()")>]
    let private stringSchema (tool: obj) : obj = jsNative

    [<Emit("$0.schema.enum($1)")>]
    let private enumSchema (tool: obj) (values: string array) : obj = jsNative

    [<Emit("$0($1)")>]
    let private applyTool (factory: obj) (definition: obj) : obj = jsNative

    [<Emit("(args, context) => $0(args)(context)")>]
    let private uncurriedExecute (fn: obj) : obj = jsNative

    [<Emit("Math.random().toString(36).slice(2, 8)")>]
    let private newAgentId () : string = jsNative

    [<Emit("JSON.stringify($0)")>]
    let private stringify (value: obj) : string = jsNative

    let private contextString (context: obj) (name: string) =
        if isNull context || isNull context?(name) then
            None
        else
            let value = unbox<string> context?(name)
            if String.IsNullOrWhiteSpace value then None else Some value

    let create
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (workspaceDirectory: string option)
        (sessionParents: Dictionary<string, string>)
        (sessionRoles: Dictionary<string, string>)
        (verdictSessions: HashSet<string>)
        : obj =
        let factory = toolModule?tool
        let runtimes = Dictionary<string, HostForkRuntime>()
        let reviewerHosts = Dictionary<string, ReviewerHost>()
        let gate = obj ()

        let runtimeFor (context: obj) =
            let sessionID =
                if isNull context || isNull context?sessionID then
                    ""
                else
                    unbox<string> context?sessionID

            if String.IsNullOrWhiteSpace sessionID then
                Error "Missing sessionID"
            else
                Ok(
                    lock gate (fun () ->
                        match runtimes.TryGetValue sessionID with
                        | true, runtime -> runtime
                        | false, _ ->
                            let runtime =
                                HostForkRuntime(SessionId.create sessionID, sessionPort, ?journal = journal)

                            runtimes.[sessionID] <- runtime
                            runtime)
                )

        let textArg (args: obj) (name: string) =
            if isNull args || isNull args?(name) then
                ""
            else
                unbox<string> args?(name)

        let forkExecute (args: obj) (context: obj) =
            task {
                match runtimeFor context with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let agent = textArg args "agent"
                    let prompt = textArg args "prompt"

                    let! result =
                        match HostSessionContext.roleOf agent with
                        | Some role -> runtime.Fork(newAgentId (), role, prompt)
                        | None -> runtime.Reuse(agent, prompt)

                    match result with
                    | Ok fork -> return box (stringify (createObj [ "agentId", box fork.AgentId ]))
                    | Error err -> return box (stringify (createObj [ "error", box err ]))
            }

        let joinExecute (_args: obj) (context: obj) =
            task {
                match runtimeFor context with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let! result = runtime.Join()

                    match result with
                    | Ok completion ->
                        return
                            box (
                                stringify (
                                    createObj
                                        [ "agentId", box completion.AgentId
                                          "runId", box completion.RunId
                                          "outcome", box completion.Outcome ]
                                )
                            )
                    | Error error -> return box (stringify (createObj [ "error", box (error.ToString()) ]))
            }

        let listExecute (_args: obj) (context: obj) =
            task {
                match runtimeFor context with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let agents, _ = runtime.List()

                    let result =
                        agents
                        |> List.map (fun agent ->
                            createObj
                                [ "agentId", box agent.AgentId
                                  "role", box (agent.Role.ToString())
                                  "status", box (agent.Status.ToString()) ])
                        |> List.toArray

                    return box (stringify (box result))
            }

        let verdictExecute (args: obj) (context: obj) =
            task {
                let sessionId = contextString context "sessionID"

                let role =
                    contextString context "agent"
                    |> Option.orElseWith (fun () ->
                        sessionId
                        |> Option.bind (fun id ->
                            match sessionRoles.TryGetValue id with
                            | true, value -> Some value
                            | false, _ -> None))

                let callId =
                    contextString context "toolCallId"
                    |> Option.orElse (contextString context "callID")

                let verdictResult =
                    if
                        role
                        |> Option.exists (fun value ->
                            not (value.Equals("reviewer", StringComparison.OrdinalIgnoreCase)))
                    then
                        Error "The verdict tool is available only to reviewer sessions"
                    elif sessionId.IsNone then
                        Error "Missing sessionID"
                    elif callId.IsNone then
                        Error "Missing tool call id"
                    elif isNull args || isNull args?verdict then
                        Error "Missing verdict"
                    else
                        try
                            StaticTools.reviewerVerdictOfString (unbox<string> args?verdict)
                        with _ ->
                            Error "verdict must be exactly PERFECT or REVISE"

                match verdictResult, sessionId, callId with
                | Error error, _, _ -> return box (stringify (createObj [ "error", box error ]))
                | Ok verdict, Some reviewerId, Some toolCallId ->
                    let managerId =
                        match sessionParents.TryGetValue reviewerId with
                        | true, parentId -> Some parentId
                        | false, _ ->
                            contextString context "managerSessionID"
                            |> Option.orElse (contextString context "managerSessionId")

                    match journal, managerId, gitTreePort with
                    | None, _, _ ->
                        return box (stringify (createObj [ "error", box "Reviewer verdict requires a journal" ]))
                    | _, None, _ ->
                        return
                            box (
                                stringify (
                                    createObj
                                        [ "error", box "Reviewer verdict requires the manager session relationship" ]
                                )
                            )
                    | _, _, None ->
                        return box (stringify (createObj [ "error", box "Reviewer verdict requires a GitTreePort" ]))
                    | Some journal, Some managerId, Some gitTreePort ->
                        let host =
                            lock gate (fun () ->
                                match reviewerHosts.TryGetValue reviewerId with
                                | true, existing -> existing
                                | false, _ ->
                                    let created =
                                        ReviewerHost(
                                            journal,
                                            SessionId.create managerId,
                                            SessionId.create reviewerId,
                                            ?gitTreePort = Some gitTreePort
                                        )

                                    reviewerHosts.[reviewerId] <- created
                                    created)

                        match host.SubmitVerdict(toolCallId, verdict) with
                        | Error error -> return box (stringify (createObj [ "error", box error ]))
                        | Ok result ->
                            lock gate (fun () -> verdictSessions.Add reviewerId |> ignore)

                            let status =
                                match result with
                                | ReviewFinishResult.Confirmed -> "CONFIRMED"
                                | ReviewFinishResult.NeedsReview -> "NEEDS_REVIEW"

                            return
                                box (
                                    stringify (
                                        createObj
                                            [ "verdict",
                                              box (
                                                  if verdict = ReviewGuardVerdict.Perfect then
                                                      "PERFECT"
                                                  else
                                                      "REVISE"
                                              )
                                              "status", box status ]
                                    )
                                )
                | Ok _, _, _ -> return box (stringify (createObj [ "error", box "Missing reviewer tool context" ]))
            }

        let verdictArgs =
            createObj [ "verdict", box (enumSchema factory [| "PERFECT"; "REVISE" |]) ]

        let definition description args execute =
            createObj
                [ "description", box description
                  "args", box args
                  "execute", uncurriedExecute (box execute) ]

        let forkArgs =
            createObj [ "agent", box (stringSchema factory); "prompt", box (stringSchema factory) ]

        let executor = ExecutorTool.create toolModule runtimeFor workspaceDirectory

        createObj
            [ "fork", box (applyTool factory (definition "Fork or nudge an agent" forkArgs forkExecute))
              "join", box (applyTool factory (definition "Wait for any agent completion" (createObj []) joinExecute))
              "list", box (applyTool factory (definition "List active agents" (createObj []) listExecute))
              "verdict",
              box (
                  applyTool
                      factory
                      (definition "Submit the review verdict for the current Git tree" verdictArgs verdictExecute)
              )
              "executor", executor ]

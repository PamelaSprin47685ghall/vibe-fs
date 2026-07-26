namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

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

    let private contextString (ctx: obj) (name: string) =
        if isNull ctx || isNull ctx?(name) then
            None
        else
            let v = unbox<string> ctx?(name) in if String.IsNullOrWhiteSpace v then None else Some v

    let private textArg (args: obj) (name: string) =
        if isNull args || isNull args?(name) then
            ""
        else
            unbox<string> args?(name)

    let private mkSid (s: string) = SessionId.create s

    let create
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (workspaceDirectory: string option)
        (sessionParents: Dictionary<string, string>)
        (sessionRoles: Dictionary<string, string>)
        (verdictSessions: HashSet<string>)
        (modelConfig: ModelResolver.ModelConfig option)
        : obj =
        let factory = toolModule?tool
        let runtimes = Dictionary<string, HostForkRuntime>()
        let reviewerHosts = Dictionary<string, ReviewerHost>()
        let gate = obj ()

        let runtimeFor (ctx: obj) =
            let sid =
                if isNull ctx || isNull ctx?sessionID then
                    ""
                else
                    unbox<string> ctx?sessionID

            if String.IsNullOrWhiteSpace sid then
                Error "Missing sessionID"
            else
                Ok(
                    lock gate (fun () ->
                        match runtimes.TryGetValue sid with
                        | true, r -> r
                        | false, _ ->
                            let r =
                                HostForkRuntime(
                                    mkSid sid,
                                    sessionPort,
                                    ?journal = journal,
                                    onChildCreated =
                                        (fun _ role childId ->
                                            let cid = SessionId.value childId
                                            sessionParents.[cid] <- sid
                                            sessionRoles.[cid] <- role.ToString().ToLowerInvariant()),
                                    ?modelResolver = modelConfig
                                )

                            runtimes.[sid] <- r
                            r)
                )

        let forkExecute (args: obj) (ctx: obj) =
            task {
                let agent = textArg args "agent"
                let prompt = textArg args "prompt"

                match runtimeFor ctx with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let! result =
                        match HostSessionContext.roleOf agent with
                        | Some role -> runtime.Fork(newAgentId (), role, prompt)
                        | None -> runtime.Reuse(agent, prompt)

                    match result with
                    | Ok fork -> return box (stringify (createObj [ "agentId", box fork.AgentId ]))
                    | Error err -> return box (stringify (createObj [ "error", box err ]))
            }

        let joinExecute (_args: obj) (ctx: obj) =
            task {
                match runtimeFor ctx with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let! result = runtime.Join()

                    match result with
                    | Ok c ->
                        return
                            box (
                                stringify (
                                    createObj
                                        [ "agentId", box c.AgentId; "runId", box c.RunId; "outcome", box c.Outcome ]
                                )
                            )
                    | Error e -> return box (stringify (createObj [ "error", box (e.ToString()) ]))
            }

        let listExecute (_args: obj) (ctx: obj) =
            task {
                match runtimeFor ctx with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let agents, _ = runtime.List()

                    let agentEntries =
                        agents
                        |> List.map (fun a ->
                            createObj
                                [ "agentId", box a.AgentId
                                  "role", box (a.Role.ToString())
                                  "status", box (a.Status.ToString()) ])

                    return box (stringify (box (agentEntries |> List.toArray)))
            }

        let verdictExecute =
            VerdictSurface.create sessionParents sessionRoles journal gitTreePort reviewerHosts verdictSessions

        let forkArgs =
            createObj [ "agent", box (stringSchema factory); "prompt", box (stringSchema factory) ]

        let verdictArgs =
            createObj [ "verdict", box (enumSchema factory [| "PERFECT"; "REVISE" |]) ]

        let definition desc args execute =
            createObj
                [ "description", box desc
                  "args", box args
                  "execute", uncurriedExecute (box execute) ]

        let executor = ExecutorTool.create toolModule runtimeFor workspaceDirectory

        createObj
            [ "fork", box (applyTool factory (definition "Fork or nudge an agent" forkArgs forkExecute))
              "join", box (applyTool factory (definition "Wait for any agent completion" (createObj []) joinExecute))
              "list", box (applyTool factory (definition "List active agents" (createObj []) listExecute))
              "verdict", box (applyTool factory (definition "Submit the review verdict" verdictArgs verdictExecute))
              "executor", executor ]

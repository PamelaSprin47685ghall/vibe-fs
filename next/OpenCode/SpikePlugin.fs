namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

type SpikePluginConfig =
    { Directory: string
      Port: IOpenCodePort option }

module SpikePlugin =

    [<Emit("import('@opencode-ai/plugin/tool')")>]
    let private importToolModule () : Task<obj> = jsNative

    [<Emit("(args, context) => $0(args)(context)")>]
    let private uncurriedExecute (fn: obj) : obj = jsNative

    let createSpikeHost (portOpt: IOpenCodePort option) = PluginHost.createSpikeHost portOpt

    let private systemTransformHook (sessionBudgets: Dictionary<string, int>) : obj =
        emitJsExpr
            sessionBudgets
            """
          (input, output) => {
            if (input && input.sessionID && input.model && input.model.limit && input.model.limit.context > 0) {
              $0.set(input.sessionID, input.model.limit.context);
            }
          }
        """

    let private projectionSessionIdFromMessages (output: obj) =
        if isNull output || isNull output?messages then
            None
        else
            let messages = unbox<obj array> output?messages

            messages
            |> Array.tryPick (fun msg ->
                if not (isNull msg) && not (isNull msg?info) && not (isNull msg?info?sessionID) then
                    Some(unbox<string> msg?info?sessionID)
                else
                    None)

    let private toolHooks
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
        ToolSurface.create
            toolModule
            sessionPort
            journal
            gitTreePort
            workspaceDirectory
            sessionParents
            sessionRoles
            verdictSessions
            modelConfig

    let initSpikePlugin (input: obj) : Task<obj> =
        task {
            let portOpt = OpenCodePort.create input
            let journal = PluginHost.createJournal input

            match PluginHost.createHost input portOpt journal with
            | Error err -> return raise (InvalidOperationException err)
            | Ok(eventPort, sessionPort, subscription, observeEvent) ->
                let companions = Dictionary<string, CompanionHost>()
                let companionGate = obj ()
                let sessionRoles = Dictionary<string, string>()
                let sessionParents = Dictionary<string, string>()
                let verdictSessions = HashSet<string>()
                let nudgeSent = HashSet<string>()

                PluginHost.restoreSessionRoles journal sessionRoles

                let gitTreePort =
                    match PluginHost.gitTreePortFromInput input with
                    | Some port -> Some port
                    | None -> PluginHost.workspaceDirectory input |> Option.map GitTree.create

                let sessionBudgets = Dictionary<string, int>()

                let eventRouter =
                    HostEventRouter(
                        sessionPort,
                        sessionParents,
                        sessionRoles,
                        verdictSessions,
                        nudgeSent,
                        ?journal = journal
                    )

                let transform inObj outObj =
                    let projectionSessionIdOpt =
                        if
                            not (isNull inObj)
                            && not (isNull inObj?sessionID)
                            && not (String.IsNullOrWhiteSpace(unbox<string> inObj?sessionID))
                        then
                            Some(unbox<string> inObj?sessionID)
                        else
                            projectionSessionIdFromMessages outObj

                    match observeEvent, projectionSessionIdOpt with
                    | Some observe, Some sid ->
                        let evt =
                            createObj
                                [ "type", box "plugin.transform"
                                  "properties", box (createObj [ "sessionID", box sid ]) ]

                        observe evt
                    | _ -> ()

                    match projectionSessionIdOpt with
                    | Some projectionSessionId ->
                        if not (isNull inObj) && isNull inObj?sessionID then
                            inObj?sessionID <- projectionSessionId

                        if
                            not (isNull inObj)
                            && isNull inObj?agent
                            && sessionRoles.ContainsKey projectionSessionId
                        then
                            inObj?agent <- sessionRoles.[projectionSessionId]
                    | None -> ()

                    CompanionTransform.handleCompanionTransform
                        companions
                        companionGate
                        sessionPort
                        journal
                        sessionBudgets
                        sessionRoles
                        inObj
                        outObj

                let hooks =
                    createObj
                        [ "projection", box Projection.projectMessages
                          "events", box eventPort
                          "sessions", box sessionPort
                          "journal", box journal
                          "hostEventsSubscription", box subscription
                          "chat.transform", box (uncurriedExecute (box transform))
                          "experimental.chat.messages.transform", box (uncurriedExecute (box transform))
                          "experimental.chat.system.transform", box (systemTransformHook sessionBudgets)
                          "config", box (fun (config: obj) -> ManagerConfig.configureManager config) ]

                observeEvent
                |> Option.iter (fun observe -> hooks?event <- box (fun raw -> eventRouter.Observe(raw, observe)))

                let client = if isNull input then null else input?client

                let modelConfig = ModelResolver.fromEnv ()

                if not (isNull client) then
                    try
                        let! toolModule = importToolModule ()

                        hooks?tool <-
                            toolHooks
                                toolModule
                                sessionPort
                                journal
                                gitTreePort
                                (PluginHost.workspaceDirectory input)
                                sessionParents
                                sessionRoles
                                verdictSessions
                                modelConfig
                    with ex ->
                        raise (InvalidOperationException(sprintf "Failed to load OpenCode tool module: %s" ex.Message))

                return box hooks
        }

    let createSpikePlugin (config: SpikePluginConfig) : obj =
        let input: obj =
            createObj
                [ "directory", box config.Directory
                  "port", box (config.Port |> Option.map box |> Option.defaultValue null) ]

        createObj
            [ "hooks",
              box (fun (inputObj: obj) ->
                  let mergedInput =
                      if isNull inputObj then
                          input
                      else
                          createObj
                              [ "directory",
                                box (
                                    if isNull inputObj?directory then
                                        config.Directory
                                    else
                                        inputObj?directory
                                )
                                "port",
                                box (
                                    if isNull inputObj?port then
                                        box config.Port
                                    else
                                        inputObj?port
                                )
                                "client", box inputObj?client
                                "events", box inputObj?events
                                "gitTreePort", box inputObj?gitTreePort ]

                  initSpikePlugin mergedInput) ]

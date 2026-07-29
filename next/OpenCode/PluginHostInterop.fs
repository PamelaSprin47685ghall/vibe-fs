namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open CompanionProjection

type SpikePluginConfig =
    { Directory: string
      Port: IOpenCodePort option }

module PluginHostInterop =

    [<Emit("import('@opencode-ai/plugin/tool')")>]
    let importToolModule () : Task<obj> = jsNative

    [<Emit("(args, context) => $0(args)(context)")>]
    let uncurriedExecute (fn: obj) : obj = jsNative

    let createSpikeHost (portOpt: IOpenCodePort option) = PluginHost.createSpikeHost portOpt

    let systemTransformHook
        (sessionBudgets: Dictionary<string, int>)
        (sessionOutputLimits: Dictionary<string, int>)
        (budgetStore: CompanionBudgetStore)
        : obj =
        let remember = fun sessionId budget -> budgetStore.Remember(sessionId, budget)

        emitJsExpr
            (sessionBudgets, sessionOutputLimits, remember)
            """
          (input, output) => {
            if (input && input.sessionID && input.model && input.model.limit) {
              const lim = input.model.limit;
              if (lim.context > 0) {
                $0.set(input.sessionID, lim.context);
                const override = Number(process.env.WANXIANGSHU_BLOGGER_CONTEXT_LIMIT || 0);
                const budget = override > 0 ? Math.min(lim.context, override) : lim.context;
                if (input.agent === 'blogger' || input.agent === 'fast-blogger' || input.agent === 'deep-blogger') {
                  if (input.parentID) $2(input.parentID, budget);
                } else if (input.agent && input.agent !== 'blogger' && input.agent !== 'fast-blogger' && input.agent !== 'deep-blogger' && input.agent !== 'title') {
                  $2(input.sessionID, budget);
                }
              }
              if (lim.output > 0) $1.set(input.sessionID, lim.output);
            }
          }
        """

    let projectionSessionIdFromMessages (output: obj) =
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

    let toolHooks
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (workspaceDirectory: string option)
        (scope: PluginRuntimeScope)
        (currentPhysicalUserMessage: string -> string option)
        (onRunStarted: (SessionId -> AgentRole -> string option -> unit) option)
        (backgroundBFor: (string -> string option) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        (eventPort: IEventObservationPort option)
        : ToolRegistration =
        ToolRegistry.create
            toolModule
            sessionPort
            journal
            gitTreePort
            workspaceDirectory
            scope.SessionParents
            currentPhysicalUserMessage
            scope.VerdictSessions
            scope.SessionDirectories
            onRunStarted
            backgroundBFor
            snapshot
            cancelSignals
            eventPort

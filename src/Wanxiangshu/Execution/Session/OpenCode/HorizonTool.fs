namespace Wanxiangshu.Execution.Session.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Domain
open Wanxiangshu.Host.Ports
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

/// OpenCode adapter for horizon(): binds `DelegationEnvironment` to one tool
/// call and puts `HorizonRoster` on the OpenCode wire.
module HorizonTool =
    /// Provider prose paths for the horizon surfaces. They live on the adapter
    /// side so an OpenCode adapter never reaches into a product namespace for
    /// a render path; `HorizonRoster` keeps the same vocabulary host-neutrally.
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/horizon/description"

        [<Literal>]
        let Returned = "tool/horizon/returned"

        [<Literal>]
        let StillAway = "tool/horizon/still-away"

        [<Literal>]
        let DidNotReturn = "tool/horizon/did-not-return"

        [<Literal>]
        let Voided = "tool/horizon/voided"

        [<Literal>]
        let RemainsOpen = "tool/horizon/remains-open"

        [<Literal>]
        let Someone = "tool/horizon/someone"

        [<Literal>]
        let TerminalLabel = "tool/horizon/terminal-label"

        [<Literal>]
        let EmptyRoster = "tool/horizon/empty-roster"

        [<Literal>]
        let UnavailableFromContext = "tool/horizon/unavailable-from-context"

        [<Literal>]
        let CannotBeSeen = "tool/horizon/cannot-be-seen"

        [<Literal>]
        let LatestWork = "tool/horizon/latest-work"

        [<Literal>]
        let NoWorkYet = "tool/horizon/no-work-yet"

        [<Literal>]
        let LatestWorkUnavailable = "tool/horizon/latest-work-unavailable"

        [<Literal>]
        let Calibration = "tool/horizon/calibration"

    type HorizonRuntimeContext =
        { RuntimeFor: HostToolContext -> Result<HostForkRuntime, string>
          LogicalOwnerFor: SessionId -> SessionId
          Journal: AgentJournal option
          EnsureWorkDevOpsBound: SessionId -> Task<unit> }

    let private environment (runtimeCtx: HorizonRuntimeContext) (context: HostToolContext) : DelegationEnvironment =
        { Caller =
            { Session =
                if String.IsNullOrWhiteSpace context.SessionId then
                    None
                else
                    Some(SessionId.create context.SessionId)
              ProviderRunId = context.ProviderRunId
              ToolCallId = context.ToolCallId }

          Journal = runtimeCtx.Journal

          ForkRuntime =
            fun () ->
                match runtimeCtx.RuntimeFor context with
                | Ok runtime -> Ok runtime
                | Error reason -> Error(HostError.Refused reason)

          LogicalOwnerFor = runtimeCtx.LogicalOwnerFor
          EnsureWorkDevOpsBound = runtimeCtx.EnsureWorkDevOpsBound
          WorkspaceDirectory = None
          ManagerCapabilityFactsFor = None
          CallerRole = None
          NewHandleId = None
          OpenManagerJob = (None: (ManagerJobId * string * string * string * int option -> HostResult<string>) option)
          ContinueManagerJob =
            (None: (ManagerJobId * string * ProviderRunIdentity * ToolCallId * int option -> HostResult<string>) option) }

    let private execute (runtimeCtx: HorizonRuntimeContext) (_args: HostToolArguments) context =
        task {
            let! lines = HorizonRoster.instructions (environment runtimeCtx context)
            return ToolResultText.tomlObjectWithInstructions lines []
        }

    let private agentRow (agent: HorizonRoster.RosterAgent) : obj =
        createObj
            [ "label" ==> agent.Label
              "status" ==> agent.Status
              "target" ==> agent.Target
              "workState" ==> agent.Work
              "record" ==> agent.LatestRecord
              "turnsUsed" ==> agent.TurnsUsed
              "turnsLeft" ==> agent.TurnsLeft
              "timeUsedMs" ==> agent.TimeUsedMs
              "timeLeftMs" ==> agent.TimeLeftMs ]

    let private receiptOf (roster: HorizonRoster.Roster option) : obj =
        match roster with
        | None -> createObj [ "ok" ==> false; "code" ==> "horizon-unavailable" ]
        | Some roster ->
            createObj
                [ "ok" ==> true
                  "agents" ==> (roster.Agents |> List.map agentRow |> List.toArray)
                  "ptys"
                  ==> (roster.Terminals
                       |> List.map (fun terminal -> createObj [ "terminal" ==> terminal ])
                       |> List.toArray) ]

    let rosterReceipt (language: string) (runtimeCtx: HorizonRuntimeContext) (context: HostToolContext) : Task<obj> =
        task {
            let! roster = HorizonRoster.roster (ProviderLanguage.parse language) (environment runtimeCtx context)
            return receiptOf roster
        }


    let spec (runtimeCtx: HorizonRuntimeContext) =
        { Name = "horizon"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = []
          Execute = execute runtimeCtx }

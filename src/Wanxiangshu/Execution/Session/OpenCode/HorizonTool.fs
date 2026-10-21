namespace Wanxiangshu.Execution.Session.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

/// OpenCode adapter for horizon(): binds `IDelegationCallerHost` to one tool
/// call and puts `HorizonRoster` on the OpenCode wire.
module HorizonTool =

    type HorizonRuntimeContext =
        { RuntimeFor: HostToolContext -> Result<HostForkRuntime, string>
          LogicalOwnerFor: SessionId -> SessionId
          Journal: AgentJournal option
          EnsureWorkDevOpsBound: SessionId -> Task<unit> }

    let private caller (runtimeCtx: HorizonRuntimeContext) (context: HostToolContext) =
        { new IDelegationCallerHost with
            member _.Caller =
                { Session =
                    if String.IsNullOrWhiteSpace context.SessionId then
                        None
                    else
                        Some(SessionId.create context.SessionId)
                  ProviderRunId = context.ProviderRunId
                  ToolCallId = context.ToolCallId }

            member _.Journal = runtimeCtx.Journal
            member _.LogicalOwnerFor sessionId = runtimeCtx.LogicalOwnerFor sessionId

            member _.EnsureWorkDevOpsBound parentSessionId =
                runtimeCtx.EnsureWorkDevOpsBound parentSessionId

            member _.ForkRuntime() = runtimeCtx.RuntimeFor context }

    let private execute (runtimeCtx: HorizonRuntimeContext) (_args: HostToolArguments) context =
        task {
            let! lines = HorizonRoster.instructions (caller runtimeCtx context)
            return ToolHostCodec.tomlObjectWithInstructions lines []
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
            let! roster = HorizonRoster.roster (ProviderLanguage.parse language) (caller runtimeCtx context)
            return receiptOf roster
        }

    let admission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ role -> HorizonRoster.permits role)

    let spec (runtimeCtx: HorizonRuntimeContext) =
        { Name = "horizon"
          Description =
            ProviderProse.render
                (ProviderLanguageBinding.readGlobalPreference ())
                HorizonRoster.Path.Description
                Map.empty
          Arguments = []
          Admission = admission
          Execute = execute runtimeCtx }

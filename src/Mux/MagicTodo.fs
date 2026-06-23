module VibeFs.Mux.MagicTodo

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicTodo
open VibeFs.Kernel.Messaging
open VibeFs.Opencode.MagicTodo
open VibeFs.Shell.MagicSessionStore

type MagicSession() =
    member _.Host = opencode

    member _.CaptureReport(callID: string, report: string) : unit =
        captureReport opencode callID report

    member _.ReplayBacklog(messages: Message<obj> list) : BacklogEntry list =
        let reportOf (fp: FlatPart<obj>) : string =
            match fp.part with
            | ToolPart(_, callID, Some state, _) ->
                let explicit = backlogReportFromTodoInput opencode state.input
                if explicit <> "" then explicit
                else tryGetReport opencode callID |> Option.defaultValue ""
            | _ -> ""

        replayBacklogWith opencode reportOf messages

    member this.GetOrRebuildBacklog(sessionID: string, messages: Message<obj> list) : BacklogEntry list =
        if messages.Length > 0 then
            let backlog = this.ReplayBacklog messages
            storeBacklog opencode sessionID backlog
            backlog
        else
            tryGetBacklog opencode sessionID |> Option.defaultValue []

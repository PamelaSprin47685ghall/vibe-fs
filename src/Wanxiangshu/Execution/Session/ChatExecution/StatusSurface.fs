namespace Wanxiangshu.Execution.Session.ChatExecution

open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module StatusSurface =

    let private dispositionLabel =
        function
        | ChatExecutionTerminalDisposition.Completed -> "Completed"
        | ChatExecutionTerminalDisposition.Cancelled -> "Cancelled"
        | ChatExecutionTerminalDisposition.Rejected -> "Rejected"
        | ChatExecutionTerminalDisposition.Failed -> "Failed"

    let internal projectState (state: ChatExecutionState option) : obj =
        match state with
        | None ->
            box
                {| accepted = false
                   providerStarted = false
                   terminal = false
                   disposition = null |}
        | Some execution ->
            let providerStarted, terminal, disposition =
                match execution.Lifecycle with
                | ChatExecutionLifecycle.Accepted -> false, false, null
                | ChatExecutionLifecycle.ProviderStarted -> true, false, null
                | ChatExecutionLifecycle.Terminal value ->
                    execution.ProviderStarted.IsSome, true, box (dispositionLabel value)

            box
                {| accepted = true
                   providerStarted = providerStarted
                   terminal = terminal
                   disposition = disposition |}

    let query (journal: JournalHandle) (sessionId: string) (physicalUserMessageId: string) : obj =
        let key =
            { SessionId = SessionId.create sessionId
              PhysicalUserMessageId = PhysicalUserMessageId.create physicalUserMessageId }

        AgentJournal.snapshot journal.Journal
        |> fun projection -> projection.AgentProjections.ChatExecutions
        |> ChatExecutionProjection.byKey key
        |> projectState

    let queryFacts (serializedFacts: string array) (sessionId: string) (physicalUserMessageId: string) : obj =
        let folded = Surface.fold serializedFacts

        if not (unbox<bool> folded?ok) then
            box
                {| ok = false
                   status = null
                   error = unbox<string> folded?error |}
        else
            let states: obj array = unbox folded?value

            let state =
                states
                |> Array.tryFind (fun value ->
                    unbox<string> value?sessionId = sessionId
                    && unbox<string> value?physicalUserMessageId = physicalUserMessageId)

            let status =
                match state with
                | None -> projectState None
                | Some value ->
                    let phase: string = unbox value?phase

                    box
                        {| accepted = true
                           providerStarted = phase = "ProviderStarted" || not (isNull value?identity?providerRun)
                           terminal = phase = "Terminal"
                           disposition = value?disposition |}

            box
                {| ok = true
                   status = status
                   error = "" |}

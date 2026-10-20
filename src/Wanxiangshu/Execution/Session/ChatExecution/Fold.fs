namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module ChatExecutionFactFold =

    let private invalidFactRejection reason : FoldRejection =
        { Fact = "ChatExecution"
          Reason = "InvalidFact: " + reason }

    let private invalidFact reason : Result<'value, FoldRejection> = Error(invalidFactRejection reason)

    let private replace (execution: ChatExecutionState) projection =
        { projection with
            ByKey = Map.add execution.key execution projection.ByKey }

    let private validateAcceptedKey (key: ChatExecutionKey) (evidence: AcceptedChatExecutionEvidence) =
        if evidence.SessionId <> key.SessionId then
            invalidFact "Accepted evidence session does not match its execution key"
        elif evidence.PhysicalUserMessageId <> key.PhysicalUserMessageId then
            invalidFact "Accepted evidence physical user message does not match its execution key"
        else
            Ok()

    let private sameAccepted (current: ChatExecutionState option) (evidence: AcceptedChatExecutionEvidence) =
        current
        |> Option.exists (fun execution -> execution.acceptedEvidence = evidence)

    let private sameStarted (current: ChatExecutionState option) (evidence: ProviderStartedEvidence) =
        current
        |> Option.bind (fun execution -> execution.startedEvidence)
        |> Option.exists (fun established -> established = evidence)

    /// CUT-12: which transition is legal is decided by wanxiangli/chat-execution;
    /// this fold only builds the next typed state from the evidence it carries.
    let private project key projection (current: ChatExecutionState option) verdict build =
        match verdict with
        | ChatExecutionLawFold.Keep -> Ok projection
        | ChatExecutionLawFold.Reject reason ->
            Error
                { Fact = "ChatExecution"
                  Reason = reason }
        | next ->
            match build current next with
            | Some execution -> Ok(replace execution projection)
            | None -> invalidOp (sprintf "ChatExecution phase law set an impossible phase for %A" key)

    let private validateTerminalEvidence =
        function
        | ChatExecutionTerminalEvidence.PreProvider accepted -> AcceptedChatExecutionEvidence.validate accepted
        | ChatExecutionTerminalEvidence.AfterProviderStart started -> ProviderStartedEvidence.validate started

    let applyAccepted
        (key: ChatExecutionKey)
        (evidence: AcceptedChatExecutionEvidence)
        (projection: ChatExecutionProjectionState)
        : Result<ChatExecutionProjectionState, FoldRejection> =
        AcceptedChatExecutionEvidence.validate evidence
        |> Result.mapError invalidFactRejection
        |> Result.bind (fun () -> validateAcceptedKey key evidence)
        |> Result.bind (fun () ->
            let current = ChatExecutionProjection.byKey key projection

            project
                key
                projection
                current
                (ChatExecutionPhaseLaw.foldAccepted current (sameAccepted current evidence))
                (fun _ next ->
                    match next with
                    | ChatExecutionLawFold.SetAccepted -> Some(ChatExecutionState.Accepted evidence)
                    | _ -> None))

    let applyProviderStarted
        (key: ChatExecutionKey)
        (evidence: ProviderStartedEvidence)
        (projection: ChatExecutionProjectionState)
        : Result<ChatExecutionProjectionState, FoldRejection> =
        ProviderStartedEvidence.validate evidence
        |> Result.mapError invalidFactRejection
        |> Result.bind (fun () ->
            let current = ChatExecutionProjection.byKey key projection

            let verdict =
                ChatExecutionPhaseLaw.foldProviderStarted
                    current
                    (sameAccepted current evidence.Accepted)
                    (sameStarted current evidence)

            project key projection current verdict (fun _ next ->
                match next with
                | ChatExecutionLawFold.SetStarted -> Some(ChatExecutionState.Started evidence)
                | _ -> None))

    let applyTerminal
        (key: ChatExecutionKey)
        (evidence: ChatExecutionTerminalEvidence)
        (disposition: ChatExecutionTerminalDisposition)
        (projection: ChatExecutionProjectionState)
        : Result<ChatExecutionProjectionState, FoldRejection> =
        validateTerminalEvidence evidence
        |> Result.mapError invalidFactRejection
        |> Result.bind (fun () ->
            let current = ChatExecutionProjection.byKey key projection

            let verdict =
                match evidence with
                | ChatExecutionTerminalEvidence.PreProvider accepted ->
                    ChatExecutionPhaseLaw.foldTerminal current false (sameAccepted current accepted) false disposition
                | ChatExecutionTerminalEvidence.AfterProviderStart started ->
                    ChatExecutionPhaseLaw.foldTerminal
                        current
                        true
                        (sameAccepted current started.Accepted)
                        (sameStarted current started)
                        disposition

            project key projection current verdict (fun current next ->
                match next, current with
                | ChatExecutionLawFold.SetEndedBeforeStart outcome, Some execution ->
                    Some(ChatExecutionState.EndedBeforeStart(execution.acceptedEvidence, outcome))
                | ChatExecutionLawFold.SetEndedAfterStart disposition, Some(ChatExecutionState.Started established) ->
                    Some(ChatExecutionState.EndedAfterStart(established, disposition))
                | _ -> None))

    let fold
        (projection: ChatExecutionProjectionState)
        (fact: ChatExecutionFactCases)
        : Result<ChatExecutionProjectionState, FoldRejection> =
        let schemaVersion =
            match fact with
            | Accepted payload -> payload.SchemaVersion
            | ProviderStarted payload -> payload.SchemaVersion
            | Terminal payload -> payload.SchemaVersion

        match ChatExecutionPhaseLaw.schemaRejection schemaVersion, fact with
        | Some reason, _ -> FoldRejection.reject "ChatExecution" reason
        | None, Accepted payload -> applyAccepted payload.Key payload.Evidence projection
        | None, ProviderStarted payload -> applyProviderStarted payload.Key payload.Evidence projection
        | None, Terminal payload -> applyTerminal payload.Key payload.Evidence payload.Disposition projection

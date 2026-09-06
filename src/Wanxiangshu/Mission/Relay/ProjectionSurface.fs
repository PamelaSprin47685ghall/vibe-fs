namespace Wanxiangshu.Mission.Relay

open System
open Fable.Core.JsInterop

module ProjectionSurface =
    let private property (value: obj) (name: string) : obj =
        if isNull value then null else value?(name)

    let private stringProperty value name =
        let candidate = property value name
        if isNull candidate then "" else unbox<string> candidate

    let private arrayProperty value name =
        let candidate = property value name

        let isArray: bool =
            if isNull candidate then
                false
            else
                emitJsExpr candidate "Array.isArray($0)"

        if isArray then unbox<obj array> candidate else [||]

    let private messageId (message: obj) =
        let info = property message "info"
        let fromInfo = if isNull info then "" else stringProperty info "id"

        if not (String.IsNullOrEmpty fromInfo) then
            fromInfo
        else
            stringProperty message "id"

    let private messageRun (message: obj) = stringProperty message "run"

    let private messageRole (message: obj) =
        let info = property message "info"
        let fromInfo = if isNull info then "" else stringProperty info "role"
        let direct = stringProperty message "role"

        let role =
            if not (String.IsNullOrEmpty fromInfo) then
                fromInfo
            else
                direct

        role.ToLowerInvariant()

    /// The wake turn is the first non-authority role=user message after the
    /// retirement tool call, mirroring production.
    let private isWakeCandidate (message: obj) = messageRole message = "user"

    let private partCallIds (part: obj) =
        [ stringProperty part "callID"
          stringProperty part "callId"
          stringProperty part "toolCallId"
          stringProperty part "id" ]
        |> List.filter (fun text -> not (String.IsNullOrEmpty text))

    let private messageContainsToolCall (message: obj) (toolCallId: string) =
        if String.IsNullOrEmpty toolCallId then
            false
        else
            let parts = arrayProperty message "parts"

            if parts.Length > 0 then
                parts |> Array.exists (fun part -> partCallIds part |> List.contains toolCallId)
            else
                messageId message = toolCallId || messageRun message = toolCallId

    /// Audit history is never rewritten: the provider view is a projection of
    /// the same physical messages. After a Continue retirement with the next
    /// iteration active, the provider keeps exactly the typed authority
    /// messages plus the current-iteration tail. Every prior-iteration message,
    /// every retired-run part (including late arrivals), the retirement tool
    /// call itself, and the first non-authority user continuation used only to
    /// wake the loop are excluded. The retired epoch ends at the retirement
    /// tool call; both exact cut positions are required, otherwise only typed
    /// authority messages pass.
    let applyCut
        (messages: obj array)
        (providerRunId: string)
        (toolCallId: string)
        (retiredRunIds: string array)
        (authorityMessageIds: string array)
        =
        let authority = Set.ofArray authorityMessageIds

        let retired =
            let baseSet = Set.ofArray retiredRunIds

            if String.IsNullOrEmpty providerRunId then
                baseSet
            else
                Set.add providerRunId baseSet

        let cutIndex =
            if String.IsNullOrEmpty providerRunId then
                None
            else
                messages
                |> Array.tryFindIndex (fun message ->
                    let id = messageId message
                    let run = messageRun message

                    (not (String.IsNullOrEmpty id) && id = providerRunId)
                    || (not (String.IsNullOrEmpty run) && run = providerRunId))

        let toolIndex =
            messages
            |> Array.tryFindIndex (fun message -> messageContainsToolCall message toolCallId)

        let provider =
            match cutIndex, toolIndex with
            | Some _, Some tool ->
                let wakeIndex =
                    messages
                    |> Array.mapi (fun index message -> index, message)
                    |> Array.tryFind (fun (index, message) ->
                        index > tool
                        && isWakeCandidate message
                        && not (Set.contains (messageId message) authority))
                    |> Option.map fst

                let inWakeTail index =
                    match wakeIndex with
                    | None -> index > tool
                    | Some wake -> index > tool && index <= wake

                messages
                |> Array.mapi (fun index message -> index, message)
                |> Array.choose (fun (index, message) ->
                    let id = messageId message
                    let run = messageRun message
                    let keepAuthority = Set.contains id authority

                    let retiredEpoch = index <= tool && not keepAuthority

                    let retiredRun =
                        (not (String.IsNullOrEmpty id) && Set.contains id retired)
                        || (not (String.IsNullOrEmpty run) && Set.contains run retired)

                    let retirementTool = messageContainsToolCall message toolCallId
                    let wakeTail = inWakeTail index

                    if keepAuthority then
                        Some message
                    elif retiredEpoch || retiredRun || retirementTool || (wakeTail && not keepAuthority) then
                        None
                    else
                        Some message)
            | _ ->
                // Fail closed: without both exact cut positions only typed
                // authority messages may reach the provider.
                messages
                |> Array.filter (fun message -> Set.contains (messageId message) authority)

        box
            {| audit = messages
               provider = provider |}

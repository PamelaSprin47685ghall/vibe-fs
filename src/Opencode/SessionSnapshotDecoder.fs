module VibeFs.Opencode.SessionSnapshotDecoder

open VibeFs.Kernel
open VibeFs.Kernel.Nudge
open VibeFs.Opencode.NudgePolicy

let decodeTodos (todosData: obj) : string list =
    if Dyn.isArray todosData then
        (todosData :?> obj array)
        |> Array.choose (fun todo ->
            let status = Dyn.str todo "status"
            match todoStatusOfString status with
            | Some s when isTerminal s -> None
            | _ -> Some status)
        |> Array.toList
    else []

let decodeLastAssistant (messagesData: obj) : string * string option * int option =
    if Dyn.isArray messagesData then
        let messagesArr = messagesData :?> obj array
        let messageCount = Some messagesArr.Length
        let lastAssistant =
            messagesArr
            |> Array.tryFindBack (fun msg -> isCompletedAssistantMessage (Dyn.get msg "info"))
        match lastAssistant with
        | Some msg ->
            let info = Dyn.get msg "info"
            let agentVal = Dyn.get info "agent"
            let agent = if Dyn.isNullish agentVal then None else Some (string agentVal)
            let text = getPartsText (Dyn.get msg "parts")
            text, agent, messageCount
        | None -> "", None, messageCount
    else "", None, None

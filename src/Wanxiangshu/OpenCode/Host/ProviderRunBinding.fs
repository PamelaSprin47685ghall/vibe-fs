namespace Wanxiangshu.OpenCode

/// Physical Host binding for the assistant run created for one user message.
/// This is transport identity only; it carries no Review semantics.
module ProviderRunBinding =

    [<RequireQualifiedAccess>]
    type Rejection =
        | NoBindableRun
        | AmbiguousRun of count: int
        | NotLatestRun

    let private confirmLatestRun single latest =
        if latest.Id = single.Id then
            Ok single
        else
            Error Rejection.NotLatestRun

    let private decideSingleRun single (messages: SessionMessage list) =
        let assistants = messages |> List.filter (fun message -> message.Role = "assistant")

        match assistants with
        | [] -> Error Rejection.NoBindableRun
        | _ -> confirmLatestRun single (assistants |> List.maxBy (fun message -> message.Id))

    let bindableRun (physicalUserMessage: string) (messages: SessionMessage list) =
        let candidates =
            messages
            |> List.filter (fun message ->
                message.Role = "assistant"
                && not message.Completed
                && not message.IsCompaction
                && message.ParentId = Some physicalUserMessage)

        match candidates with
        | [] -> Error Rejection.NoBindableRun
        | [ single ] -> decideSingleRun single messages
        | many -> Error(Rejection.AmbiguousRun(List.length many))

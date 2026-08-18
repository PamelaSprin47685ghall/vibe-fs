namespace Wanxiangshu.OpenCode

/// Physical Host binding for the assistant run created for one user message.
/// This is transport identity only; it carries no Review semantics.
module ProviderRunBinding =

    [<RequireQualifiedAccess>]
    type Rejection =
        | NoBindableRun
        | AmbiguousRun of count: int
        | NotLatestRun

    [<RequireQualifiedAccess>]
    type Observation =
        | Bound of SessionMessage
        | ProjectionNotVisibleYet
        | Rejected of Rejection

    /// Maximum public-snapshot reads used by the physical Host seam when the
    /// only evidence missing is the not-yet-projected assistant message.
    /// First read is immediate; subsequent reads are separated by the delay
    /// below. Identity rejections are never retried.
    let projectionCatchupMaxReads = 6

    let projectionCatchupDelayMilliseconds = 10

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

    /// Split a physical snapshot visibility gap from a genuine identity
    /// rejection without weakening `bindableRun` itself. The Host can publish
    /// the assistant message before its public session projection is readable;
    /// only the zero-candidate case is eligible for a bounded reread.
    let observeBindableRun (physicalUserMessage: string) (messages: SessionMessage list) =
        let matchingCompaction =
            messages
            |> List.exists (fun message ->
                message.Role = "assistant"
                && not message.Completed
                && message.IsCompaction
                && message.ParentId = Some physicalUserMessage)

        match matchingCompaction, bindableRun physicalUserMessage messages with
        | true, _ -> Observation.Rejected Rejection.NoBindableRun
        | false, Ok run -> Observation.Bound run
        | false, Error Rejection.NoBindableRun -> Observation.ProjectionNotVisibleYet
        | false, Error rejection -> Observation.Rejected rejection

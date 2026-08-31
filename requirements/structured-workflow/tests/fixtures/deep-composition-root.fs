module DeepCompositionRoot

let private decideFinality evidence =
    if evidence then "bless" else "retry"

let create ports =
    ports |> List.map decideFinality

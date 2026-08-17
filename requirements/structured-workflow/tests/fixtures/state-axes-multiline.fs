module Sample

[<RequireQualifiedAccess>]
type Availability =
    | Ready
    | Taken

type RuntimeSnapshot =
    {
        Availability: Availability
        CurrentRunId: string option
        CompletionCellSettled: bool
    }

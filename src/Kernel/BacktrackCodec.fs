module VibeFs.Kernel.BacktrackCodec

let idPrefix = "#id_: "

let idComment =
    "  // Keep history clean and high-density — NOW consider calling backtrack tool to substitute redundant outputs with a concise summary"

let encodeId (id: int) (output: string) : string =
    $"%s{idPrefix}%d{id}%s{idComment}\n%s{output}"

let tryParseId (output: string) : int option =
    if isNull output || not (output.StartsWith idPrefix) then None
    else
        let rest = output.Substring idPrefix.Length
        let mutable endIdx = 0
        while endIdx < rest.Length && System.Char.IsDigit rest.[endIdx] do
            endIdx <- endIdx + 1
        if endIdx = 0 then None
        else
            match System.Int32.TryParse (rest.Substring(0, endIdx)) with
            | true, n -> Some n
            | false, _ -> None

let stripIdPrefix (output: string) : string =
    if isNull output || not (output.StartsWith idPrefix) then output
    else
        let newlineIdx = output.IndexOf '\n'
        if newlineIdx < 0 then output else output.Substring(newlineIdx + 1)

let maxIdFromOutputs (outputs: string seq) : int =
    outputs |> Seq.choose tryParseId |> Seq.fold max 0

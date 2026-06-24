module VibeFs.Methodology.Args

open System
open VibeFs.Methodology.SchemaCommon
open VibeFs.Shell.Dyn

let parse (schema: MethodologySchema) (args: obj) : Result<Map<string, string> * Map<string, string list>, string> =
    if isNullish args then Error "missing tool arguments"
    else
        let values = ResizeArray()
        let arrays = ResizeArray()
        let errors = ResizeArray()

        for f in schema.fields do
            let raw = get args f.name
            match f.kind with
            | FieldKind.String ->
                let text =
                    if isNullish raw then ""
                    else string raw |> fun s -> s.Trim()
                if f.required && text = "" then errors.Add($"{f.name} is required")
                elif Set.contains f.name notebookMinWordFieldNames && text <> "" then
                    match validateNotebookMinWords f.name text with
                    | Error e -> errors.Add e
                    | Ok _ -> values.Add(f.name, text)
                elif Set.contains f.name notebookMinWordFieldNames && f.required then
                    errors.Add($"{f.name} is required")
                elif text <> "" || f.required then values.Add(f.name, text)
            | FieldKind.StringArray ->
                let items =
                    if isNullish raw || not (isArray raw) then []
                    else
                        unbox<obj array> raw
                        |> Array.map (fun x -> string x |> fun s -> s.Trim())
                        |> Array.filter ((<>) "")
                        |> Array.toList
                if f.required && items.Length < f.minArrayItems then
                    errors.Add($"{f.name} requires at least {f.minArrayItems} non-empty items")
                elif not f.required && items.IsEmpty then ()
                else arrays.Add(f.name, items)

        if errors.Count > 0 then Error (String.concat "; " (errors |> Seq.toList))
        else
            Ok(
                values |> Seq.map (fun (k, v) -> k, v) |> Map.ofSeq,
                arrays |> Seq.map (fun (k, v) -> k, v) |> Map.ofSeq)
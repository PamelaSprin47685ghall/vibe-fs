namespace Wanxiangshu.Domain

open System

/// AGENT-022: the loadable artifact contract for StudentCompile output.
/// OpenCode discovers only .agent/skills/<name>/SKILL.md and requires a
/// frontmatter name. Wanxiangshu additionally requires a useful description
/// and a non-empty body before the Student may finish.
[<RequireQualifiedAccess>]
module StudentSkill =

    let private unquote (value: string) =
        let trimmed = value.Trim()

        if
            trimmed.Length >= 2
            && ((trimmed.[0] = '"' && trimmed.[trimmed.Length - 1] = '"')
                || (trimmed.[0] = '\'' && trimmed.[trimmed.Length - 1] = '\''))
        then
            trimmed.Substring(1, trimmed.Length - 2).Trim()
        else
            trimmed

    let private frontmatterValue (key: string) (lines: string array) : Result<string, string> =
        let prefix = key + ":"

        let values =
            lines
            |> Array.choose (fun line ->
                let trimmed = line.Trim()

                if trimmed.StartsWith(prefix, StringComparison.Ordinal) then
                    Some(unquote (trimmed.Substring(prefix.Length)))
                else
                    None)

        match values with
        | [| value |] when not (String.IsNullOrWhiteSpace value) -> Ok value
        | [||] -> Error(sprintf "SKILL.md frontmatter requires non-empty '%s'" key)
        | _ -> Error(sprintf "SKILL.md frontmatter defines '%s' more than once" key)

    /// The write/edit gate accepts exactly one loadable document shape. It
    /// deliberately rejects absolute paths, traversal, flat markdown files,
    /// nested alternate roots, and supporting-file writes.
    let targetName (path: string) : Result<string, string> =
        if String.IsNullOrWhiteSpace path then
            Error "StudentCompile requires .agent/skills/<skill-name>/SKILL.md"
        else
            let parts = path.Split([| '/' |], StringSplitOptions.None)

            if
                parts.Length <> 4
                || parts.[0] <> ".agent"
                || parts.[1] <> "skills"
                || String.IsNullOrWhiteSpace parts.[2]
                || parts.[2].StartsWith(".", StringComparison.Ordinal)
                || parts.[3] <> "SKILL.md"
            then
                Error "StudentCompile write/edit target must be exactly .agent/skills/<skill-name>/SKILL.md"
            else
                Ok parts.[2]

    let validateDocument (expectedName: string) (content: string) : Result<unit, string> =
        let normalized =
            if isNull content then
                ""
            else
                content.Replace("\r\n", "\n").Replace("\r", "\n")

        let lines = normalized.Split([| '\n' |], StringSplitOptions.None)

        if lines.Length < 4 || lines.[0].Trim() <> "---" then
            Error "SKILL.md must start with YAML frontmatter delimited by ---"
        else
            let closing =
                lines
                |> Array.indexed
                |> Array.tryPick (fun (index, line) ->
                    if index > 0 && line.Trim() = "---" then
                        Some index
                    else
                        None)

            match closing with
            | None -> Error "SKILL.md YAML frontmatter has no closing --- delimiter"
            | Some closingIndex ->
                let frontmatter = lines.[1 .. closingIndex - 1]

                match frontmatterValue "name" frontmatter, frontmatterValue "description" frontmatter with
                | Error error, _
                | _, Error error -> Error error
                | Ok actualName, Ok _ when actualName <> expectedName ->
                    Error(sprintf "SKILL.md frontmatter name '%s' must match directory '%s'" actualName expectedName)
                | Ok _, Ok _ ->
                    let body = String.Join("\n", lines.[closingIndex + 1 ..]).Trim()

                    if String.IsNullOrWhiteSpace body then
                        Error "SKILL.md requires a non-empty Markdown body after frontmatter"
                    else
                        Ok()

namespace Wanxiangshu.Infrastructure.Resources

open System
open System.Text.RegularExpressions
open Wanxiangshu.Domain

/// Loads the Enforcer Rulebook from `resources/enforcer/<tip>/enforcer.md` + `main.md`.
/// Directory basename = TipName = provider enum = durable RuleId.
/// Missing / empty / invalid → throw (fail-fast). Module import does not read files.
/// `catalog.json` is not read and must not be required at runtime.
module EnforcerCatalogResource =

    let private kebabNamePattern =
        Regex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled)

    let private isKebab (name: string) = kebabNamePattern.IsMatch name

    /// Extract markdown section body under `## <heading>` (case-insensitive heading match).
    let private trySection (markdown: string) (heading: string) : string option =
        if isNull markdown then
            None
        else
            let lines = markdown.Replace("\r\n", "\n").Split('\n')
            let target = heading.Trim().ToLowerInvariant()
            // DSL-MUTABLE: algorithm-scratch — section scan state
            let mutable collecting = false
            let mutable acc: string list = []

            for line in lines do
                let trimmed = line.TrimStart()

                if trimmed.StartsWith("## ") then
                    let title = trimmed.Substring(3).Trim().ToLowerInvariant()

                    if collecting then
                        collecting <- false
                    elif title = target then
                        collecting <- true
                        acc <- []
                elif collecting then
                    acc <- line :: acc

            if List.isEmpty acc then
                None
            else
                let body = acc |> List.rev |> String.concat "\n" |> (fun s -> s.Trim())

                if body.Length = 0 then None else Some body

    let private tryFamily (markdown: string) : string option =
        if isNull markdown then
            None
        else
            let m = Regex.Match(markdown, @"Family:\s*([A-Za-z0-9_-]+)")

            if m.Success then Some(m.Groups.[1].Value.Trim()) else None

    let private firstNonEmptyParagraph (markdown: string) : string =
        markdown.Replace("\r\n", "\n").Split([| "\n\n" |], StringSplitOptions.None)
        |> Array.map (fun p -> p.Trim())
        |> Array.tryFind (fun p ->
            p.Length > 0
            && not (p.StartsWith("#"))
            && not (p.StartsWith("Tip already selected")))
        |> Option.defaultValue (markdown.Trim())

    /// Effective Blogger system prompt: base blogger-system.md + full enforcer.md set.
    /// Deterministic projection only — never written back to the repository.
    let composeBloggerSystemPrompt (basePrompt: string) (rules: EnforcerRule list) : string =
        let ordered = rules |> List.sortBy (fun r -> r.CatalogOrdinal)
        let parts = ResizeArray<string>()

        let baseText = if isNull basePrompt then "" else basePrompt.TrimEnd()

        if baseText.Length > 0 then
            parts.Add(baseText)

        parts.Add("# Enforcer Rulebook")

        for rule in ordered do
            parts.Add(sprintf "## %s" rule.Name)
            parts.Add(rule.EnforcerText.Trim())

        String.concat "\n\n" (parts.ToArray()) + "\n"

    let load () : EnforcerRule list =
        let rootRel = "enforcer"
        let names = PackageResources.listChildDirectoryNames rootRel

        if List.isEmpty names then
            raise (
                InvalidOperationException(
                    sprintf "enforcer rulebook empty: no rule directories under resources/%s" rootRel
                )
            )

        let rules =
            names
            |> List.mapi (fun index name ->
                if not (isKebab name) then
                    raise (
                        InvalidOperationException(
                            sprintf "enforcer rule directory name must be lower-kebab-case: %s" name
                        )
                    )

                let enforcerRel = sprintf "%s/%s/enforcer.md" rootRel name
                let mainRel = sprintf "%s/%s/main.md" rootRel name

                if not (PackageResources.exists enforcerRel) then
                    raise (InvalidOperationException(sprintf "package resource missing: resources/%s" enforcerRel))

                if not (PackageResources.exists mainRel) then
                    raise (InvalidOperationException(sprintf "package resource missing: resources/%s" mainRel))

                let enforcerText = PackageResources.readText(enforcerRel).Trim()
                let mainText = PackageResources.readText(mainRel).Trim()

                if enforcerText.Length = 0 then
                    raise (InvalidOperationException(sprintf "enforcer.md empty for rule %s" name))

                if mainText.Length = 0 then
                    raise (InvalidOperationException(sprintf "main.md empty for rule %s" name))

                let scoreWhen =
                    trySection enforcerText "ScoreWhen" |> Option.defaultValue enforcerText

                let nudge =
                    trySection enforcerText "Nudge"
                    |> Option.orElse (trySection mainText "What to do")
                    |> Option.defaultValue (firstNonEmptyParagraph mainText)

                let family = tryFamily enforcerText |> Option.defaultValue "rulebook"
                let ordinal = index + 1

                { Name = name
                  EnforcerText = enforcerText
                  MainText = mainText
                  RuleId = name
                  FieldName = name
                  Family = family
                  ScoreWhen = scoreWhen.Trim()
                  Nudge = nudge.Trim()
                  CatalogOrdinal = ordinal })

        match EnforcerCatalog.validate 1 rules with
        | Error err ->
            raise (InvalidOperationException(sprintf "enforcer rulebook invalid under resources/%s: %s" rootRel err))
        | Ok validated -> validated

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

    /// Effective Blogger system prompt: base blogger-system.md + full enforcer.md set.
    /// Deterministic projection only — never written back to the repository.
    let composeBloggerSystemPrompt (basePrompt: string) (rules: EnforcerRule list) : string =
        let ordered = rules |> List.sortBy (fun r -> r.LexicalOrder)
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

                let order = index + 1

                { Name = name
                  EnforcerText = enforcerText
                  MainText = mainText
                  RuleId = name
                  FieldName = name
                  LexicalOrder = order })

        match EnforcerCatalog.validate 1 rules with
        | Error err ->
            raise (InvalidOperationException(sprintf "enforcer rulebook invalid under resources/%s: %s" rootRel err))
        | Ok validated -> validated

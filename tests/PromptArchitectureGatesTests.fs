module Wanxiangshu.Tests.PromptArchitectureGatesTests

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Wanxiangshu.Tests.ArchitectureGatesFs
open Wanxiangshu.Tests.Assert

let private violations = ResizeArray<string>()

let private failIf (cond: bool) (msg: string) =
    if cond then
        violations.Add msg

let checkSmolTomlImports (srcRoot: string) =
    let smolTomlRe =
        Regex(@"import\s+.*from\s+['""]smol-toml['""]|Import\([^)]*['""]smol-toml['""]\)", RegexOptions.Multiline)

    for path in collectFsFiles srcRoot do
        let norm = path.Replace("\\", "/")
        let content = readFileSync path "utf8"

        if smolTomlRe.IsMatch content then
            if not (norm.EndsWith("src/Runtime/Serialization/Toml.fs")) then
                failIf true (sprintf "smol-toml imported outside of Runtime/Serialization/Toml.fs: %s" path)

let checkNoSmolTomlParseInProduction (srcRoot: string) =
    let parseRe =
        Regex(@"import\s+.*parse.*from\s+['""]smol-toml['""]|parseNative|smol-toml\.parse", RegexOptions.Multiline)

    for path in collectFsFiles srcRoot do
        let content = readFileSync path "utf8"
        failIf (parseRe.IsMatch content) (sprintf "production code must not import or call smol-toml.parse: %s" path)

let checkYamlImports (srcRoot: string) =
    let yamlRe =
        Regex(@"import\s+.*from\s+['""]yaml['""]|Import\([^)]*['""]yaml['""]\)", RegexOptions.Multiline)

    let allowed =
        [ "src/Runtime/Fallback/FallbackConfigCodec.fs"
          "src/Runtime/Wanxiangzhen/ConfigReader.fs" ]

    for path in collectFsFiles srcRoot do
        let norm = path.Replace("\\", "/")
        let content = readFileSync path "utf8"

        if yamlRe.IsMatch content then
            let isAllowed = allowed |> List.exists (fun a -> norm.EndsWith a)

            if not isAllowed then
                failIf
                    true
                    (sprintf "yaml imported outside whitelist (FallbackConfigCodec.fs/ConfigReader.fs): %s" path)

let checkDeletedProductionFiles (srcRoot: string) =
    let forbiddenFiles =
        [ "PromptHeader.fs"
          "PromptFrontMatter.fs"
          "Yaml.fs"
          "ToolOutputInfoParse.fs" ]

    for path in collectFsFiles srcRoot do
        let norm = path.Replace("\\", "/")
        let name = norm.Split('/') |> Array.last

        if List.contains name forbiddenFiles then
            // Yaml.fs only forbidden if under Runtime/Workspace/Yaml.fs
            if name = "Yaml.fs" && norm.Contains("Runtime/Workspace/Yaml.fs") then
                failIf true (sprintf "Legacy file must be physically deleted: %s" path)
            elif name <> "Yaml.fs" then
                failIf true (sprintf "Legacy file must be physically deleted: %s" path)

let checkNoLegacyPromptSymbols (srcRoot: string) =
    let forbiddenSymbols =
        [ "yamlField"
          "yamlSeqField"
          "yamlStringSeqField"
          "frontMatter"
          "frontMatterPrompt"
          "promptHeader"
          "promptHeaderPrompt"
          "CapsYamlItem"
          "hasDoubleCheckAnchor"
          "isNudgePromptText"
          "isNudgePrompt"
          "reviewInstructionsProse"
          "reviewSubmissionVerdictBody"
          "todoNudgePromptProse"
          "loopNudgePromptProse"
          "parallelToolPromptProse"
          "readOnlyRulesFor"
          "readOnlyWorkspaceConstraint"
          "wipAcknowledgmentAnchor"
          "buildSessionPromptBody"
          "reportFromSummary"
          "capExecutorSummaryOutput" ]

    for path in collectFsFiles srcRoot do
        let content = readFileSync path "utf8"

        for sym in forbiddenSymbols do
            let re = Regex(@"\b" + Regex.Escape(sym) + @"\b")

            if re.IsMatch content then
                failIf true (sprintf "Forbidden legacy prompt symbol '%s' in %s" sym path)

let checkNoPromptTomlBodyKey (srcRoot: string) =
    // Model-facing TOML projections must not reintroduce a root `body =` bag key.
    // Host HTTP envelopes and free-text prose may still contain the word "body".
    let projectionFiles =
        [ "src/Runtime/Prompt/PromptToml.fs"
          "src/Runtime/Tooling/ToolOutputToml.fs"
          "src/Runtime/Tooling/ToolOutputBatchToml.fs"
          "src/Runtime/PromptFragments.fs"
          "src/Runtime/SubagentPrompts.fs"
          "src/Runtime/SubagentSummarizerPrompts.fs"
          "src/Kernel/Methodology/Schema.fs"
          "src/Runtime/ReviewPrompts"
          "src/Runtime/Subsession/Subagent.fs"
          "src/Runtime/Execution/LoopMessages.fs"
          "src/Runtime/Nudge/NudgeDerivation.fs" ]

    let repoRoot = srcRoot.Replace("/src", "")
    let bodyKeyRe = Regex(@"^\s*body\s*=", RegexOptions.Multiline)

    for rel in projectionFiles do
        let abs = pathJoin repoRoot rel

        if existsSync abs then
            if isDirectory abs then
                for path in collectFsFiles abs do
                    let content = readFileSync path "utf8"

                    if bodyKeyRe.IsMatch content then
                        failIf true (sprintf "Forbidden TOML body= key in projection %s" path)
            else
                let content = readFileSync abs "utf8"

                if bodyKeyRe.IsMatch content then
                    failIf true (sprintf "Forbidden TOML body= key in projection %s" abs)

let checkNoHandwrittenTomlOrStringBuilderInProjections (srcRoot: string) =
    let repoRoot = srcRoot.Replace("/src", "")

    let dirs =
        [ "src/Runtime/Prompt"
          "src/Runtime/Tooling"
          "src/Runtime/ReviewPrompts"
          "src/Runtime/Subsession"
          "src/Runtime"
          "src/Kernel/Methodology" ]

    let forbiddenPatterns =
        [ Regex(@"\bStringBuilder\b")
          Regex(@"\bescapeToml\w*\b")
          Regex(@"\.ToLowerInvariant\(\)")
          Regex(@"[""']---\n[""']")
          Regex(@"[""']\n---[""']") ]

    // Toml projection files + prompt producers that must not normalize wire strings.
    let pathAllowed (norm: string) =
        norm.Contains("/Toml")
        || norm.EndsWith("SubagentPrompts.fs")
        || norm.EndsWith("SubagentSummarizerPrompts.fs")
        || norm.EndsWith("Subagent.fs")
        || norm.EndsWith("Schema.fs") && norm.Contains("/Methodology/")
        || norm.Contains("/ReviewPrompts/")

    for dir in dirs do
        let fullDir = pathJoin repoRoot dir

        if existsSync fullDir then
            for path in collectFsFiles fullDir do
                let norm = path.Replace("\\", "/")

                if pathAllowed norm then
                    let content = readFileSync path "utf8"

                    for pat in forbiddenPatterns do
                        if pat.IsMatch content then
                            failIf true (sprintf "Forbidden projection anti-pattern '%s' in %s" (pat.ToString()) path)

let checkNoMarkdownSectionDividersInPromptProducers (repoRoot: string) =
    let dirs =
        [ "src/Runtime/ReviewPrompts"
          "src/Runtime/Subsession"
          "src/Runtime/Prompt" ]

    let forbidden =
        [ Regex(@"===\s*(Affected Files|Original Task|Change Report)\s*===")
          Regex(@"\\n---\\n")
          Regex(@"\bwithReportTail\b")
          Regex(@"reportSeparator") ]

    for dir in dirs do
        let fullDir = pathJoin repoRoot dir

        if existsSync fullDir then
            for path in collectFsFiles fullDir do
                let content = readFileSync path "utf8"

                for pat in forbidden do
                    if pat.IsMatch content then
                        failIf
                            true
                            (sprintf "Forbidden markdown/prose-join anti-pattern '%s' in %s" (pat.ToString()) path)

let run (srcRoot: string) (phase: int) : ResizeArray<string> =
    violations.Clear()
    let repoRoot = srcRoot.Replace("/src", "")

    checkSmolTomlImports srcRoot
    checkNoSmolTomlParseInProduction srcRoot
    checkYamlImports srcRoot

    if phase >= 5 then
        checkDeletedProductionFiles srcRoot
        checkNoLegacyPromptSymbols srcRoot
        checkNoPromptTomlBodyKey srcRoot

    checkNoHandwrittenTomlOrStringBuilderInProjections srcRoot
    checkNoMarkdownSectionDividersInPromptProducers repoRoot

    violations

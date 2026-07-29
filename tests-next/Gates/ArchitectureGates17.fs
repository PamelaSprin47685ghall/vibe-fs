namespace Wanxiangshu.Next.Tests.Gates

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Tests.Gates.ArchitectureGateSupport

module ArchitectureGates17 =

    let private fileText path =
        ArchitectureGateSupport.readFileSync path

    let private norm (p: string) = p.Replace("\\", "/")
    let private isFs (p: string) = p.EndsWith(".fs")

    [<Fact>]
    let ``TASK_section_17_semantic_gates`` () =
        let root = findRepoRoot ()
        let nextDir = NodeFsGatesSupport.pathJoin (root, "next")
        let testsDir = NodeFsGatesSupport.pathJoin (root, "tests-next")
        let nextFiles = collectFsFiles nextDir
        let testsFiles = collectFsFiles testsDir
        let allFiles = List.append nextFiles testsFiles
        let violations = List<string>()

        // §17.1 mechanical filename suffixes
        for file in allFiles do
            let n = norm file
            let leaf = System.IO.Path.GetFileNameWithoutExtension(file)

            match mechanicalSuffixes |> List.tryFind (fun s -> leaf.EndsWith(s)) with
            | Some suffix ->
                if not (Map.containsKey n mechanicalAllowlist) then
                    violations.Add(
                        sprintf "File '%s' has mechanical suffix '%s'; add to allowlist with reason" file suffix
                    )
            | None -> ()

        // §17.2 Host/Fable interop boundary
        // Tests intentionally build raw Host-shaped fixtures. The boundary gate
        // applies to production code; fixture construction is not a production
        // dependency leak.
        for file in nextFiles do
            if isFs file then
                let text = fileText file

                if hasHostInterop text && not (isAllowedHostInteropFile file) then
                    violations.Add(
                        sprintf
                            "File '%s' contains raw Host/Fable dynamic access; move to an explicit codec/adapter or add to allowlist"
                            file
                    )

        // §17.3 single-writer durable facts
        for file in nextFiles do
            if isFs file then
                let n = norm file
                let text = fileText file

                for (fact, allowedPaths, reason) in singleWriterFacts do
                    // Projection folds and type declarations mention fact names;
                    // only an actual AgentFact constructor is a write site.
                    if Regex.IsMatch(text, @"AgentFact\." + Regex.Escape(fact) + @"\b") then
                        if not (allowedPaths |> List.exists n.EndsWith) then
                            violations.Add(
                                sprintf
                                    "File '%s' constructs durable fact '%s' outside its single-writer boundary: %s"
                                    file
                                    fact
                                    reason
                            )

        // §17.4 DSL production callsites and GuideContract
        for (builder, programFile, publicNames) in dslPrograms do
            let programPath = "next/" + programFile

            let programText =
                if NodeFsGatesSupport.existsSync programPath then
                    fileText programPath
                else
                    ""

            let usesBuilder =
                programText.Contains(builder + " {")
                || programText.Contains("``" + builder + "`` {")
                || programText.Contains("FlowBuilder<" + builder)

            if not usesBuilder then
                violations.Add(sprintf "DSL program '%s' does not use the '%s { ... }' builder" programFile builder)

            let programModule = System.IO.Path.GetFileNameWithoutExtension(programFile)
            let mutable referenced = false

            for file in nextFiles do
                if isFs file && not ((norm file).EndsWith(programFile)) then
                    let text = fileText file

                    for name in publicNames do
                        if text.Contains(programModule + "." + name) then
                            referenced <- true

            let guidePath = NodeFsGatesSupport.pathJoin (root, guideContractPath)

            let guideText =
                if NodeFsGatesSupport.existsSync guidePath then
                    fileText guidePath
                else
                    ""

            if builder = "process" then
                if
                    not (
                        nextFiles
                        |> List.exists (fun f ->
                            (norm f).StartsWith("next/Process/")
                            && ((fileText f).Contains("process {") || (fileText f).Contains("``process`` {")))
                    )
                then
                    violations.Add(
                        "No production 'process { ... }' callsite found in next/Process; ProcessRunner uses a private runnerFlow"
                    )

                if not (guideText.Contains("ProcessRunner") || guideText.Contains("process {")) then
                    violations.Add(
                        sprintf "GuideContract '%s' does not reference the production process program" guideContractPath
                    )
            else
                if not referenced then
                    violations.Add(
                        sprintf
                            "DSL program '%s' is orphan: no approved production entrypoint calls %s"
                            programFile
                            (String.concat "/" publicNames)
                    )

                if not (guideText.Contains(programModule)) then
                    violations.Add(
                        sprintf
                            "GuideContract '%s' does not reference the production DSL program '%s'"
                            guideContractPath
                            programModule
                    )

        // §17.5 dependency direction
        for file in allFiles do
            if isFs file then
                let n = norm file

                if lowerLayerDirs |> List.exists n.StartsWith then
                    let text = fileText file

                    for upper in upperLayerOpens do
                        if text.Contains(upper) then
                            violations.Add(
                                sprintf
                                    "File '%s' in lower layer opens '%s'; lower layers must not depend on upper layers"
                                    file
                                    upper
                            )

        // §17.6 duplicate algorithm owners
        for (symbol, allowedPaths) in duplicateAlgorithmSymbols do
            let hits =
                allFiles
                |> List.choose (fun file ->
                    if
                        isFs file
                        && Regex.IsMatch(fileText file, @"\b(let|member|let\s+rec)\s+" + Regex.Escape(symbol) + @"\b")
                    then
                        Some(norm file)
                    else
                        None)

            if hits.Length > 1 then
                if not (hits |> List.forall (fun h -> allowedPaths |> List.exists h.EndsWith)) then
                    violations.Add(
                        sprintf
                            "Algorithm '%s' is defined in %d places (%s); canonical owner(s): %s"
                            symbol
                            hits.Length
                            (String.concat ", " hits)
                            (String.concat ", " allowedPaths)
                    )

        Assert.True(
            violations.Count = 0,
            sprintf "TASK §17 semantic gate violations:\n%s" (String.concat "\n" (violations |> Seq.toList))
        )

namespace Wanxiangshu.Next.Tests

open System.IO
open System.Collections.Generic
open System.Text.RegularExpressions
open Xunit

module ArchitectureGates =

    let findRepoRoot () =
        let sourceDir = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

        let rec search dir =
            let nextDir = Path.Combine(dir, "next")
            let testsNextDir = Path.Combine(dir, "tests-next")

            if Directory.Exists(nextDir) && Directory.Exists(testsNextDir) then
                dir
            else
                let parent = Directory.GetParent(dir)
                if parent <> null then search parent.FullName else dir

        search sourceDir

    let forbiddenTokens =
        [ "Nudge"
          "idleProposals"
          "callOnce"
          "FallbackPhase"
          "ContinuationStage"
          "SubsessionActor"
          "SessionDriverRegistry"
          "EventBus"
          "MailboxProcessor"
          "workspace lockfile"
          "Wait(predicate)"
          "Previous"
          "Fork"
          "Owner"
          "Lease"
          "Coordinator"
          "Registry" ]

    let containsForbiddenToken (text: string) (token: string) =
        if token.Contains("(") || token.Contains(")") || token.Contains(" ") then
            text.Contains(token)
        else
            let pattern = @"\b" + Regex.Escape(token) + @"\b"
            Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant)

    let checkNextFiles (files: seq<string>) (violations: List<string>) =
        for file in files do
            let text = File.ReadAllText(file)

            for token in forbiddenTokens do
                if containsForbiddenToken text token then
                    violations.Add(sprintf "File '%s' contains forbidden token '%s'" file token)

            if file.EndsWith(".fsproj") && text.Contains("ProjectReference") then
                violations.Add(sprintf "File '%s' in 'next' contains forbidden ProjectReference" file)

    let checkTestsNextFiles (files: seq<string>) (violations: List<string>) =
        let gateSelfPath =
            Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "ArchitectureGates.fs"))

        for file in files do
            let fullPath = Path.GetFullPath(file)

            if
                file.EndsWith(".fs")
                && not (System.String.Equals(fullPath, gateSelfPath, System.StringComparison.OrdinalIgnoreCase))
            then
                let text = File.ReadAllText(file)

                for token in forbiddenTokens do
                    if containsForbiddenToken text token then
                        violations.Add(sprintf "File '%s' contains forbidden token '%s'" file token)

            if file.EndsWith(".fsproj") then
                let text = File.ReadAllText(file)

                if text.Contains("ProjectReference") then
                    let allowed =
                        text.Contains("../next/Wanxiangshu.Next.fsproj")
                        || text.Contains(@"..\next\Wanxiangshu.Next.fsproj")

                    let hasLegacyWanxiangshu =
                        text.Contains("wanxiangshu.fsproj")
                        && not (text.Contains("Wanxiangshu.Next.fsproj"))

                    let forbidden =
                        hasLegacyWanxiangshu
                        || text.Contains("../src")
                        || text.Contains(@"..\src")
                        || text.Contains(@"\src")

                    if forbidden || not allowed then
                        violations.Add(sprintf "File '%s' in 'tests-next' contains forbidden ProjectReference" file)

    [<Fact>]
    let ``Next_has_no_legacy_tokens`` () =
        let repoRoot = findRepoRoot ()
        let nextDir = Path.Combine(repoRoot, "next")
        let testsNextDir = Path.Combine(repoRoot, "tests-next")
        Assert.True(Directory.Exists(nextDir), sprintf "Directory 'next' does not exist at %s" nextDir)
        Assert.True(Directory.Exists(testsNextDir), sprintf "Directory 'tests-next' does not exist at %s" testsNextDir)

        let nextFiles = List<string>()
        nextFiles.AddRange(Directory.GetFiles(nextDir, "*.fs", SearchOption.AllDirectories))
        nextFiles.AddRange(Directory.GetFiles(nextDir, "*.fsproj", SearchOption.AllDirectories))
        let propsPathNext = Path.Combine(nextDir, "Directory.Build.props")

        if File.Exists(propsPathNext) then
            nextFiles.Add(propsPathNext)

        let testsNextFiles = List<string>()
        testsNextFiles.AddRange(Directory.GetFiles(testsNextDir, "*.fs", SearchOption.AllDirectories))
        testsNextFiles.AddRange(Directory.GetFiles(testsNextDir, "*.fsproj", SearchOption.AllDirectories))
        let propsPathTests = Path.Combine(testsNextDir, "Directory.Build.props")

        if File.Exists(propsPathTests) then
            testsNextFiles.Add(propsPathTests)

        Assert.NotEmpty(nextFiles)
        Assert.NotEmpty(testsNextFiles)

        let violations = List<string>()
        checkNextFiles nextFiles violations
        checkTestsNextFiles testsNextFiles violations
        Assert.Empty(violations)

    [<Fact>]
    let ``Next_fsproj_has_no_legacy_project_reference`` () =
        let repoRoot = findRepoRoot ()
        let fsprojPath = Path.Combine(repoRoot, "next", "Wanxiangshu.Next.fsproj")
        Assert.True(File.Exists(fsprojPath), sprintf "File 'Wanxiangshu.Next.fsproj' does not exist at %s" fsprojPath)

        let text = File.ReadAllText(fsprojPath)
        Assert.DoesNotContain("ProjectReference", text)
        Assert.DoesNotContain("wanxiangshu.fsproj", text)
        Assert.DoesNotContain("../src", text)

    [<Fact>]
    let ``Next_source_files_do_not_exceed_300_lines`` () =
        let repoRoot = findRepoRoot ()
        let nextDir = Path.Combine(repoRoot, "next")
        Assert.True(Directory.Exists(nextDir), sprintf "Directory 'next' does not exist at %s" nextDir)

        let nextFsFiles = Directory.GetFiles(nextDir, "*.fs", SearchOption.AllDirectories)
        let violations = List<string>()

        for file in nextFsFiles do
            let lineCount = File.ReadAllLines(file).Length

            if lineCount > 300 then
                let relPath = Path.GetRelativePath(repoRoot, file)
                violations.Add(sprintf "File '%s' has %d lines (exceeds maximum of 300 lines)" relPath lineCount)

        Assert.Empty(violations)

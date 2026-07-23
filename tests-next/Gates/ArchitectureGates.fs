namespace Wanxiangshu.Next.Tests.Gates

open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Tests.Gates.ArchitectureGateSupport

module ArchitectureGates =

    let private checkNextFiles (files: seq<string>) (violations: List<string>) =
        for file in files do
            let text = NodeFsGatesSupport.readFileSync (file, "utf-8")

            for token in forbiddenTokens do
                if containsForbiddenToken text token then
                    violations.Add(sprintf "File '%s' contains forbidden legacy workflow token '%s'" file token)

            if
                text.Contains("../src")
                || text.Contains(@"..\src")
                || text.Contains(@"/src/")
                || text.Contains(@"\src\")
                || (text.Contains("open Wanxiangshu.")
                    && not (text.Contains("open Wanxiangshu.Next")))
            then
                violations.Add(sprintf "File '%s' in 'next' contains forbidden reference or import to 'src'" file)

            if file.EndsWith(".fsproj") && text.Contains("ProjectReference") then
                violations.Add(sprintf "File '%s' in 'next' contains forbidden ProjectReference" file)

    let private checkTestsNextFiles (files: seq<string>) (violations: List<string>) =
        for file in files do
            if
                file.EndsWith(".fs")
                && not (file.EndsWith("ArchitectureGates.fs"))
                && not (file.EndsWith("ArchitectureGateSupport.fs"))
            then
                let text = NodeFsGatesSupport.readFileSync (file, "utf-8")

                for token in forbiddenTokens do
                    if containsForbiddenToken text token then
                        violations.Add(sprintf "File '%s' contains forbidden legacy workflow token '%s'" file token)

                if
                    text.Contains("../src")
                    || text.Contains(@"..\src")
                    || text.Contains(@"/src/")
                    || text.Contains(@"\src\")
                    || (text.Contains("open Wanxiangshu.")
                        && not (text.Contains("open Wanxiangshu.Next")))
                then
                    violations.Add(
                        sprintf "File '%s' in 'tests-next' contains forbidden reference or import to 'src'" file
                    )

            if file.EndsWith(".fsproj") then
                let text = NodeFsGatesSupport.readFileSync (file, "utf-8")

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
    let ``Next_has_no_legacy_workflow_tokens_or_src_imports`` () =
        let repoRoot = findRepoRoot ()
        let nextDir = NodeFsGatesSupport.pathJoin (repoRoot, "next")
        let testsNextDir = NodeFsGatesSupport.pathJoin (repoRoot, "tests-next")
        Assert.True(NodeFsGatesSupport.existsSync nextDir, sprintf "Directory 'next' does not exist at %s" nextDir)

        Assert.True(
            NodeFsGatesSupport.existsSync testsNextDir,
            sprintf "Directory 'tests-next' does not exist at %s" testsNextDir
        )

        let nextFiles = List<string>()

        for f in collectFsFiles nextDir do
            nextFiles.Add(f)

        let propsPathNext = NodeFsGatesSupport.pathJoin (nextDir, "Directory.Build.props")

        if NodeFsGatesSupport.existsSync propsPathNext && isDir propsPathNext then
            nextFiles.Add(propsPathNext)

        let testsNextFiles = List<string>()

        for f in collectFsFiles testsNextDir do
            testsNextFiles.Add(f)

        let propsPathTests =
            NodeFsGatesSupport.pathJoin (testsNextDir, "Directory.Build.props")

        if NodeFsGatesSupport.existsSync propsPathTests && isDir propsPathTests then
            testsNextFiles.Add(propsPathTests)

        let violations = List<string>()
        checkNextFiles nextFiles violations
        checkTestsNextFiles testsNextFiles violations

        if violations.Count > 0 then
            Assert.True(
                false,
                sprintf
                    "Next_has_no_legacy_workflow_tokens_or_src_imports violations:\n%s"
                    (String.concat "\n" (violations |> Seq.toList))
            )

    [<Fact>]
    let ``Next_fsproj_has_no_legacy_project_reference`` () =
        let repoRoot = findRepoRoot ()

        let fsprojPath =
            NodeFsGatesSupport.pathJoin (repoRoot, NodeFsGatesSupport.pathJoin ("next", "Wanxiangshu.Next.fsproj"))

        Assert.True(
            NodeFsGatesSupport.existsSync fsprojPath,
            sprintf "File 'Wanxiangshu.Next.fsproj' does not exist at %s" fsprojPath
        )

        let text = NodeFsGatesSupport.readFileSync (fsprojPath, "utf-8")
        Assert.False(text.Contains("ProjectReference"))
        Assert.False(text.Contains("wanxiangshu.fsproj"))
        Assert.False(text.Contains("../src"))

    [<Fact>]
    let ``Next_source_files_do_not_exceed_300_lines`` () =
        let repoRoot = findRepoRoot ()
        let nextDir = NodeFsGatesSupport.pathJoin (repoRoot, "next")
        let testsNextDir = NodeFsGatesSupport.pathJoin (repoRoot, "tests-next")
        let nextFsFiles = collectFsFiles nextDir
        let testsFsFiles = collectFsFiles testsNextDir
        let violations = List<string>()

        for file in (List.append nextFsFiles testsFsFiles) do
            if file.EndsWith(".fs") then
                let text = NodeFsGatesSupport.readFileSync (file, "utf-8")

                let lineCount =
                    text.Split([| "\r\n"; "\n" |], System.StringSplitOptions.None).Length

                if lineCount > 300 then
                    violations.Add(sprintf "File '%s' has %d lines (exceeds maximum of 300 lines)" file lineCount)

        Assert.Empty(violations)

    [<Fact>]
    let ``Next_recursive_scan_includes_subdirectory_files`` () =
        let repoRoot = findRepoRoot ()
        let nextDir = NodeFsGatesSupport.pathJoin (repoRoot, "next")
        let files = collectFsFiles nextDir

        Assert.True(
            List.length files >= 10,
            sprintf "Expected recursive scan to return >= 10 files, got %d" (List.length files)
        )

        let fromSubdir (token: string) (leaf: string) =
            files |> List.exists (fun f -> f.Contains(token) && f.EndsWith(leaf))

        Assert.True(fromSubdir "Kernel" "Flow.fs", "Recursive scan missed next/Kernel/Flow.fs")
        Assert.True(fromSubdir "Journal" "Writer.fs", "Recursive scan missed next/Journal/Writer.fs")
        Assert.True(fromSubdir "OpenCode" "Plugin.fs", "Recursive scan missed next/OpenCode/Plugin.fs")
        Assert.True(fromSubdir "Tools" "StaticTools.fs", "Recursive scan missed next/Tools/StaticTools.fs")

    [<Fact>]
    let ``Test_runner_timeout_is_strictly_1s`` () =
        let repoRoot = findRepoRoot ()

        let runnerPath =
            NodeFsGatesSupport.pathJoin (repoRoot, NodeFsGatesSupport.pathJoin ("tests-next", "runner.js"))

        Assert.True(NodeFsGatesSupport.existsSync runnerPath, sprintf "runner.js not found at %s" runnerPath)
        let content = NodeFsGatesSupport.readFileSync (runnerPath, "utf-8")

        Assert.True(
            content.Contains("runTestInWorker") && content.Contains("1000"),
            "runner.js must enforce rigid 1000ms (1s) process timeout limit for all tests"
        )

        Assert.True(
            content.Contains("detached: true") && content.Contains("SIGKILL"),
            "runner.js must hard-stop the isolated test process tree on timeout"
        )

    [<Fact>]
    let ``No_sleep_or_setTimeout_allowed_in_tests_or_production`` () =
        let repoRoot = findRepoRoot ()
        let nextDir = NodeFsGatesSupport.pathJoin (repoRoot, "next")
        let testsNextDir = NodeFsGatesSupport.pathJoin (repoRoot, "tests-next")
        let allFiles = List.append (collectFsFiles nextDir) (collectFsFiles testsNextDir)
        let violations = List<string>()

        for file in allFiles do
            if
                file.EndsWith(".fs")
                && not (file.EndsWith("ArchitectureGates.fs"))
                && not (file.EndsWith("ArchitectureGateSupport.fs"))
            then
                let text = NodeFsGatesSupport.readFileSync (file, "utf-8")

                if containsForbiddenToken text "sleepJs" then
                    violations.Add(sprintf "File '%s' contains forbidden sleep token 'sleepJs'" file)

                if containsForbiddenToken text "sleep" then
                    violations.Add(sprintf "File '%s' contains forbidden sleep token 'sleep'" file)

        Assert.Empty(violations)

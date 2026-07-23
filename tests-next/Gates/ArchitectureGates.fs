namespace Wanxiangshu.Next.Tests

open System.Collections.Generic
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Xunit

module private NodeFsGates =
    [<Import("existsSync", "node:fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("readFileSync", "node:fs")>]
    let readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("readdirSync", "node:fs")>]
    let readdirSync (path: string) : string array = jsNative

    [<Import("statSync", "node:fs")>]
    let statSync (path: string) : obj = jsNative

    [<Import("join", "node:path")>]
    let pathJoin (a: string, b: string) : string = jsNative

    [<Import("dirname", "node:path")>]
    let pathDirname (p: string) : string = jsNative

    let isDir (path: string) : bool =
        try
            let s = statSync path

            if isNull s then
                false
            else
                let p = s?isDirectory ()
                if isNull p then false else unbox<bool> p
        with _ ->
            false

module ArchitectureGates =

    let findRepoRoot () =
        if NodeFsGates.existsSync "next" then "."
        elif NodeFsGates.existsSync "../next" then ".."
        elif NodeFsGates.existsSync "../../next" then "../.."
        else "."

    let private collectFsFiles (root: string) : string list =
        let rec walk (dir: string) (acc: string list) =
            let entries = NodeFsGates.readdirSync dir
            let mutable result = acc

            for e in entries do
                let full = NodeFsGates.pathJoin (dir, e)

                if e = "fable_modules" || e = "node_modules" || e = ".git" then
                    ()
                elif NodeFsGates.isDir full then
                    result <- walk full result
                elif e.EndsWith(".fs") || e.EndsWith(".fsproj") then
                    result <- full :: result
                else
                    ()

            result

        walk root []

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

    let private containsForbiddenToken (text: string) (token: string) =
        if token.Contains("(") || token.Contains(")") || token.Contains(" ") then
            text.Contains(token)
        else
            let pattern = @"\b" + Regex.Escape(token) + @"\b"
            Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase)

    let private checkNextFiles (files: seq<string>) (violations: List<string>) =
        for file in files do
            let text = NodeFsGates.readFileSync (file, "utf-8")

            for token in forbiddenTokens do
                if containsForbiddenToken text token then
                    violations.Add(sprintf "File '%s' contains forbidden token '%s'" file token)

            if file.EndsWith(".fsproj") && text.Contains("ProjectReference") then
                violations.Add(sprintf "File '%s' in 'next' contains forbidden ProjectReference" file)

    let private checkTestsNextFiles (files: seq<string>) (violations: List<string>) =
        for file in files do
            if file.EndsWith(".fs") && not (file.EndsWith("ArchitectureGates.fs")) then
                let text = NodeFsGates.readFileSync (file, "utf-8")

                for token in forbiddenTokens do
                    if containsForbiddenToken text token then
                        violations.Add(sprintf "File '%s' contains forbidden token '%s'" file token)

            if file.EndsWith(".fsproj") then
                let text = NodeFsGates.readFileSync (file, "utf-8")

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
        let nextDir = NodeFsGates.pathJoin (repoRoot, "next")
        let testsNextDir = NodeFsGates.pathJoin (repoRoot, "tests-next")
        Assert.True(NodeFsGates.existsSync (nextDir), sprintf "Directory 'next' does not exist at %s" nextDir)

        Assert.True(
            NodeFsGates.existsSync (testsNextDir),
            sprintf "Directory 'tests-next' does not exist at %s" testsNextDir
        )

        let nextFiles = List<string>()

        let nextFs = collectFsFiles nextDir

        for f in nextFs do
            nextFiles.Add(f)

        let propsPathNext = NodeFsGates.pathJoin (nextDir, "Directory.Build.props")

        if NodeFsGates.existsSync (propsPathNext) && NodeFsGates.isDir propsPathNext then
            nextFiles.Add(propsPathNext)

        let testsNextFiles = List<string>()

        let testsFs = collectFsFiles testsNextDir

        for f in testsFs do
            testsNextFiles.Add(f)

        let propsPathTests = NodeFsGates.pathJoin (testsNextDir, "Directory.Build.props")

        if NodeFsGates.existsSync (propsPathTests) && NodeFsGates.isDir propsPathTests then
            testsNextFiles.Add(propsPathTests)

        Assert.NotEmpty(nextFiles)
        Assert.NotEmpty(testsNextFiles)

        let violations = List<string>()
        checkNextFiles nextFiles violations
        checkTestsNextFiles testsNextFiles violations

        if violations.Count > 0 then
            let msg = String.concat "\n" (violations |> Seq.toList)
            Assert.True(false, sprintf "Next_has_no_legacy_tokens violations:\n%s" msg)

    [<Fact>]
    let ``Next_fsproj_has_no_legacy_project_reference`` () =
        let repoRoot = findRepoRoot ()

        let fsprojPath =
            NodeFsGates.pathJoin (repoRoot, NodeFsGates.pathJoin ("next", "Wanxiangshu.Next.fsproj"))

        Assert.True(
            NodeFsGates.existsSync (fsprojPath),
            sprintf "File 'Wanxiangshu.Next.fsproj' does not exist at %s" fsprojPath
        )

        let text = NodeFsGates.readFileSync (fsprojPath, "utf-8")
        Assert.False(text.Contains("ProjectReference"))
        Assert.False(text.Contains("wanxiangshu.fsproj"))
        Assert.False(text.Contains("../src"))

    [<Fact>]
    let ``Next_source_files_do_not_exceed_300_lines`` () =
        let repoRoot = findRepoRoot ()
        let nextDir = NodeFsGates.pathJoin (repoRoot, "next")
        Assert.True(NodeFsGates.existsSync (nextDir), sprintf "Directory 'next' does not exist at %s" nextDir)

        let nextFsFiles = collectFsFiles nextDir

        let violations = List<string>()

        for file in nextFsFiles do
            let text = NodeFsGates.readFileSync (file, "utf-8")

            let lineCount =
                text.Split([| "\r\n"; "\n" |], System.StringSplitOptions.None).Length

            if lineCount > 300 then
                violations.Add(sprintf "File '%s' has %d lines (exceeds maximum of 300 lines)" file lineCount)

        Assert.Empty(violations)

    [<Fact>]
    let ``Next_recursive_scan_includes_subdirectory_files`` () =
        let repoRoot = findRepoRoot ()
        let nextDir = NodeFsGates.pathJoin (repoRoot, "next")
        Assert.True(NodeFsGates.existsSync (nextDir), sprintf "Directory 'next' does not exist at %s" nextDir)

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

        let knownSubdirs =
            [ "Kernel"
              "Journal"
              "OpenCode"
              "Tools"
              "Session"
              "Process"
              "Wanxiangzhen" ]

        let observedSubdirs =
            knownSubdirs
            |> List.filter (fun token -> files |> List.exists (fun f -> f.Contains(token)))

        Assert.True(
            List.length observedSubdirs >= 3,
            sprintf "Recursive scan only observed %d subdirs (expected >= 3)" (List.length observedSubdirs)
        )

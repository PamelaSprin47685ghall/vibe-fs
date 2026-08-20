namespace Wanxiangshu.Requirement.Grounding

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Repository.Programming.Js

module GroundingCatalog =

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("readdirSync", "node:fs")>]
    let private readdirSync (path: string) : string array = jsNative

    [<Import("statSync", "node:fs")>]
    let private statSync (path: string) : obj = jsNative

    [<Import("realpathSync", "node:fs")>]
    let private realpathSync (path: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string, b: string) : string = jsNative

    [<Import("relative", "node:path")>]
    let private pathRelative (fromPath: string, toPath: string) : string = jsNative

    [<Import("resolve", "node:path")>]
    let private pathResolve (path: string) : string = jsNative

    [<Import("isAbsolute", "node:path")>]
    let private pathIsAbsolute (path: string) : bool = jsNative

    [<Emit("$0.isDirectory()")>]
    let private isDirectory (stat: obj) : bool = jsNative

    [<Emit("$0.isFile()")>]
    let private isFile (stat: obj) : bool = jsNative

    type ScopeRule = { Include: bool; Pattern: string }

    type PackageDescriptor =
        { Name: string
          Root: string
          Rules: ScopeRule list }

    let private slash (value: string) = value.Replace('\\', '/')

    let canonicalWorkspace (workspace: string) =
        let resolved = pathResolve workspace

        try
            realpathSync resolved
        with _ ->
            resolved

    let private absolutePath root path =
        if pathIsAbsolute path then
            pathResolve path
        else
            pathResolve (pathJoin (root, path))

    let private workspaceRelative root path =
        let relative = pathRelative (root, absolutePath root path) |> slash

        if relative = "" then
            Some ""
        elif
            relative = ".."
            || relative.StartsWith("../", StringComparison.Ordinal)
            || pathIsAbsolute relative
        then
            None
        else
            Some relative

    let private matches pattern path =
        match JsGlobFs.matchesPathPattern pattern path with
        | Ok value -> value
        | Error _ -> invalidOp ("invalid APPLIES-TO pattern: " + pattern)

    let private ruleBody (raw: string) =
        let line = raw.Trim()

        if line = "" || line.StartsWith("#", StringComparison.Ordinal) then
            None
        else
            let includeRule = not (line.StartsWith("!", StringComparison.Ordinal))
            let pattern = if includeRule then line else line.Substring 1
            Some(includeRule, pattern)

    let private validateRule packageName includeRule pattern =
        let selfProbe = "requirements/" + packageName + "/WHAT.md"

        if String.IsNullOrWhiteSpace pattern then
            None
        elif matches pattern selfProbe then
            invalidOp ("APPLIES-TO must not declare package self coverage: " + packageName)
        else
            Some
                { Include = includeRule
                  Pattern = pattern }

    let private parseRule (packageName: string) (raw: string) =
        ruleBody raw
        |> Option.bind (fun (includeRule, pattern) -> validateRule packageName includeRule pattern)

    let private directoryExists path =
        try
            isDirectory (statSync path)
        with _ ->
            false

    let private loadRules packageName packageRoot =
        let path = pathJoin (packageRoot, "APPLIES-TO")

        if not (existsSync path) then
            []
        else
            let text = readFileSync (path, "utf8")

            text.Split('\n') |> Array.toList |> List.choose (parseRule packageName)

    let private tryPackage requirementsRoot name =
        let root = pathJoin (requirementsRoot, name)
        let what = pathJoin (root, "WHAT.md")

        if directoryExists root && existsSync what then
            Some
                { Name = name
                  Root = root
                  Rules = loadRules name root }
        else
            None

    let discover workspace =
        let root = canonicalWorkspace workspace
        let requirementsRoot = pathJoin (root, "requirements")

        if not (existsSync requirementsRoot) then
            []
        else
            readdirSync requirementsRoot
            |> Array.toList
            |> List.choose (tryPackage requirementsRoot)
            |> List.sortBy _.Name

    let private externalMatch relativePath package =
        package.Rules
        |> List.fold
            (fun included rule ->
                if matches rule.Pattern relativePath then
                    rule.Include
                else
                    included)
            false

    let private selfMatch relativePath package =
        let selfPrefix = "requirements/" + package.Name + "/"

        relativePath = "requirements/" + package.Name
        || relativePath.StartsWith(selfPrefix, StringComparison.Ordinal)

    let private packageMatches relativePath package =
        selfMatch relativePath package || externalMatch relativePath package

    let resolve workspace path =
        let root = canonicalWorkspace workspace

        match workspaceRelative root path with
        | None -> []
        | Some relativePath -> discover root |> List.filter (packageMatches relativePath) |> List.sortBy _.Name

    let private directMarkdownPaths packageRoot =
        readdirSync packageRoot
        |> Array.toList
        |> List.filter (fun name -> name.EndsWith(".md", StringComparison.Ordinal))
        |> List.filter (fun name ->
            let full = pathJoin (packageRoot, name)

            try
                isFile (statSync full)
            with _ ->
                false)
        |> List.sort

    let private materializePaths root package packageRelativePaths =
        let materials =
            packageRelativePaths
            |> List.map (fun packageRelative ->
                let full = pathJoin (package.Root, packageRelative)

                { Path = "requirements/" + package.Name + "/" + slash packageRelative
                  ResultBytes = readFileSync (full, "utf8") })

        let digestInput =
            materials
            |> List.map (fun material -> material.Path + "\u0000" + material.ResultBytes + "\u0000")
            |> String.concat ""

        { Workspace = root
          PackageName = package.Name
          Digest = HostDigest.sha256Hex digestInput
          Materials = materials }

    let private materializePackageMaterials root package =
        // Requirement Grounding injects ONLY direct root-level *.md files for normative guidance.
        // tests/** (executable proof) and APPLIES-TO (manifest metadata) are never injected into context.
        directMarkdownPaths package.Root |> materializePaths root package

    let materialize workspace packageName =
        let root = canonicalWorkspace workspace

        let package =
            discover root
            |> List.tryFind (fun candidate -> candidate.Name = packageName)
            |> Option.defaultWith (fun () -> invalidArg "packageName" ("unknown requirement package: " + packageName))

        materializePackageMaterials root package

    let snapshotsForPaths workspace paths =
        let root = canonicalWorkspace workspace
        let packages = discover root

        paths
        |> List.choose (workspaceRelative root)
        |> List.collect (fun relativePath -> packages |> List.filter (packageMatches relativePath))
        |> List.groupBy _.Name
        |> List.sortBy fst
        |> List.map (fun (_, matches) ->
            let package = List.head matches
            materializePackageMaterials root package)

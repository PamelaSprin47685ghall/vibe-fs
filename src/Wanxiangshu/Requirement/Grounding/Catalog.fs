namespace Wanxiangshu.Requirement.Grounding

open Fable.Core
open Fable.Core.JsInterop

/// 需求发现与材料溯源的算法内核已抽取到独立库 `wanxiangli/grounding`
/// （CUT-26）。本模块保持主仓签名稳定，把库的 JS 原生结果解码回
/// F# record；发现、APPLIES-TO 匹配、物化与 Digest 计算都住在库里。
module GroundingCatalog =

    [<Import("Grounding", "wanxiangli/grounding")>]
    let private grounding: obj = jsNative

    type ScopeRule = { Include: bool; Pattern: string }

    type PackageDescriptor =
        { Name: string
          Root: string
          Rules: ScopeRule list }

    [<Emit("$0.discover($1)")>]
    let private rawDiscover (_api: obj) (_workspace: string): obj = jsNative

    [<Emit("$0.resolve($1, $2)")>]
    let private rawResolve (_api: obj) (_workspace: string) (_path: string): obj = jsNative

    [<Emit("$0.materialize($1, $2)")>]
    let private rawMaterialize (_api: obj) (_workspace: string) (_packageName: string): obj = jsNative

    [<Emit("$0.snapshotsForPaths($1, $2)")>]
    let private rawSnapshotsForPaths (_api: obj) (_workspace: string) (_paths: obj): obj = jsNative

    [<Emit("$0.canonicalWorkspace($1)")>]
    let private rawCanonicalWorkspace (_api: obj) (_workspace: string): string = jsNative

    [<Emit("$0.workspaceRelativePath($1, $2)")>]
    let private rawWorkspaceRelativePath (_api: obj) (_workspace: string) (_path: string): obj = jsNative

    [<Emit("Array.from($0)")>]
    let private toArray (_value: obj): obj array = jsNative

    let private text (value: obj) (key: string) = unbox<string> (value?(key))

    let private decodeRule (rule: obj) : ScopeRule =
        { Include = unbox<bool> (rule?("include"))
          Pattern = text rule "pattern" }

    let private decodePackage (entry: obj) : PackageDescriptor =
        let rules =
            toArray (entry?("rules"))
            |> Array.map decodeRule
            |> Array.toList

        { Name = text entry "name"
          Root = text entry "root"
          Rules = rules }

    let private decodeSnapshot (snapshot: obj) : GroundingSnapshot =
        let materials =
            toArray (snapshot?("materials"))
            |> Array.map (fun material ->
                { Path = text material "path"
                  ResultBytes = text material "resultBytes" })
            |> Array.toList

        { Workspace = text snapshot "workspace"
          PackageName = text snapshot "packageName"
          Digest = text snapshot "digest"
          Materials = materials }

    let canonicalWorkspace (workspace: string) = rawCanonicalWorkspace grounding workspace

    let workspaceRelativePath (workspace: string) (path: string) : string option =
        let relative = rawWorkspaceRelativePath grounding workspace path

        if isNull relative then None else Some(unbox<string> relative)

    let discover (workspace: string) : PackageDescriptor list =
        rawDiscover grounding workspace |> toArray |> Array.map decodePackage |> Array.toList

    let resolve (workspace: string) (path: string) : PackageDescriptor list =
        let names = rawResolve grounding workspace path |> toArray |> Array.map (unbox<string>)

        discover workspace
        |> List.filter (fun package -> Array.contains package.Name names)
        |> List.sortBy (fun package -> package.Name)

    let materialize (workspace: string) (packageName: string) : GroundingSnapshot =
        rawMaterialize grounding workspace packageName |> decodeSnapshot

    let snapshotsForPaths (workspace: string) (paths: string list) : GroundingSnapshot list =
        let encoded = paths |> List.toArray |> box
        rawSnapshotsForPaths grounding workspace encoded
        |> toArray
        |> Array.map decodeSnapshot
        |> Array.toList

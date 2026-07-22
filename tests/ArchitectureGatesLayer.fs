module Wanxiangshu.Tests.ArchitectureGatesLayer

open Fable.Core
open Fable.Core.JsInterop
open System.Text.RegularExpressions
open Wanxiangshu.Tests.ArchitectureGatesFs

let private violations = ResizeArray<string>()

let private failIf (cond: bool) (msg: string) =
    if cond then
        violations.Add msg

let checkKernelNoFable (kernelRoot: string) =
    let re = Regex(@"\bFable\.Core\b|\bJsInterop\b")

    for path in collectFsFiles kernelRoot do
        let content = readFileSync path "utf8"
        failIf (re.IsMatch content) (sprintf "Kernel must not reference Fable.Core/JsInterop: %s" path)

let checkKernelNoRuntimeOrHosts (kernelRoot: string) =
    let re =
        Regex(@"open\s+Wanxiangshu\.(Runtime|Hosts)\b|Wanxiangshu\.(Runtime|Hosts)\.")

    for path in collectFsFiles kernelRoot do
        let content = readFileSync path "utf8"
        failIf (re.IsMatch content) (sprintf "Kernel must not reference Runtime/Hosts: %s" path)

let checkRuntimeNoHosts (runtimeRoot: string) =
    let re = Regex(@"open\s+Wanxiangshu\.Hosts\b|Wanxiangshu\.Hosts\.")

    for path in collectFsFiles runtimeRoot do
        let content = readFileSync path "utf8"
        failIf (re.IsMatch content) (sprintf "Runtime must not reference Hosts: %s" path)

let checkNoCrossHost (hostsRoot: string) =
    let rules =
        [ "Mux", [ "Hosts.Omp"; "Hosts.OpenCode"; "Hosts.Opencode" ]
          "Omp", [ "Hosts.Mux"; "Hosts.OpenCode"; "Hosts.Opencode" ]
          "OpenCode", [ "Hosts.Mux"; "Hosts.Omp" ] ]

    for hostDir, forbidden in rules do
        let dir = pathJoin hostsRoot hostDir

        if existsSync dir then
            for path in collectFsFiles dir do
                let content = readFileSync path "utf8"

                for frag in forbidden do
                    failIf (content.Contains("Wanxiangshu." + frag)) (sprintf "cross-host reference %s in %s" frag path)

let run (srcRoot: string) : ResizeArray<string> =
    violations.Clear()
    checkKernelNoFable (pathJoin srcRoot "Kernel")
    checkKernelNoRuntimeOrHosts (pathJoin srcRoot "Kernel")
    checkRuntimeNoHosts (pathJoin srcRoot "Runtime")
    checkNoCrossHost (pathJoin srcRoot "Hosts")
    violations

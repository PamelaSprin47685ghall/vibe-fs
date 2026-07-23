namespace Wanxiangshu.Next.Tests.Gates

open System.Collections.Generic
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop

module private NodeFsGatesSupport =
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

module ArchitectureGateSupport =

    let isDir (path: string) : bool =
        try
            let s = NodeFsGatesSupport.statSync path
            if isNull s then false else unbox<bool> (s?isDirectory ())
        with _ ->
            false

    let findRepoRoot () =
        if NodeFsGatesSupport.existsSync "next" then "."
        elif NodeFsGatesSupport.existsSync "../next" then ".."
        elif NodeFsGatesSupport.existsSync "../../next" then "../.."
        else "."

    let collectFsFiles (root: string) : string list =
        let rec walk (dir: string) (acc: string list) =
            let entries = NodeFsGatesSupport.readdirSync dir
            let mutable result = acc

            for e in entries do
                let full = NodeFsGatesSupport.pathJoin (dir, e)

                if e = "fable_modules" || e = "node_modules" || e = ".git" then
                    ()
                elif isDir full then
                    result <- walk full result
                elif e.EndsWith(".fs") || e.EndsWith(".fsproj") then
                    result <- full :: result
                else
                    ()

            result

        walk root []

    let forbiddenTokens =
        [ "idleProposals"
          "callOnce"
          "FallbackPhase"
          "ContinuationStage"
          "ReviewPhase"
          "SessionStage"
          "JoinOwner"
          "NudgeLease"
          "CompactionGeneration"
          "SessionActor"
          "SubsessionActor"
          "WorkflowRegistry"
          "JournalDrivenWorkflow"
          "TodoState"
          "Methodology"
          "SquadWave"
          "EventStore"
          "SessionDriverRegistry"
          "EventBus"
          "MailboxProcessor"
          "workspace lockfile"
          "Wait(predicate)"
          "sleepJs"
          "setTimeout" ]

    let containsForbiddenToken (text: string) (token: string) =
        if token.Contains("(") || token.Contains(")") || token.Contains(" ") then
            text.Contains(token)
        else
            Regex.IsMatch(text, @"\b" + Regex.Escape(token) + @"\b", RegexOptions.IgnoreCase)

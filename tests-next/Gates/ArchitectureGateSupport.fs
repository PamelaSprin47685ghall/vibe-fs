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

    let readFileSync (path: string) = NodeFsGatesSupport.readFileSync (path, "utf-8")

    let forbiddenTokens =
        [ "idleProposals"; "callOnce"; "FallbackPhase"; "FallbackState"; "ContinuationStage"
          "ReviewPhase"; "ReviewStages"; "SessionStage"; "JoinOwner"; "NudgeLease"
          "CompactionGeneration"; "SessionActor"; "SubsessionActor"; "WorkflowRegistry"
          "JournalDrivenWorkflow"; "TodoState"; "Methodology"; "SquadWave"; "EventStore"
          "SessionDriverRegistry"; "EventBus"; "MailboxProcessor"; "workspace lockfile"
          "Wait(predicate)"; "sleepJs"; "type ReviewState"; "recordFailureForTests"; "Advisor" ]

    let containsForbiddenToken (text: string) (token: string) =
        if token.Contains("(") || token.Contains(")") || token.Contains(" ") then
            text.Contains(token)
        else
            Regex.IsMatch(text, @"\b" + Regex.Escape(token) + @"\b", RegexOptions.IgnoreCase)

    let forbiddenSseEventTokens =
        [ "message.part.delta"; "message.part.updated"; "message.updated"; "session.diff"; "session.updated" ]

    let sessionStatusAllowlist =
        [ "next/OpenCode/HostEventCodec.fs"; "next/OpenCode/HostSignalAdapter.fs"
          "next/OpenCode/RetrySignalHandler.fs"; "next/OpenCode/HostSignalSubscribe.fs" ]

    let sessionErrorAllowlist =
        [ "next/OpenCode/HostSignalAdapter.fs"; "next/OpenCode/HostSignal.fs"; "next/OpenCode/HostSignalBootstrap.fs" ]

    let isNextDocPath (file: string) : bool =
        file.Replace("\\", "/").Contains("/next/Doc/")

    // TASK §17 allowlists and helpers

    let mechanicalSuffixes = [ "Helpers"; "Primitives"; "Fields"; "Emit"; "Service"; "Core" ]

    let mechanicalAllowlist =
        Map
            [ "next/Session/AgentRoleHelpers.fs", "legacy: pending rename to a semantic module"
              "next/OpenCode/SpikePluginHelpers.fs", "legacy: pending rename to a semantic module"
              "next/OpenCode/TerminalPolicyHelpers.fs", "legacy: pending rename to a semantic module"
              "next/OpenCode/CompanionTransformHelpers.fs", "legacy: pending rename to a semantic module" ]

    let hasHostInterop (text: string) =
        [ "Fable.Core.JsInterop"; "jsNative"; "createObj"; "unbox" ]
        |> List.exists text.Contains
        || Regex.IsMatch(text, @"[\w\)]\?[a-zA-Z]")

    let private hostInteropNamePattern =
        Regex(
            @"(Host|Port|Codec|Adapter|Boot|Runtime|Writer|Node|Plugin|Supervisor|Backend|Projection|Transform|Signal|Json|Git|Flow|Pty|Tool|Subscribe|Canonical|Process)",
            RegexOptions.IgnoreCase)

    let private hostInteropExplicitAllowlist =
        Map
            [ "next/Session/CompanionDelta.fs", "companion canonical hash and projection delta"
              "next/Orchestrator.IntegrationGate.fs", "external lockfile host adapter"
              "next/Orchestrator.WorktreeResource.fs", "external worktree/ValueTask adapter" ]

    let isAllowedHostInteropFile (path: string) =
        let n = path.Replace("\\", "/")
        if Map.containsKey n hostInteropExplicitAllowlist then true
        else hostInteropNamePattern.IsMatch(System.IO.Path.GetFileName(path))

    let singleWriterFacts =
        [ ("FallbackFailureRecorded",
           [ "OpenCode/FallbackDetect.fs"; "Journal/AgentJournal.fs"; "Journal/Fold.fs"; "Kernel/Fact.fs" ],
           "only RetrySignalHandler may build the fallback failure fact")
          ("ReviewConfirmedIdle",
           [ "Journal/AgentJournal.fs"; "OpenCode/TurnCompletionProgram.fs"; "Journal/Fold.fs"; "Kernel/Fact.fs" ],
           "only TurnCompletionProgram may record a confirmed reviewer idle")
          ("PluginPromptClaimed",
           [ "OpenCode/PromptDispatcherSend.fs"; "OpenCode/PromptDispatcher.fs"; "Journal/PromptAuthorityLedger.fs"; "Journal/Fold.fs"; "Kernel/Fact.fs" ],
           "only PromptDispatcher may claim a plugin prompt")
          ("PluginPromptAccepted",
           [ "OpenCode/PromptDispatcher.fs"; "Journal/PromptAuthorityLedger.fs"; "Journal/Fold.fs"; "Kernel/Fact.fs" ],
           "only PromptDispatcher may accept a plugin prompt")
          ("PluginPromptAbandoned",
           [ "OpenCode/PromptDispatcherSend.fs"; "OpenCode/PromptDispatcher.fs"; "Journal/PromptAuthorityLedger.fs"; "Journal/Fold.fs"; "Kernel/Fact.fs" ],
           "only PromptDispatcher may abandon a plugin prompt") ]

    let dslPrograms =
        [ ("agent", "Agent/AgentProgram.fs", [ "forkAgent"; "validateSession"; "runAgentFlow" ])
          ("companion", "Session/CompanionProgram.fs", [ "buildDelta"; "shouldReplacePrefix"; "runCompanionFlow" ])
          ("review", "Review/ReviewProgram.fs", [ "recordVerdict"; "confirmPerfect"; "runReviewFlow" ])
          ("orchestrator", "Orchestrator/OrchestratorProgram.fs", [ "run" ])
          ("process", "Process/ProcessRunner.fs", [ "run"; "runWithHost" ]) ]

    let guideContractPath = "tests-next/GuideContract/Signatures.fs"

    let lowerLayerDirs = [ "next/Kernel/"; "next/Domain/" ]

    let upperLayerOpens =
        [ "Wanxiangshu.Next.OpenCode"; "Wanxiangshu.Next.Session"; "Wanxiangshu.Next.Process"
          "Wanxiangshu.Next.Journal"; "Wanxiangshu.Next.Orchestrator"; "Wanxiangshu.Next.Review"
          "Wanxiangshu.Next.Agent"; "Wanxiangshu.Next.Tools" ]

    let duplicateAlgorithmSymbols =
        [ ("advance", [ "Domain/AgentPairCursor.fs" ])
          ("effectiveAgent", [ "Domain/AgentPairCursor.fs"; "Domain/PromptAuthority.fs" ])
          ("peerAgent", [ "Domain/PromptAuthority.fs" ])
          ("sha256Hex", [ "Domain/PromptAuthority.fs" ])
          ("reviewWitness", [ "Domain/ReviewWitness.fs" ])
          ("confirmPerfect", [ "Review/ReviewProgram.fs" ]) ]

    let codecAllowlistFor280 =
        [ "next/OpenCode/HostEventCodec.fs"; "next/OpenCode/HostMessageCodec.fs"; "next/OpenCode/CanonicalJson.fs"
          "next/OpenCode/ToolHostCodec.fs"; "next/OpenCode/Projection.fs"; "next/OpenCode/PromptIngress.fs"
          "next/OpenCode/HostSessionContext.fs"; "next/OpenCode/HostSignalAdapter.fs"
          "next/OpenCode/HostSignalSubscribe.fs" ]

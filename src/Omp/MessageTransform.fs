module VibeFs.Omp.MessageTransform

// OMP caps use Shell.OmpCaps (workspace CAPS.md scan), not Mux CapsFileCache / synth caps pipeline.
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell.Dyn
module Dyn = VibeFs.Shell.Dyn
open VibeFs.Shell.OmpCaps
open VibeFs.Shell.TreeSitterShell

let beforeAgentStart (cwd: string) (systemPrompt: obj) : JS.Promise<obj> =
    promise {
        let! caps = buildCapsContextAsync cwd
        let stripped = stripHostAgentsPrompt systemPrompt
        if caps = "" then return createObj [ "systemPrompt", box stripped ]
        else
            let prefix = [| caps |]
            let merged =
                if stripped.Length = 0 then prefix
                else Array.append [| caps |] stripped
            return createObj [ "systemPrompt", box merged ]
    }

let appendToolResultSyntax (cwd: string) (event: obj) : JS.Promise<unit> =
    promise {
        let toolName = Dyn.str event "toolName"
        if toolName <> "read" && toolName <> "write" && toolName <> "edit" then return ()
        let args = Dyn.get event "args"
        let content = Dyn.str event "content"
        let paths = extractFilePaths args
        match paths |> List.tryHead with
        | None -> ()
        | Some path ->
            let! extra = appendSyntaxDiagnostics path content false
            match extra with
            | None -> ()
            | Some diag ->
                let existing = Dyn.str event "content"
                event?content <- existing + "\n" + diag
    }
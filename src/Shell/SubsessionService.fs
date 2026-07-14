module Wanxiangshu.Shell.SubsessionService

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Shell.SubsessionActorRegistry

/// Public entry point for child-session runs.
/// Callers only use StartRun; they never touch actor state, runtime leases,
/// or FallbackRuntimeState child fields.
type SubsessionService(hostFactory: string -> ISubsessionHost) =

    /// Start (or continue) a run on the given physical child session.
    /// The actor is reused across runs on the same physical session.
    member _.StartRun
        (childSessionId: string, parentSessionId: string, prompt: string, cfg: FallbackConfig, chain: FallbackChain)
        : JS.Promise<RunResult> =
        promise {
            let host = hostFactory childSessionId
            let actor = SubsessionActorRegistry.GetOrCreate childSessionId host

            let request =
                { RunId = RunId.newId ()
                  SessionId = SessionId.create childSessionId
                  ParentSessionId = SessionId.create parentSessionId
                  Prompt = prompt
                  FallbackConfig = cfg
                  Chain = chain }

            return! actor.StartRun request
        }

    /// Post a typed fact to the actor for a child session (if one exists).
    member _.TryPost (childSessionId: string) (cmd: Command) : JS.Promise<unit> =
        match SubsessionActorRegistry.TryGet childSessionId with
        | Some actor -> actor.Post cmd
        | None -> Promise.lift ()

    /// Remove the actor when the physical child session is deleted.
    member _.RemoveSession(childSessionId: string) : unit =
        SubsessionActorRegistry.Remove childSessionId

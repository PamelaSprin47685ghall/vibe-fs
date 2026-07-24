namespace Wanxiangshu.Next.Agent

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Process

[<RequireQualifiedAccess>]
type ProgramError =
    | PermissionDenied of role: Role * permission: ToolPermission
    | InspectorAlreadyUsed
    | ExecutionError of reason: string

[<RequireQualifiedAccess>]
type ReviewVerdict =
    | Approved of summary: string
    | Rejected of reason: string
    | NeedsRevision of feedback: string list

type CommandRequest =
    { Command: Command
      Estimate: ProcessEstimate option
      Context: ProcessContext option }

module CommandRequest =
    let ofCommand (cmd: Command) : CommandRequest =
        { Command = cmd
          Estimate = None
          Context = None }

type RunnerPort = CommandRequest -> Task<Result<RunnerOutcome, RunnerError>>

type OneShotInspector =
    { Run: CommandRequest -> Task<Result<RunnerOutcome, RunnerError>>
      RunCommand: Command -> Task<Result<RunnerOutcome, RunnerError>> }

type FilePort =
    { Read: string -> Task<Result<string, string>>
      Write: string -> string -> Task<Result<unit, string>>
      Edit: string -> string -> string -> Task<Result<unit, string>> }

type SearchPort =
    { Glob: string -> Task<Result<string list, string>>
      Grep: string -> Task<Result<string list, string>> }

type BrowserPort =
    { Read: string -> Task<Result<string, string>>
      NetworkFetch: string -> Task<Result<string, string>> }

type ManagerPort =
    { Fork: string -> Task<Result<string, string>>
      Join: string -> Task<Result<unit, string>>
      List: unit -> Task<Result<string list, string>> }

type CoderCapability =
    { Read: string -> Task<Result<string, ProgramError>>
      Write: string -> string -> Task<Result<unit, ProgramError>>
      Edit: string -> string -> string -> Task<Result<unit, ProgramError>>
      CreateInspector: RunnerPort -> Result<OneShotInspector, ProgramError> }

type InspectorCapability = OneShotInspector

type BrowserCapability =
    { Read: string -> Task<Result<string, ProgramError>>
      NetworkFetch: string -> Task<Result<string, ProgramError>> }

type MeditatorCapability =
    { Read: string -> Task<Result<string, ProgramError>>
      Glob: string -> Task<Result<string list, ProgramError>>
      Grep: string -> Task<Result<string list, ProgramError>>
      CreateInspector: RunnerPort -> Result<OneShotInspector, ProgramError> }

type ReviewerCapability =
    { Read: string -> Task<Result<string, ProgramError>>
      Glob: string -> Task<Result<string list, ProgramError>>
      Grep: string -> Task<Result<string list, ProgramError>>
      CreateInspector: RunnerPort -> Result<OneShotInspector, ProgramError>
      SubmitVerdict: ReviewVerdict -> Result<ReviewVerdict, ProgramError> }

type ManagerCapability =
    { Fork: string -> Task<Result<string, ProgramError>>
      Join: string -> Task<Result<unit, ProgramError>>
      List: unit -> Task<Result<string list, ProgramError>> }

type OrchestratorCapability =
    { Fork: string -> Task<Result<string, ProgramError>>
      Join: string -> Task<Result<unit, ProgramError>> }

type ExecutorCapability = unit
type BloggerCapability = unit

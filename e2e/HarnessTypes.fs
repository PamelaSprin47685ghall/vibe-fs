module Wanxiangshu.E2e.HarnessTypes

open Fable.Core
open Fable.Core.JsInterop

type MockLLM =
    abstract expectTool: string -> obj -> unit
    abstract expectText: string -> unit
    abstract reset: unit -> unit
    abstract getRemainingExpectations: unit -> int
    abstract calls: ResizeArray<obj>

type Harness =
    abstract mockLLM: MockLLM
    abstract workDir: string
    abstract createSession: obj -> obj -> JS.Promise<obj>
    abstract sendPrompt: string -> string -> obj -> JS.Promise<obj>
    abstract getMessages: string -> obj -> JS.Promise<obj>
    abstract getSession: string -> obj -> JS.Promise<obj>
    abstract listProviders: unit -> JS.Promise<obj>
    abstract contextBudgetClient: unit -> obj
    abstract getSessions: obj -> JS.Promise<obj>
    abstract listCommands: obj -> JS.Promise<obj>
    abstract runSessionCommand: string -> string -> string -> obj -> JS.Promise<obj>
    abstract abortSession: string -> obj -> JS.Promise<obj>
    abstract readNdjson: unit -> JS.Promise<string>
    abstract waitForNdjson: int -> int -> JS.Promise<bool>
    abstract partsText: obj -> string
    abstract allMessagesText: obj -> string
    abstract waitForCalls: int -> int -> JS.Promise<int>
    abstract readFile: string -> JS.Promise<string>
    abstract fileExists: string -> JS.Promise<bool>
    abstract waitForFile: string -> int -> JS.Promise<bool>
    abstract dispose: unit -> JS.Promise<unit>

type OmpHarness =
    abstract tools: ResizeArray<obj>
    abstract handlers: obj
    abstract getToolNames: unit -> JS.Promise<ResizeArray<string>>
    abstract runCommand: string -> string -> string -> JS.Promise<obj>
    abstract triggerTool: string -> obj -> string -> obj -> JS.Promise<obj>
    abstract emitEvent: string -> obj -> string -> JS.Promise<obj>
    abstract readNdjson: unit -> JS.Promise<string>
    abstract readFile: string -> JS.Promise<string>
    abstract fileExists: string -> JS.Promise<bool>
    abstract getCommands: unit -> JS.Promise<obj>
    abstract getRemainingExpectations: unit -> int
    abstract calls: ResizeArray<obj>
    abstract expectText: string -> JS.Promise<unit>
    abstract expectTool: string -> obj -> JS.Promise<unit>
    abstract waitForNdjson: int -> int -> JS.Promise<bool>
    abstract dispose: unit -> JS.Promise<unit>

type WanxiangzhenHarness =
    abstract mode: string
    abstract tmpDir: string
    abstract token: string
    abstract url: string
    abstract runCommand: string -> string -> obj -> JS.Promise<obj>
    abstract toolRound: string -> obj -> JS.Promise<obj>
    abstract coordinatorGet: string -> string option -> JS.Promise<obj>
    abstract coordinatorPost: string -> obj -> string option -> JS.Promise<obj>
    abstract readMeta: unit -> string
    abstract waitForMeta: unit -> JS.Promise<string>
    abstract waitForScheduler: string -> JS.Promise<unit>
    abstract ensureSchedulerCapacity: unit -> JS.Promise<unit>
    abstract clearCallSpies: unit -> unit
    abstract getLog: unit -> obj[]
    abstract getSquadEvents: unit -> obj[]
    abstract getPromptCalls: unit -> obj[]
    abstract getSpawnCalls: unit -> obj[]
    abstract getKillCalls: unit -> obj[]
    abstract getWorktreeAddCalls: unit -> obj[]
    abstract getWorktreeRemoveCalls: unit -> obj[]
    abstract getBranchDeleteCalls: unit -> obj[]
    abstract callSlavePlugin: obj -> string -> string -> string -> string -> string -> JS.Promise<obj>
    abstract dispose: unit -> JS.Promise<unit>
    abstract setRevParseRef: string -> string -> unit
    abstract setMergeBaseResult: bool -> unit
    abstract setMergeFfResult: string -> unit
    abstract setStatusClean: bool -> unit
    abstract setHasCommits: bool -> unit
    abstract setShowRefExists: bool -> unit
    abstract setIsPidAlive: bool -> unit
    abstract setNowResult: string -> unit

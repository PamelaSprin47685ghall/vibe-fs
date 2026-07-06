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
    abstract expectText: string -> JS.Promise<unit>
    abstract expectTool: string -> obj -> JS.Promise<unit>
    abstract waitForNdjson: int -> int -> JS.Promise<bool>
    abstract dispose: unit -> JS.Promise<unit>

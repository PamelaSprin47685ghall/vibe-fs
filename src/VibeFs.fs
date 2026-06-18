/// Public API surface for vibe-fs — the pure kernel plus a thin effectful shell.
/// The kernel never touches the filesystem, network, or clock; the shell is the
/// only place effects live, exactly as the 宝典 prescribes: 纯函数是内核, 外壳是效果.
module VibeFs

module Boundary = VibeFs.Kernel.Boundary
module RecordValidator = VibeFs.Kernel.RecordValidator
module Prompts = VibeFs.Kernel.Prompts
module McpConfig = VibeFs.Kernel.McpConfig
module ToolPolicy = VibeFs.Kernel.ToolPolicy
module JsBoundary = VibeFs.Kernel.JsBoundary
module WorkspaceState = VibeFs.Kernel.WorkspaceState
module ReviewSession = VibeFs.Kernel.ReviewSession
module ReviewRuntime = VibeFs.Shell.ReviewRuntime
module Nudge = VibeFs.Kernel.Nudge
module FuzzyQuery = VibeFs.Kernel.FuzzyQuery
module FuzzyFormat = VibeFs.Kernel.FuzzyFormat
module FuzzyGrepDetect = VibeFs.Kernel.FuzzyGrepDetect
module Dedup = VibeFs.Kernel.Dedup
module Lru = VibeFs.Kernel.Lru
module HeadTail = VibeFs.Kernel.HeadTail
module ExecutorKernel = VibeFs.Kernel.ExecutorKernel
module CapsFormat = VibeFs.Kernel.CapsFormat
module NudgeEvents = VibeFs.Kernel.NudgeEvents
module OllamaFormat = VibeFs.Kernel.OllamaFormat
module UnifiedContext = VibeFs.Kernel.UnifiedContext
module CapsFilter = VibeFs.Shell.CapsFilter
module TreeSitterKernel = VibeFs.Kernel.TreeSitterKernel
module SessionText = VibeFs.Kernel.SessionText
module IteratorStore = VibeFs.Shell.IteratorStore

module Shell =
    module Caps = VibeFs.Shell.CapsShell
    module ReverieFiles = VibeFs.Shell.ReverieFiles
    module Executor = VibeFs.Shell.ExecutorShell
    module OllamaClient = VibeFs.Shell.OllamaClient
    module TreeSitter = VibeFs.Shell.TreeSitterShell
    module FuzzyFinder = VibeFs.Shell.FuzzyFinderShell
    module FuzzyCoordinator = VibeFs.Shell.FuzzyCoordinator
    module FuzzyRawMapping = VibeFs.Shell.FuzzyRawMapping
    module FuzzyFindCmd = VibeFs.Shell.FuzzyFindCmd
    module FuzzyGrepCmd = VibeFs.Shell.FuzzyGrepCmd

module Mux =
    module Contract = VibeFs.Mux.Contract
    module StreamEnd = VibeFs.Mux.StreamEnd
    module TodoWriteNudge = VibeFs.Mux.TodoWriteNudge
    module Dedup = VibeFs.Mux.Dedup
    module CapsFileRead = VibeFs.Mux.CapsFileRead

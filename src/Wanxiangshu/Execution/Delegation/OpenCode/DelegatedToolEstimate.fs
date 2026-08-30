namespace Wanxiangshu.Execution.Delegation.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module DelegatedToolEstimate =

    let ArgumentPath = "delegation/expected-tool-calls-argument"
    let InvalidPath = "delegation/expected-tool-calls-invalid"

    let decode (args: HostToolArguments) =
        args.OptionalNonNegativeInteger "expected_tool_calls"

    let schema language factory =
        ToolHostCodec.optionalNonNegativeIntegerSchemaDescribed
            (ProviderProse.render language ArgumentPath Map.empty)
            factory

    let invalid language =
        ProviderProse.render language InvalidPath Map.empty

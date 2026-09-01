namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

module PromptIngress =
    val resolveDecision:
        journal: AgentJournal option -> message: PromptIngressCodec.DecodedMessage -> ChatAdmissionIntent.Decision

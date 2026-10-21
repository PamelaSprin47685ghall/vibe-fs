namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Admission
open Wanxiangshu.Persistence.Journal

module PromptIngress =
    val resolveDecision:
        journal: AgentJournal option -> message: ChatAdmissionIntent.DecodedMessage -> ChatAdmissionIntent.Decision

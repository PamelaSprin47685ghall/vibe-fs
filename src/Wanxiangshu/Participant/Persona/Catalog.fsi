namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type Persona =
    | Integrator
    | Director
    | Coordinator
    | Lead
    | Coder
    | Engineer
    | Scout
    | Investigator
    | Technician
    | Operator
    | Navigator
    | Researcher
    | Analyst
    | Inquirer
    | Examiner
    | Auditor
    | Scribe
    | Chronicler
    | Condenser
    | Distiller
    | Clerk
    | Curator

[<RequireQualifiedAccess>]
module Persona =
    val render: Persona -> string
    val tryParse: string -> Persona option

[<RequireQualifiedAccess>]
module PersonaCatalog =
    val persona: Role -> AgentTier -> Persona
    val bookkeeperPersona: AgentTier -> Persona
    val personaV1: Role -> AgentTier -> string
    val bookkeeperPersonaV1: AgentTier -> string
    val inheritFrom: string -> string

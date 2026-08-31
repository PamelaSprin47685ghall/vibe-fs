namespace Foreign

let commitWithoutAdmission (effectPort: EffectPort) (witness: CurrentWitness) =
    effectPort.Commit witness

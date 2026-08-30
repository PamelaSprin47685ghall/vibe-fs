namespace Foreign

module Forge =
    let mint owner subject version = OneShotCapability.issue owner subject version

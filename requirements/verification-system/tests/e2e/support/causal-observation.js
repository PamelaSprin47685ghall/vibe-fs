const sleep = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

const observationsByScenario = new WeakMap();

const observationsFor = (scenario) => {
  let observations = observationsByScenario.get(scenario);
  if (!observations) {
    observations = new Map();
    observationsByScenario.set(scenario, observations);
  }
  return observations;
};

// The observed token must be a stage marker of a known, ordered progression. A token that is not
// one of the declared stages is not evidence about the monitored work, and an observed stage that
// is not ahead of the previous one is not progress: only a strictly forward move renews.
const stageIndex = (stages, token) => {
  const index = stages.indexOf(token);
  return index === -1 ? null : index;
};

export const observeCausalProgress = (scenario, { id, token, stages, reason, lane }) => {
  if (!id || !reason || !lane) throw new TypeError('causal observation requires id, reason, and lane');
  if (token === undefined || token === null) throw new TypeError(`causal observation '${id}' requires a token`);
  if (!Array.isArray(stages) || stages.length === 0) {
    throw new TypeError(`causal observation '${id}' requires its ordered stages`);
  }

  const observations = observationsFor(scenario);
  const previous = observations.get(id);
  if (previous !== undefined && (previous.lane !== lane || previous.stages.length !== stages.length ||
      previous.stages.some((stage, index) => stage !== stages[index]))) {
    throw new TypeError(`causal observation '${id}' changed its stage sequence or lane`);
  }
  const current = stageIndex(stages, token);
  if (current === null) throw new TypeError(`causal observation '${id}' reported an unknown stage: ${String(token)}`);
  if (previous === undefined) {
    observations.set(id, { stages: [...stages], lane, index: current });
    return false;
  }
  if (current <= previous.index) return false;

  previous.index = current;
  // A stage advance is a lifecycle transition of the monitored work, not a verdict: the
  // runtime has not concluded anything about a leaf here.
  scenario.watchdog?.advance({ reason, lane, transition: true, blocking: false });
  return true;
};

export const awaitCausalObservation = async ({
  scenario,
  id,
  reason,
  lane,
  timeoutMs,
  read,
  token,
  stages,
  ready,
  intervalMs = 50,
}) => {
  const deadline = Date.now() + timeoutMs;
  let value = await read();
  observeCausalProgress(scenario, { id, token: token(value), stages, reason, lane });

  while (!ready(value)) {
    if (Date.now() >= deadline) throw new Error(`timed out waiting for causal observation '${id}'`);
    await sleep(intervalMs);
    value = await read();
    observeCausalProgress(scenario, { id, token: token(value), stages, reason, lane });
  }

  return value;
};

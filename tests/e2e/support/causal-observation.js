const sleep = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

const tokensByScenario = new WeakMap();

const tokensFor = (scenario) => {
  let tokens = tokensByScenario.get(scenario);
  if (!tokens) {
    tokens = new Map();
    tokensByScenario.set(scenario, tokens);
  }
  return tokens;
};

export const observeCausalProgress = (scenario, { id, token, reason, lane }) => {
  if (!id || !reason || !lane) throw new TypeError('causal observation requires id, reason, and lane');
  if (token === undefined || token === null) throw new TypeError(`causal observation '${id}' requires a token`);

  const tokens = tokensFor(scenario);
  const previous = tokens.get(id);
  tokens.set(id, token);
  if (previous === undefined || previous === token) return false;

  scenario.watchdog?.advance({ reason, lane, blocking: true });
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
  ready,
  intervalMs = 50,
}) => {
  const deadline = Date.now() + timeoutMs;
  let value = await read();
  observeCausalProgress(scenario, { id, token: token(value), reason, lane });

  while (!ready(value)) {
    if (Date.now() >= deadline) throw new Error(`timed out waiting for causal observation '${id}'`);
    await sleep(intervalMs);
    value = await read();
    observeCausalProgress(scenario, { id, token: token(value), reason, lane });
  }

  return value;
};

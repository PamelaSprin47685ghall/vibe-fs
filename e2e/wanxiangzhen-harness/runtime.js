import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { BUILD_SRC } from './git.js';

export async function spinUntil(predicate, maxSteps = 200) {
  let data;
  for (let i = 0; i < maxSteps; i++) {
    const stop = await predicate(data);
    const shouldStop = typeof stop === 'object' && stop !== null ? stop.stop === true : !!stop;
    if (shouldStop) return;
    await Promise.resolve();
    data = typeof stop === 'object' && stop !== null && 'nextData' in stop ? stop.nextData : data;
  }
}

export async function runningCount(runtime) {
  const url = pathToFileURL(path.join(BUILD_SRC, 'src', 'Kernel', 'Wanxiangzhen', 'Dag.js')).href;
  const dag = await import(url);
  return dag.runningCount(runtime.Dag);
}

export async function tickScheduler(runtime, log) {
  try {
    const url = pathToFileURL(path.join(BUILD_SRC, 'src', 'Shell', 'Wanxiangzhen', 'CoordinatorOps.js')).href;
    const ops = await import(url);
    runtime.Scheduling = false;
    await ops.schedulerTick(runtime);
  } catch (e) {
    log.push(['tickSchedulerError', e.message]);
  }
}

export async function findTaskInDag(runtime, taskId) {
  const url = pathToFileURL(path.join(BUILD_SRC, 'src', 'Kernel', 'Wanxiangzhen', 'Dag.js')).href;
  const dag = await import(url);
  return dag.findTask(taskId, runtime.Dag);
}

export function extractTaskIds(events) {
  const ids = [];
  for (const evt of events || []) {
    if (evt?.type === 'tasks_created' && Array.isArray(evt.tasks)) {
      for (const t of evt.tasks) {
        if (t?.taskId) ids.push(t.taskId);
      }
    }
  }
  return ids;
}

/** Fable SquadEvent DU → `.wanxiangshu.ndjson` line object */
export function shapeWanSquadLine(evt, at) {
  let tag = evt.tag;
  if (typeof tag === 'number') {
    const mapping = {
      0: 'SquadCreated',
      1: 'TasksCreated',
      2: 'TaskStarted',
      3: 'TaskSubmitted',
      4: 'TaskMerged',
      5: 'TaskDone',
      6: 'TaskError',
      7: 'SquadCancelled',
    };
    tag = mapping[tag];
  } else if (!tag) {
    tag = evt.constructor?.name;
  }
  const fields = evt.fields || [];
  const session = fields[0] ?? '';
  const kindMap = {
    SquadCreated: 'squad_created',
    TasksCreated: 'tasks_created',
    TaskStarted: 'task_started',
    TaskSubmitted: 'task_submitted',
    TaskMerged: 'task_merged',
    TaskDone: 'task_done',
    TaskError: 'task_error',
    SquadCancelled: 'squad_cancelled',
  };
  const kind = kindMap[tag] || String(tag);
  let payload = {};
  switch (tag) {
    case 'SquadCreated':
      payload = { requirement: fields[1] };
      break;
    case 'TasksCreated': {
      const tasksList = fields[1] || [];
      const tasksArr = Array.isArray(tasksList) ? tasksList : Array.from(tasksList);
      const tasks = tasksArr.map((item) => {
        let tid, title, desc, deps;
        if (Array.isArray(item)) {
          [tid, title, desc, deps] = item;
        } else {
          tid = item.taskId || item.task_id || item.fields?.[0];
          title = item.title || item.fields?.[1];
          desc = item.description || item.fields?.[2];
          deps = item.dependsOn || item.depends_on || item.fields?.[3];
        }
        const depsArr = deps && !Array.isArray(deps) ? Array.from(deps) : (deps || []);
        return {
          task_id: tid,
          title,
          description: desc,
          ...(depsArr.length ? { depends_on: depsArr } : {}),
        };
      });
      payload = { tasksJson: JSON.stringify(tasks) };
      break;
    }
    case 'TaskStarted':
      payload = { task_id: fields[1], worktree_path: fields[2], branch_name: fields[3] };
      break;
    case 'TaskSubmitted':
      payload = { task_id: fields[1], commit_sha: fields[2] };
      break;
    case 'TaskMerged':
      payload = { task_id: fields[1], master_sha: fields[2] };
      break;
    case 'TaskDone':
      payload = { task_id: fields[1], merged: String(fields[2]) };
      break;
    case 'TaskError':
      payload = { task_id: fields[1], error: fields[2] };
      break;
    default:
      break;
  }
  return { v: 1, session, kind, at, payload };
}
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '..');
function runtime(storage = new Map()) {
  const window = {};
  const context = vm.createContext({ window, TextEncoder, sessionStorage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value) } });
  for (const file of ['rule-execution.js', 'collection-store.js']) vm.runInContext(fs.readFileSync(path.join(root, file), 'utf8'), context);
  return { engine: window.RuleExecution, store: window.CollectionStore, storage };
}
const variables = { factor: { name: '系数', kind: 'Decimal' }, output: { name: '结果', kind: 'Decimal', output: true } };
const rule = (id, priority, assignments, condition = () => true) => ({ id, name: id, enabled: true, priority, order: 1, assignments, condition });
const write = (target, value) => ({ target, value });
const formula = (target, compute) => ({ target, compute });
function run(rules, executionMode = 'Sequential', multipleWritePolicy = 'LastWriteWins', extra = {}) {
  return runtime().engine.execute({ revision: { rules, executionMode, multipleWritePolicy }, variables,
    scene: { resultCardinality: executionMode === 'CollectMatches' ? 'Many' : 'Single' }, initial: { factor: 1 },
    match: (rule, values) => rule.condition(values), evaluate: (assignment, values) => assignment.compute ? assignment.compute(values) : assignment.value, ...extra });
}
const dependent = () => [rule('base', 100, [write('factor', 2), write('output', 20)]), rule('next', 80, [formula('output', values => values.factor * 10 + 5)], values => values.factor === 2)];

test('FirstMatch 按优先级和顺序停止，后续条件和公式均不执行', () => {
  const rules = dependent();
  rules[1].condition = () => { throw new Error('不应计算后续条件'); };
  const result = run(rules.reverse(), 'FirstMatch');
  assert.equal(result.error, undefined); assert.equal(result.results[0].outputs.output, 20);
  assert.equal(result.results[0].sourceRuleIds.join(','), 'base');
  assert.match(result.trace[1].status, /未执行/);
  const samePriority = [rule('later', 100, [write('output', 2)]), rule('earlier', 100, [write('output', 1)])];
  samePriority[0].order = 2;
  assert.equal(run(samePriority, 'FirstMatch').results[0].outputs.output, 1);
});
test('Sequential 后续条件和表达式读取前序赋值，结果合成一行', () => {
  const result = run(dependent());
  assert.equal(result.error, undefined); assert.equal(result.results.length, 1);
  assert.equal(result.results[0].outputs.output, 25); assert.equal(result.results[0].sourceRuleIds.join(','), 'base,next');
  assert.equal(result.trace[1].assignments[0].previous, 20);
});
test('FirstWriteWins 保留初值写入，跳过被忽略的公式', () => {
  const rules = dependent(); rules[1].assignments[0].compute = () => { throw new Error('被忽略的表达式不能计算'); };
  const result = run(rules, 'Sequential', 'FirstWriteWins');
  assert.equal(result.error, undefined); assert.equal(result.results[0].outputs.output, 20);
  assert.equal(result.trace[1].assignments[0].action, '忽略后续赋值');
});
test('ErrorOnMultipleWrite 静态拒绝多条启用规则的重复目标，即使条件互斥', () => {
  const rules = dependent(); rules[1].condition = () => false;
  const result = run(rules, 'Sequential', 'ErrorOnMultipleWrite');
  assert.match(result.error, /可能重复赋值/); assert.equal(result.results.length, 0); assert.equal(result.trace.length, 0);
});
test('停用的规则不参与跨规则写入冲突检查或计算', () => {
  const rules = dependent(); rules[1].enabled = false;
  assert.equal(run(rules, 'Sequential', 'ErrorOnMultipleWrite').results[0].outputs.output, 20);
});
test('Sequential 可由多条规则共同填齐结果，其他模式要求每条规则结果完整', () => {
  const rules = [rule('factor-only', 100, [write('factor', 2)]), rule('output-only', 80, [formula('output', values => values.factor * 10)])];
  assert.equal(run(rules, 'Sequential', 'ErrorOnMultipleWrite').results[0].outputs.output, 20);
  assert.match(run(rules, 'FirstMatch').error, /缺少必需结果/);
  assert.match(run(rules, 'CollectMatches', 'ErrorOnMultipleWrite').error, /缺少必需结果/);
});
test('CollectMatches 独立重置上下文，跨行不覆盖，保留完整行关系', () => {
  const rules = [rule('one', 100, [write('factor', 2), write('output', 20)]), rule('two', 80, [formula('output', values => values.factor * 10)])];
  const initial = { factor: 1 };
  const result = run(rules, 'CollectMatches', 'ErrorOnMultipleWrite', { initial });
  assert.equal(result.error, undefined); assert.equal(result.results.map(row => row.outputs.output).join(','), '20,10');
  assert.equal(result.results.map(row => row.sourceRuleIds.join(',')).join(';'), 'one;two');
  assert.equal(initial.factor, 1);
});
test('同一规则重复目标不被 First/Last 策略放行', () => {
  for (const policy of ['FirstWriteWins', 'LastWriteWins']) assert.match(run([rule('duplicate', 1, [write('output', 1), write('output', 2)])], 'Sequential', policy).error, /同一条规则不能/);
});
test('后续规则计算失败时丢弃已有结果行', () => {
  const rules = [rule('one', 100, [write('output', 1)]), rule('broken', 80, [formula('output', () => { throw new Error('除零'); })])];
  const result = run(rules, 'CollectMatches', 'ErrorOnMultipleWrite');
  assert.match(result.error, /除零/); assert.equal(result.results.length, 0); assert.equal(result.trace[1].status, '计算失败');
});
test('无命中：多行返回空集合，单行无法生成结果时报错', () => {
  const rules = [rule('no-match', 1, [write('output', 1)], () => false)];
  assert.equal(run(rules, 'CollectMatches', 'ErrorOnMultipleWrite').results.length, 0);
  assert.equal(run(rules, 'CollectMatches', 'ErrorOnMultipleWrite').error, undefined);
  assert.match(run(rules, 'FirstMatch').error, /没有规则命中/);
});
test('Sequential 实际命中路径缺少结果时失败，不能只看所有规则的赋值并集', () => {
  const rules = [rule('factor', 100, [write('factor', 2)]), rule('output', 80, [write('output', 20)], () => false)];
  assert.match(run(rules).error, /缺少完整结果/);
});
test('多行的行数限制与无效值失败均不返回部分结果', () => {
  const rules = Array.from({ length: 101 }, (_, index) => rule('rule-' + index, 1000 - index, [write('output', index)]));
  assert.match(run(rules, 'CollectMatches', 'ErrorOnMultipleWrite').error, /100 行/);
  assert.equal(run(rules, 'CollectMatches', 'ErrorOnMultipleWrite').results.length, 0);
  assert.match(run([rule('nan', 1, [write('output', NaN)])]).error, /未得到有效值/);
});
test('设置随草稿保存并使门禁待校验；线上、其他规则集隔离', () => {
  const { store } = runtime(), original = JSON.stringify(store.get('expansion').published), other = JSON.stringify(store.get('line'));
  store.saveDraft('expansion', { executionMode: 'Sequential', multipleWritePolicy: 'FirstWriteWins' });
  assert.equal(store.get('expansion').draft.executionMode, 'Sequential'); assert.equal(store.get('expansion').draft.gateStatus, 'pending');
  assert.equal(JSON.stringify(store.get('expansion').published), original); assert.equal(JSON.stringify(store.get('line')), other);
});
test('新建、复制、从线上建草稿与会话刷新保留正确执行设置', () => {
  const { store, storage } = runtime();
  assert.equal(store.create('rule', { scene: 'matched-results', name: 'new', code: 'NEW' }).draft.executionMode, 'CollectMatches');
  const copied = store.copy('sequential-width', { name: 'copy', code: 'COPY' }, 'draft');
  assert.equal(copied.draft.executionMode, 'Sequential'); assert.equal(copied.draft.multipleWritePolicy, 'LastWriteWins');
  assert.equal(store.copy('sequential-width', { name: 'live', code: 'LIVE' }, 'published').draft.executionMode, 'FirstMatch');
  assert.equal(store.createDraft('line').executionMode, 'FirstMatch');
  assert.equal(runtime(storage).store.get(copied.id).draft.executionMode, 'Sequential');
});
test('无效枚举、基数不匹配、多行覆盖策略、归档及无草稿的写入都被拒绝', () => {
  const { store } = runtime();
  for (const changes of [{ executionMode: 'CollectMatches' }, { executionMode: 'bad' }, { executionMode: 'toString' }, { multipleWritePolicy: 'bad' }, { multipleWritePolicy: 'constructor' }]) assert.throws(() => store.saveDraft('expansion', changes));
  assert.throws(() => store.saveDraft('matched-sizes', { executionMode: 'Sequential' }));
  assert.throws(() => store.saveDraft('matched-sizes', { multipleWritePolicy: 'LastWriteWins' }));
  assert.throws(() => store.saveDraft('line', { executionMode: 'Sequential' }));
  store.archive('expansion'); assert.throws(() => store.saveDraft('expansion', { executionMode: 'Sequential' }));
});

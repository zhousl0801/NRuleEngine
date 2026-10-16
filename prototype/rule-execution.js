(() => {
  'use strict';
  const modes = {
    FirstMatch: { name: '首条命中', description: '按优先级从高到低、顺序从小到大判断，第一条命中后停止，返回一行完整结果。' },
    Sequential: { name: '顺序计算', description: '依次判断全部启用规则。赋值立即写回共享上下文，后续条件和公式可读取；所有命中规则共同生成一行结果。' },
    CollectMatches: { name: '全部命中分别计算', description: '每条规则从相同输入和默认值独立计算，每条命中规则生成一行完整结果，不读取其他规则的赋值。' }
  };
  const policies = {
    ErrorOnMultipleWrite: { name: '重复赋值时报错', description: '同一变量存在多条启用规则的赋值时，校验不通过；请拆分赋值目标或停用冲突规则。' },
    FirstWriteWins: { name: '保留首次赋值', description: '变量第一次写入后，后续对它的赋值直接忽略，不计算被忽略的公式。' },
    LastWriteWins: { name: '后续赋值覆盖', description: '按执行顺序写入，后续赋值覆盖先前值；公式读取覆盖前的当前上下文。' }
  };
  function defaults(scene) {
    return { executionMode: scene?.resultCardinality === 'Many' ? 'CollectMatches' : 'FirstMatch', multipleWritePolicy: 'ErrorOnMultipleWrite' };
  }
  function checkSettings(scene, settings) {
    if (!Object.hasOwn(modes, settings.executionMode) || !Object.hasOwn(policies, settings.multipleWritePolicy)) throw new Error('请选择有效的执行方式和重复赋值策略。');
    if ((scene?.resultCardinality === 'Many') !== (settings.executionMode === 'CollectMatches')) throw new Error('执行方式必须符合场景的单行或多行结果要求。');
    if (settings.executionMode === 'CollectMatches' && settings.multipleWritePolicy !== 'ErrorOnMultipleWrite') throw new Error('多行结果独立计算，不使用跨规则覆盖策略。');
  }
  function validate(revision, variables) {
    const issues = [], enabled = revision.rules.filter(rule => rule.enabled);
    const outputs = Object.keys(variables).filter(code => variables[code].output);
    if (!enabled.length) issues.push('至少需要一条已启用的规则。');
    for (const rule of revision.rules) {
      const targets = rule.assignments.map(assignment => assignment.target);
      if (!targets.length) issues.push(`${rule.name}：没有赋值。`);
      if (new Set(targets).size !== targets.length) issues.push(`${rule.name}：同一条规则不能重复赋值同一变量，重复赋值策略只处理多条规则之间的写入。`);
      if (targets.some(target => !variables[target] || variables[target].input)) issues.push(`${rule.name}：赋值目标不存在或是输入变量。`);
      if (rule.enabled && revision.executionMode !== 'Sequential') {
        const missing = outputs.filter(code => !targets.includes(code));
        if (missing.length) issues.push(`${rule.name}：缺少必需结果 ${missing.map(code => variables[code].name).join('、')}。`);
      }
    }
    if (revision.executionMode === 'Sequential') {
      const allTargets = enabled.flatMap(rule => rule.assignments.map(assignment => assignment.target));
      const missing = outputs.filter(code => !allTargets.includes(code));
      if (missing.length) issues.push(`启用规则无法共同生成必需结果 ${missing.map(code => variables[code].name).join('、')}。`);
      if (revision.multipleWritePolicy === 'ErrorOnMultipleWrite') {
        const repeated = [...new Set(allTargets.filter((code, index) => allTargets.indexOf(code) !== index))];
        if (repeated.length) issues.push(`多条启用规则可能重复赋值：${repeated.map(code => variables[code].name).join('、')}。当前策略会拒绝试算，即使条件可能互斥。`);
      }
    }
    return issues;
  }
  function execute({ revision, scene, variables, initial, match, evaluate }) {
    const trace = [], results = [];
    try {
      checkSettings(scene, revision);
      const issues = validate(revision, variables);
      if (issues.length) throw new Error(issues.join('\n'));
      const ordered = revision.rules.filter(rule => rule.enabled).sort((a, b) => Number(b.priority) - Number(a.priority) || Number(a.order) - Number(b.order));
      const many = revision.executionMode === 'CollectMatches';
      let values = { ...initial }, written = new Set();
      const sources = [];
      const append = ids => {
        const outputs = {};
        for (const [code, target] of Object.entries(variables).filter(([, target]) => target.output)) {
          if (values[code] === undefined || values[code] === null) throw new Error(`命中路径缺少完整结果：${target.name}`);
          outputs[code] = values[code];
        }
        results.push({ sourceRuleIds: [...ids], outputs });
        if (results.length > 100 || new TextEncoder().encode(JSON.stringify(results)).length > 65536) throw new Error('结果超过 100 行或 65536 字节限制。');
      };
      for (const rule of ordered) {
        const step = { ruleId: rule.id, name: rule.name, priority: rule.priority, order: rule.order, status: '判断中', assignments: [] };
        trace.push(step);
        try {
          if (many) { values = { ...initial }; written = new Set(); }
          if (!match(rule, values)) { step.status = '未命中'; continue; }
          step.status = '命中';
          sources.push(rule.id);
          for (const assignment of rule.assignments) {
            const previous = values[assignment.target], repeated = written.has(assignment.target);
            if (repeated && revision.multipleWritePolicy === 'ErrorOnMultipleWrite') throw new Error(`${variables[assignment.target].name} 被重复赋值。`);
            if (repeated && revision.multipleWritePolicy === 'FirstWriteWins') {
              step.assignments.push({ target: assignment.target, action: '忽略后续赋值', value: previous });
              continue;
            }
            const value = evaluate(assignment, values);
            const target = variables[assignment.target];
            if (value === undefined || value === null || target.kind === 'Decimal' && !target.shape && (typeof value !== 'number' || !Number.isFinite(value))) throw new Error(`${target.name} 未得到有效值。`);
            values[assignment.target] = value;
            written.add(assignment.target);
            step.assignments.push({ target: assignment.target, action: repeated ? '覆盖先前赋值' : '写入', ...(repeated ? { previous } : {}), value });
          }
          if (many) append([rule.id]);
          if (revision.executionMode === 'FirstMatch') {
            for (const skipped of ordered.slice(trace.length)) trace.push({ ruleId: skipped.id, name: skipped.name, priority: skipped.priority, order: skipped.order, status: '首条已命中，未执行', assignments: [] });
            break;
          }
        } catch (error) { step.status = '计算失败'; throw new Error(`${rule.name}：${error.message}`); }
      }
      if (!sources.length && !many) throw new Error('没有规则命中，无法生成单行结果。');
      if (!many) append(sources);
      return { results, trace };
    } catch (error) {
      // 任意规则失败时丢弃已计算的行，过程仅供定位，不代表成功结果。
      return { error: error.message, results: [], trace };
    }
  }
  window.RuleExecution = { modes, policies, defaults, checkSettings, validate, execute };
})();

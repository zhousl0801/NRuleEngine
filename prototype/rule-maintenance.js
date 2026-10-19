(() => {
  const variableCatalog = {
    inputWidth: { name: '入口宽度', kind: 'Decimal', key: '11111111-1111-1111-1111-111111111111', input: true },
    productKind: { name: '产品类别', kind: 'String', key: '22222222-2222-2222-2222-222222222222', input: true },
    factor: { name: '展开系数', kind: 'Decimal', key: '33333333-3333-3333-3333-333333333333' },
    offset: { name: '偏移量', kind: 'Decimal', key: '44444444-4444-4444-4444-444444444444' },
    outputWidth: { name: '展开宽度', kind: 'Decimal', key: '55555555-5555-5555-5555-555555555555', output: true },
    widthRange: { name: '宽度范围', kind: 'Decimal', shape: 'NumericRange', key: '77777777-7777-7777-7777-777777777777', output: true },
    candidateWidths: { name: '候选宽度', kind: 'Decimal', shape: 'List', key: '88888888-8888-8888-8888-888888888888', output: true },
    candidateCodes: { name: '候选编码', kind: 'String', shape: 'List', key: '99999999-9999-9999-9999-999999999999', output: true },
    calcStatus: { name: '计算状态', kind: 'String', key: '66666666-6666-6666-6666-666666666666', output: true }
  };
  const opLabels = { eq: '=', ne: '≠', gt: '>', gte: '≥', lt: '<', lte: '≤' };
  const arithmeticLabels = { add: '+', sub: '−', mul: '×', div: '÷' };
  let nextId = Date.now();
  const condition = (field, op, value) => ({ kind: 'condition', id: `c${nextId++}`, field, op, value });
  const group = (op, children = []) => ({ kind: 'group', id: `g${nextId++}`, op, children });
  const variable = name => ({ kind: 'variable', variable: name });
  const binary = (op, left, right) => ({ kind: 'binary', op, left, right });
  const defaultExpression = () => binary('mul', variable('inputWidth'), variable('factor'));
  const params = new URLSearchParams(location.search);
  const collectionStore = window.CollectionStore;
  const execution = window.RuleExecution;
  let collection;
  try {
    collection = collectionStore.get(params.get('collection') || 'expansion');
    if (collection.kind !== 'rule') throw new Error('请选择规则集。');
  } catch {
    document.querySelector('.page').innerHTML = '<h1>找不到这个规则集</h1><p><a href="index.html#model-list">返回规则集列表</a></p>';
    return;
  }
  const requestedMode = params.get('revision');
  const initialMode = ['draft', 'published'].includes(requestedMode) && collection[requestedMode] ? requestedMode : collection.draft ? 'draft' : 'published';
  const rules = collectionStore.clone(collection[initialMode].rules);
  const initialIndex = Math.max(0, Math.min(rules.length - 1, Number(params.get('rule')) || 0));
  const state = { rules, selectedId: rules.find(rule => rule.id === params.get('ruleId'))?.id || rules[initialIndex]?.id || null, stage: 'definition', revision: initialMode, stamp: 42, conflict: false, pendingDelete: null };
  const $ = selector => document.querySelector(selector);
  const $$ = selector => [...document.querySelectorAll(selector)];
  const selected = () => state.rules.find(rule => rule.id === state.selectedId);
  const editable = () => state.revision === 'draft' && !collection.archived;
  const html = value => String(value ?? '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);

  function revisionLabel() { return `${state.revision === 'draft' ? '草稿' : collection.archived ? '已发布' : '线上'} R${String(collection[state.revision].no).padStart(2, '0')}`; }
  function updateContext() {
    document.title = `${collection.name} · 规则维护`;
    const semantics = collection[state.revision];
    $('#execution-summary').textContent = execution.modes[semantics.executionMode].name + '：' + execution.modes[semantics.executionMode].description + (semantics.executionMode === 'Sequential' ? ` 重复赋值：${execution.policies[semantics.multipleWritePolicy].name}。` : '');
    $('#execution-settings-link').href = `index.html#models?${new URLSearchParams({ set:collection.id, revision:state.revision, listScene:params.get('listScene') || 'all', tab:'execution' })}`;
    $('.page-head h1').textContent = `${collection.name} · 维护规则`;
    $('.page-head > div > p').textContent = `${collection.code} · ${revisionLabel()}${collection.archived ? ' · 已归档，只读' : ''}`;
    $('.back').href = `index.html#models?${new URLSearchParams({ set:collection.id, revision:state.revision, listScene:params.get('listScene') || 'all' })}`;
    $$('.collection-return').forEach(link => { link.href = $('.back').href; });
    $('#revision-mode').innerHTML = ['draft','published'].filter(mode => collection[mode]).map(mode => `<option value="${mode}">${mode === 'draft' ? '草稿' : '线上'} R${String(collection[mode].no).padStart(2,'0')} · ${mode === 'draft' && !collection.archived ? '可编辑' : '只读'}</option>`).join('');
    $('#revision-mode').value = state.revision;
  }
  function persistChange(kind, rule, assignment) {
    if (!editable()) return;
    const saved = collectionStore.clone(collectionStore.get(collection.id).draft.rules);
    const index = saved.findIndex(item => item.id === rule.id);
    if (kind === 'rule') {
      const definition = { ...collectionStore.clone(rule), assignments:index >= 0 ? saved[index].assignments : [] };
      if (index >= 0) saved[index] = definition; else saved.push(definition);
    }
    if (kind === 'assignment' && index >= 0) {
      const position = saved[index].assignments.findIndex(item => item.id === assignment.id);
      if (position >= 0) saved[index].assignments[position] = collectionStore.clone(assignment);
      else saved[index].assignments.push(collectionStore.clone(assignment));
    }
    if (kind === 'delete-rule' && index >= 0) saved.splice(index,1);
    if (kind === 'delete-assignment' && index >= 0) saved[index].assignments = saved[index].assignments.filter(item => item.id !== assignment.id);
    collectionStore.saveDraft(collection.id,{ rules:saved });
    collection = collectionStore.get(collection.id);
  }
  function newRule() {
    return { id: `r${nextId++}`, name: '', priority: 50, order: Math.max(0, ...state.rules.map(rule => Number(rule.order) || 0)) + 1, enabled: true, conditionTree: group('and', [condition('inputWidth', 'gte', '')]), assignments: [], saved: false, dirty: true };
  }
  function newAssignment() {
    const used = new Set(selected()?.assignments.map(item => item.target));
    const target = ['outputWidth', 'calcStatus', 'widthRange', 'candidateWidths', 'candidateCodes', 'factor', 'offset'].find(item => !used.has(item)) || 'outputWidth';
    return { id: `a${nextId++}`, target, kind: 'constant', constant: '', expression: defaultExpression(), round: '', saved: false, dirty: true };
  }
  function currentDirty() {
    const rule = selected();
    return !!rule && (rule.dirty || rule.assignments.some(assignment => assignment.dirty));
  }
  function allDirty() { return state.rules.some(rule => rule.dirty || rule.assignments.some(assignment => assignment.dirty)); }
  function showMessage(id, text, type = '') {
    const node = $(`#${id}`);
    node.textContent = text;
    node.className = `message ${type}`.trim();
    node.hidden = !text;
    node.setAttribute('role', type === 'error' ? 'alert' : 'status');
    if (text && type === 'error') node.scrollIntoView({ block:'nearest' });
  }
  function toast(text) { window.PrototypeUI.toast(text); }
  function option(value, label, active) { return `<option value="${html(value)}"${value === active ? ' selected' : ''}>${html(label)}</option>`; }
  function variableOptions(active, filter = () => true) { return Object.entries(variableCatalog).filter(([key, variable]) => filter(variable, key)).map(([key, variable]) => option(key, variable.name, active)).join(''); }
  function kindName(kind) { return ({ Decimal: '数值', String: '文本', Boolean: '布尔' })[kind] || kind; }
  function opOptions(active, kind) { return Object.entries(opLabels).filter(([key]) => kind === 'Decimal' || ['eq', 'ne'].includes(key)).map(([key, label]) => option(key, label, active)).join(''); }
  function findConditionNode(node, id, parent = null) {
    if (node.id === id) return { node, parent };
    if (node.kind === 'group') for (const child of node.children) {
      const found = findConditionNode(child, id, node);
      if (found) return found;
    }
    return null;
  }
  function conditionNodeAst(node) {
    if (node.kind === 'condition') {
      const meta = variableCatalog[node.field];
      return { op: node.op, args: [{ op: 'variable', stableKey: meta.key }, { op: 'constant', kind: meta.kind, value: node.value === '' ? null : meta.kind === 'Decimal' ? Number(node.value) : node.value }] };
    }
    const children = node.children.map(conditionNodeAst);
    return children.length ? children.slice(1).reduce((left, right) => ({ op: node.op, args: [left, right] }), children[0]) : { op: 'constant', kind: 'Boolean', value: true };
  }
  function conditionAst(rule) {
    return { version: 1, root: conditionNodeAst(rule.conditionTree) };
  }
  function conditionNodeSource(node, nested = false) {
    if (node.kind === 'condition') return `${variableCatalog[node.field].name} ${opLabels[node.op]} ${node.value || '…'}`;
    if (!node.children.length) return '始终满足（布尔常量 true）';
    const source = node.children.map(child => conditionNodeSource(child, true)).join(node.op === 'and' ? '  AND  ' : '  OR  ');
    return nested && node.children.length > 1 ? `(${source})` : source;
  }
  function conditionSource(rule) {
    return conditionNodeSource(rule.conditionTree);
  }
  function expressionNodeAst(node) {
    if (node.kind === 'variable') return { op: 'variable', stableKey: variableCatalog[node.variable].key };
    if (node.kind === 'constant') return { op: 'constant', kind: 'Decimal', value: node.value === '' ? null : Number(node.value) };
    return { op: node.op, args: [expressionNodeAst(node.left), expressionNodeAst(node.right)] };
  }
  function expressionAst(assignment) {
    const base = expressionNodeAst(assignment.expression);
    const root = assignment.round === '' ? base : { op: 'function', function: 'Round', args: [base, { op: 'constant', kind: 'Decimal', value: Number(assignment.round) }] };
    return { version: 1, root };
  }
  function expressionNodeSource(node, nested = false) {
    if (node.kind === 'variable') return variableCatalog[node.variable].name;
    if (node.kind === 'constant') return node.value || '…';
    const source = `${expressionNodeSource(node.left, true)} ${arithmeticLabels[node.op]} ${expressionNodeSource(node.right, true)}`;
    return nested ? `(${source})` : source;
  }
  function parseFormula(source) {
    if (!source.trim()) throw new Error('请输入计算公式。');
    if (source.length > 500) throw new Error('公式最多 500 个字符，请拆分为多条赋值。');
    const names = Object.entries(variableCatalog)
      .filter(([, meta]) => meta.kind === 'Decimal' && !meta.shape)
      .sort((left, right) => right[1].name.length - left[1].name.length);
    const tokens = [];
    for (let index = 0; index < source.length;) {
      const char = source[index];
      if (/\s/.test(char)) { index += 1; continue; }
      const named = names.find(([, meta]) => source.startsWith(meta.name, index));
      if (named) { tokens.push({ kind: 'variable', value: named[0], position: index }); index += named[1].name.length; continue; }
      const number = source.slice(index).match(/^(?:\d+(?:\.\d*)?|\.\d+)/);
      if (number) {
        if (!Number.isFinite(Number(number[0]))) throw new Error(`第 ${index + 1} 位的数字超出范围。`);
        tokens.push({ kind: 'number', value: number[0], position: index }); index += number[0].length; continue;
      }
      const normalized = { '×': '*', '÷': '/', '−': '-', '＋': '+', '－': '-', '＊': '*', '／': '/', '（': '(', '）': ')' }[char] || char;
      if ('+-*/()'.includes(normalized)) { tokens.push({ kind: normalized, position: index }); index += 1; continue; }
      throw new Error(`第 ${index + 1} 位不是可用的变量、数字或运算符。`);
    }
    if (tokens.length > 200) throw new Error('公式过长，请拆分为多条赋值。');
    let cursor = 0;
    const current = () => tokens[cursor];
    const take = kind => current()?.kind === kind ? tokens[cursor++] : null;
    function primary(depth) {
      if (depth > 32) throw new Error('括号层级过多，请简化公式。');
      if (take('+')) return primary(depth + 1);
      if (take('-')) return binary('sub', { kind: 'constant', value: '0' }, primary(depth + 1));
      if (take('(')) {
        const node = addition(depth + 1);
        if (!take(')')) throw new Error('缺少右括号 )。');
        return node;
      }
      const token = current();
      if (!token) throw new Error('公式末尾缺少变量或数字。');
      if (token.kind === 'variable') { cursor += 1; return variable(token.value); }
      if (token.kind === 'number') { cursor += 1; return { kind: 'constant', value: token.value }; }
      throw new Error(`第 ${token.position + 1} 位需要变量、数字或左括号。`);
    }
    function multiplication(depth) {
      let node = primary(depth);
      while (current() && ['*', '/'].includes(current().kind)) {
        const operator = tokens[cursor++].kind;
        node = binary(operator === '*' ? 'mul' : 'div', node, primary(depth));
      }
      return node;
    }
    function addition(depth) {
      let node = multiplication(depth);
      while (current() && ['+', '-'].includes(current().kind)) {
        const operator = tokens[cursor++].kind;
        node = binary(operator === '+' ? 'add' : 'sub', node, multiplication(depth));
      }
      return node;
    }
    const root = addition(0);
    if (current()) throw new Error(`第 ${current().position + 1} 位附近缺少运算符或有多余的右括号。`);
    return root;
  }
  function updateFormula(assignment, source) {
    assignment.formulaDraft = source;
    try {
      assignment.expression = parseFormula(source);
      assignment.formulaError = '';
    } catch (error) {
      assignment.formulaError = error.message;
    }
  }

  function resultParts(assignment) {
    if (!assignment.parts) assignment.parts = Array.from({ length: variableCatalog[assignment.target].shape === 'NumericRange' ? 2 : 1 }, () => ({ source: 'constant', value: '' }));
    return assignment.parts;
  }
  function scalarResult(part, target) {
    if (part.source !== 'constant') throw new Error('此结果需要计算。');
    if (!part.value.trim()) throw new Error('结果值不能为空。');
    if (target.kind === 'Decimal') {
      if (!/^[+-]?(?:\d+(?:\.\d*)?|\.\d+)$/.test(part.value.trim()) || !Number.isFinite(Number(part.value))) throw new Error('请输入有效数值。');
      return Number(part.value);
    }
    return part.value;
  }
  function resultNode(part, target) {
    if (part.source === 'constant') return { op: 'constant', kind: target.kind, value: scalarResult(part, target) };
    if (part.source === 'variable') {
      const meta = variableCatalog[part.value];
      if (!meta || meta.kind !== target.kind || meta.shape) throw new Error('请选择同类型单值变量。');
      return { op: 'variable', stableKey: meta.key };
    }
    if (target.kind !== 'Decimal') throw new Error('公式只能生成数值。');
    return expressionNodeAst(parseFormula(part.value));
  }
  function resultAst(assignment) {
    const target = variableCatalog[assignment.target], parts = resultParts(assignment);
    if (target.shape === 'NumericRange' && parts.length !== 2) throw new Error('填写最小值和最大值。');
    if (target.shape === 'List' && (parts.length < 1 || parts.length > 100)) throw new Error('候选列表需要 1–100 项。');
    return { version: 1, root: { op: target.shape === 'NumericRange' ? 'range' : 'list', args: parts.map(part => resultNode(part, target)) } };
  }
  function validateResult(target, items) {
    if (target.kind === 'Decimal' && items.some(value => !Number.isFinite(value))) throw new Error('结果包含无效数值。');
    if (items.some(value => value == null || (typeof value === 'string' && !value.trim()))) throw new Error('结果项不能为空。');
    if (target.shape === 'NumericRange') {
      if (items[0] > items[1]) throw new Error('最小值不能大于最大值。');
      return { min: items[0], max: items[1] };
    }
    if (items.length < 1 || items.length > 100) throw new Error('候选列表需要 1–100 项。');
    if (new Set(items).size !== items.length) throw new Error('候选值重复，请修改或删除重复项。');
    return items;
  }
  function constantResult(assignment) {
    const target = variableCatalog[assignment.target];
    return validateResult(target, resultParts(assignment).map(part => scalarResult(part, target)));
  }
  function evaluateResult(assignment, values) {
    const target = variableCatalog[assignment.target];
    const items = resultParts(assignment).map(part => part.source === 'constant' ? scalarResult(part, target)
      : part.source === 'variable' ? values[part.value] : evaluateExpression(parseFormula(part.value), values));
    return validateResult(target, items);
  }
  function resultPreview(assignment) {
    const target = variableCatalog[assignment.target], parts = resultParts(assignment);
    const values = parts.map(part => part.source === 'variable' ? variableCatalog[part.value]?.name || '选择变量' : part.value || '待填写');
    return target.shape === 'NumericRange' ? `[${values[0]}, ${values[1]}]` : `[${values.join('，')}]`;
  }
  function resultShapeName(target) {
    return target.shape === 'NumericRange' ? '数值区间' : target.shape === 'List' ? `${kindName(target.kind)}候选列表` : kindName(target.kind);
  }
  function renderResultEditor(assignment, target) {
    const parts = resultParts(assignment), range = target.shape === 'NumericRange';
    const hint = range ? '包含两端；最小值 ≤ 最大值。每个端点可独立设置常量或公式。'
      : '按维护顺序返回 1–100 个同类型候选值，不允许空项或重复值。';
    const rows = parts.map((part, index) => {
      const label = range ? (index === 0 ? '最小值' : '最大值') : `候选 ${index + 1}`;
      const sourceOptions = option('constant', '常量', part.source) + (target.kind === 'Decimal' ? option('expression', '公式', part.source) : option('variable', '变量', part.source));
      const field = part.source === 'variable'
        ? `<select data-part-field="value" aria-label="${label}变量">${option('', '选择变量', part.value)}${variableOptions(part.value, meta => meta.kind === target.kind && !meta.shape)}</select>`
        : `<input data-part-field="value" aria-label="${label}${part.source === 'expression' ? '公式' : '常量'}" value="${html(part.value)}" placeholder="${part.source === 'expression' ? '例如：入口宽度 + 10' : '填写' + kindName(target.kind) + '值'}">`;
      return `<div class="result-part" data-part-index="${index}"><strong>${label}</strong><select data-part-field="source" aria-label="${label}来源">${sourceOptions}</select>${field}${range ? '' : `<div class="candidate-actions"><button type="button" data-part-action="up" aria-label="上移候选 ${index + 1}" ${index === 0 ? 'disabled data-limit-disabled' : ''}>↑</button><button type="button" data-part-action="down" aria-label="下移候选 ${index + 1}" ${index === parts.length - 1 ? 'disabled data-limit-disabled' : ''}>↓</button><button type="button" data-part-action="remove" aria-label="删除候选 ${index + 1}" ${parts.length === 1 ? 'disabled data-limit-disabled' : ''}>×</button></div>`}</div>`;
    }).join('');
    return `<div class="result-editor"><p class="result-hint">${hint}</p><div class="result-parts">${rows}</div>${range ? '' : `<button type="button" class="btn subtle small" data-add-candidate ${parts.length >= 100 ? 'disabled data-limit-disabled' : ''}>＋ 添加候选值</button>`}<p class="formula-help">${target.kind === 'Decimal' ? '公式可引用入口宽度、展开系数、偏移量等单值变量；精度与最终结果以服务端校验为准。' : '变量候选可引用同类型单值；最终结果以服务端校验为准。'}</p></div>`;
  }
  function assignmentSource(assignment) {
    if (variableCatalog[assignment.target].shape) return `${variableCatalog[assignment.target].name} ← ${resultPreview(assignment)}`;

    if (assignment.kind === 'constant') return `${variableCatalog[assignment.target].name} ← ${JSON.stringify(assignment.constant)}`;
    if (assignment.formulaError) return `${variableCatalog[assignment.target].name} ← 公式尚未完成`;
    const formula = (assignment.formulaDraft ?? expressionNodeSource(assignment.expression)).trim();
    return `${variableCatalog[assignment.target].name} ← ${assignment.round === '' ? formula : `Round(${formula}, ${assignment.round})`}`;
  }
  function rulePayload(rule) {
    return { Name: rule.name.trim(), Priority: Number(rule.priority), SortOrder: Number(rule.order), IsEnabled: rule.enabled,
      ConditionSource: conditionSource(rule), ConditionAstJson: JSON.stringify(conditionAst(rule)), ExpectedRevisionConcurrencyStamp: `demo-${state.stamp}` };
  }
  function assignmentPayload(assignment) {
    const variable = variableCatalog[assignment.target];
    if (variable.shape) {
      const ast = resultAst(assignment), constant = resultParts(assignment).every(part => part.source === 'constant');
      const constantJson = constant ? JSON.stringify(constantResult(assignment)) : null;
      const expressionJson = constant ? null : JSON.stringify(ast);
      if ((constantJson?.length || 0) > 4096 || (expressionJson?.length || 0) > 65536) throw new Error('结果定义过长，请减少候选项或简化公式。');
      return { TargetVariableStableKey: variable.key, ValueSourceKind: constant ? 'Constant' : 'Expression',
        ConstantValueJson: constantJson,
        ExpressionSource: constant ? null : assignmentSource(assignment).split('← ')[1], ExpressionAstJson: expressionJson,
        SortOrder: selected().assignments.indexOf(assignment) + 1, ExpectedRevisionConcurrencyStamp: `demo-${state.stamp}` };
    }
    return { TargetVariableStableKey: variable.key, ValueSourceKind: assignment.kind === 'constant' ? 'Constant' : 'Expression',
      ConstantValueJson: assignment.kind === 'constant' ? JSON.stringify(variable.kind === 'Decimal' ? Number(assignment.constant) : assignment.constant) : null,
      ExpressionSource: assignment.kind === 'expression' ? assignmentSource(assignment).split('← ')[1] : null,
      ExpressionAstJson: assignment.kind === 'expression' ? JSON.stringify(expressionAst(assignment)) : null,
      SortOrder: selected().assignments.indexOf(assignment) + 1, ExpectedRevisionConcurrencyStamp: `demo-${state.stamp}` };
  }
  const compactRules = window.matchMedia('(max-width: 760px)');
  const syncRulePicker = () => { $('#rule-picker').open = !compactRules.matches; };
  compactRules.addEventListener('change', syncRulePicker);
  syncRulePicker();
  function renderMenu() {
    $('#rule-picker-label').textContent = `${selected()?.name || '尚未选择规则'} · ${state.rules.length} 条规则`;

    $('#rule-menu').innerHTML = state.rules.map((rule, index) => `<button class="rule-select${rule.id === state.selectedId ? ' active' : ''}" aria-pressed="${rule.id === state.selectedId}" data-select-rule="${rule.id}"><span class="rule-meta">规则 ${String(index + 1).padStart(2, '0')} · 优先级 ${html(rule.priority)}</span><strong>${html(rule.name || '未命名规则')}</strong><small>${rule.saved ? '已保存' : '待创建'} · ${rule.assignments.length} 项赋值${rule.dirty || rule.assignments.some(item => item.dirty) ? ' · 有未保存修改' : ''}</small></button>`).join('') || '<p class="assignment-empty">当前修订没有规则。点击上方 ＋ 新建。</p>';
  }
  function renderConditionNode(node, root = false) {
    if (node.kind === 'condition') {
      const kind = variableCatalog[node.field].kind;
      return `<div class="condition-row" data-condition-id="${node.id}"><select class="row-input" data-condition-field="field" aria-label="条件变量">${variableOptions(node.field, (variable, code) => !variable.shape && (collection[state.revision].executionMode === 'Sequential' || variable.input || !variable.output || code === node.field))}</select><select class="row-input" data-condition-field="op" aria-label="比较操作符">${opOptions(node.op, kind)}</select><input class="row-input" data-condition-field="value" aria-label="比较值" value="${html(node.value)}" placeholder="比较值" ${kind === 'Decimal' ? 'inputmode="decimal"' : ''}><button class="mini-delete" data-remove-condition="${node.id}" aria-label="删除此条件" title="删除此条件">×</button></div>`;
    }
    return `<div class="condition-group${root ? ' root' : ''}" data-group-id="${node.id}">
      <div class="condition-group-head"><strong>${root ? '主条件组' : '子条件组'}</strong><label>组内关系<select data-group-op="${node.id}" aria-label="条件组关系">${option('and', '全部满足 AND', node.op)}${option('or', '任一满足 OR', node.op)}</select></label><div class="group-actions"><button class="btn quiet" data-add-condition="${node.id}">＋ 条件</button><button class="btn quiet" data-add-group="${node.id}">＋ 子组</button>${root ? '' : `<button class="mini-delete" data-remove-condition="${node.id}" aria-label="删除此条件组" title="删除此条件组">×</button>`}</div></div>
      <div class="condition-children">${node.children.length ? node.children.map(child => renderConditionNode(child)).join('') : '<p class="group-empty">无条件：主组表示始终满足；子组需添加条件后才能保存。</p>'}</div>
    </div>`;
  }
  function renderConditionRows() {
    const rule = selected();
    $('#condition-rows').innerHTML = rule ? renderConditionNode(rule.conditionTree, true) : '';
  }
  function renderConditionPreview() {
    const rule = selected();
    $('#condition-preview').textContent = rule ? conditionSource(rule) : '选择或新建规则';
    $('#ast-preview').textContent = rule ? JSON.stringify(conditionAst(rule), null, 2) : '';
  }
  function renderDefinition() {
    const rule = selected();
    $('#rule-name').value = rule?.name || '';
    $('#rule-priority').value = rule?.priority ?? '';
    $('#rule-order').value = rule?.order ?? '';
    $('#rule-enabled').checked = rule?.enabled || false;
    const badge = $('#rule-state');
    badge.textContent = !editable() ? '只读' : !rule ? '无规则' : rule.dirty ? '未保存修改' : '已保存';
    badge.className = `state-badge${!editable() ? ' readonly' : rule?.dirty ? ' dirty' : ''}`;
    renderConditionRows();
    renderConditionPreview();
    applyReadOnly();
  }
  function renderFormulaStatus(card, assignment) {
    const field = card.querySelector('[data-formula-edit]');
    const status = card.querySelector('.formula-status');
    field.setAttribute('aria-invalid', String(!!assignment.formulaError));
    status.textContent = assignment.formulaError || '公式语法正确 · 按先乘除、后加减计算；括号可调整顺序';
    status.classList.toggle('error', !!assignment.formulaError);
    card.querySelector('.assignment-source').textContent = assignmentSource(assignment);
    card.querySelector('.assignment-head span').textContent = `未保存 · ${kindName(variableCatalog[assignment.target].kind)}`;
  }
  function renderAssignments() {
    const rule = selected();
    const list = $('#assignment-list');
    if (!rule) { list.innerHTML = '<div class="assignment-empty">选择或新建规则后添加赋值。</div>'; applyReadOnly(); return; }
    list.innerHTML = rule.assignments.map((assignment, index) => {
      const target = variableCatalog[assignment.target];
      const expression = assignment.kind === 'expression';
      return `<article class="assignment-card" data-assignment-id="${assignment.id}">
        <div class="assignment-head"><strong>赋值 ${String(index + 1).padStart(2, '0')}</strong><div class="assignment-head-end"><span>${assignment.dirty ? '未保存' : '已保存'} · ${resultShapeName(target)}</span><button class="mini-delete" data-remove-assignment="${assignment.id}" aria-label="删除赋值 ${String(index + 1).padStart(2, '0')}，先查看影响" title="删除赋值 · 查看影响">×</button></div></div>
        <div class="assignment-grid"><label class="field">目标变量<select data-assignment-field="target">${variableOptions(assignment.target, variable => !variable.input)}</select></label><label class="field">值来源${target.shape ? '<span class="result-source-label">逐项设置</span>' : `<select data-assignment-field="kind">${option('constant', '常量', assignment.kind)}${option('expression', '表达式', assignment.kind)}</select>`}</label></div>
        <div class="assignment-detail">${target.shape ? renderResultEditor(assignment, target) : expression ? `
          <label class="formula-label" for="formula-${assignment.id}">计算公式</label>
          <textarea class="formula-input" id="formula-${assignment.id}" data-formula-edit rows="2" spellcheck="false" aria-describedby="formula-help-${assignment.id} formula-status-${assignment.id}" aria-invalid="${!!assignment.formulaError}" placeholder="例如：入口宽度 × 展开系数 + 偏移量">${html(assignment.formulaDraft ?? expressionNodeSource(assignment.expression))}</textarea>
          <p class="formula-help" id="formula-help-${assignment.id}">直接输入，或点击下方变量和符号插入光标处；用括号调整计算顺序。</p>
          <div class="formula-palette" role="group" aria-label="插入变量"><span>插入变量</span>${Object.entries(variableCatalog).filter(([, item]) => item.kind === 'Decimal' && !item.shape).map(([, item]) => `<button class="formula-token variable-token" data-formula-token="${html(item.name)}" type="button">${html(item.name)}</button>`).join('')}</div>
          <div class="formula-palette" role="group" aria-label="插入运算符"><span>插入符号</span>${['+', '−', '×', '÷', '(', ')'].map(symbol => `<button class="formula-token" data-formula-token="${symbol}" type="button">${symbol}</button>`).join('')}</div>
          <p class="formula-status${assignment.formulaError ? ' error' : ''}" id="formula-status-${assignment.id}" role="status">${html(assignment.formulaError || '公式语法正确 · 按先乘除、后加减计算；括号可调整顺序')}</p>
          <label class="field round-field">保留小数位（可空）<input data-assignment-field="round" type="number" min="0" max="28" step="1" value="${html(assignment.round)}" placeholder="不舍入"></label>`
          : `<label class="field">常量值<input data-assignment-field="constant" value="${html(assignment.constant)}" placeholder="输入${kindName(target.kind)}值"></label>`}<p class="assignment-source">${html(assignmentSource(assignment))}</p></div>
        <div class="assignment-actions"><button class="btn subtle" data-save-assignment="${assignment.id}">保存此赋值</button></div>
      </article>`;
    }).join('') || '<div class="assignment-empty">此规则尚无赋值。可先保存规则，再添加赋值；完整校验会检查必需出口。</div>';
    applyReadOnly();
  }
  function renderSummary() {
    const rule = selected();
    const assignmentsSaved = !!rule && rule.assignments.length > 0 && rule.assignments.every(item => item.saved && !item.dirty);
    const items = [
      { level: 'ok', text: editable() ? `当前为可编辑草稿 ${revisionLabel()}` : `${revisionLabel()} · 只读` },
      { level: rule?.saved && !rule?.dirty ? 'ok' : 'wait', text: rule ? (rule.dirty ? '规则信息或条件待保存' : '规则信息与条件已保存') : '请选择规则' },
      { level: assignmentsSaved ? 'ok' : 'wait', text: rule ? `${rule.assignments.length} 项赋值 · ${assignmentsSaved ? '已保存' : '待保存'}` : '未选择赋值' }
    ];
    if (state.conflict) items.push({ level: 'error', text: '并发冲突 · 保存已暂停' });
    $('#save-summary').innerHTML = items.map(item => `<div class="summary-item ${item.level}"><span class="bullet">${item.level === 'ok' ? '✓' : item.level === 'error' ? '!' : '·'}</span><span>${html(item.text)}</span></div>`).join('');
    $('#revision-stamp').textContent = `修订标识 #${state.stamp}`;
    const alert = $('#conflict-alert');
    alert.hidden = !state.conflict;
    $('#conflict-title').textContent = '草稿已被他人更新';
    $('#conflict-detail').textContent = '保存已暂停，请刷新草稿。';
    $('#trigger-conflict').hidden = state.conflict;
  }
  function applyReadOnly() {
    const disabled = !editable() || !selected();
    $$('.stage input,.stage select,.stage textarea').forEach(node => node.disabled = disabled);
    ['add-assignment','save-rule','delete-rule'].forEach(id => $(`#${id}`).disabled = disabled);
    $$('#condition-rows button,#assignment-list button').forEach(node => node.disabled = disabled || node.hasAttribute('data-limit-disabled'));
    $('#add-rule').disabled = !editable();
    $('#trigger-conflict').disabled = !editable();
    $('#run-validation').disabled = !editable();
    $('#run-simulation').disabled = !editable();
  }
  function render() { $('#validation-result').hidden = true; $('#simulation-result').hidden = true; renderMenu(); renderDefinition(); renderAssignments(); renderSummary(); }
  function showStage(stage) {
    state.stage = stage;
    $$('.flow-tab').forEach(tab => { const active = tab.dataset.stage === stage; tab.classList.toggle('active', active); tab.setAttribute('aria-selected', String(active)); tab.tabIndex = active ? 0 : -1; tab.id = `flow-tab-${tab.dataset.stage}`; tab.setAttribute('aria-controls', `stage-${tab.dataset.stage}`); });
    $$('.stage').forEach(section => { section.classList.toggle('active', section.id === `stage-${stage}`); section.setAttribute('aria-labelledby', section.id.replace('stage-', 'flow-tab-')); });
  }
  function requireWrite(messageId) {
    if (!editable()) { showMessage(messageId, '线上修订只读。请切回草稿或从线上创建新草稿。', 'error'); return false; }
    if (state.conflict) { showMessage(messageId, '草稿已被他人更新，请先刷新草稿。', 'error'); return false; }
    return true;
  }
  function ruleError(message, field) {
    showMessage('definition-message', message, 'error');
    if (field) { field.setAttribute('aria-invalid', 'true'); field.setAttribute('aria-describedby', 'definition-message'); field.focus(); }
  }
  function saveRule() {
    if (!requireWrite('definition-message')) return;
    const rule = selected(); if (!rule) return;
    if (!rule.name.trim()) { ruleError('填写规则名称后再保存。', $('#rule-name')); return; }
    if (!Number.isInteger(Number(rule.priority)) || !Number.isInteger(Number(rule.order)) || rule.priority === '' || rule.order === '') { ruleError('优先级和同级顺序必须是整数。', !Number.isInteger(Number(rule.priority)) || rule.priority === '' ? $('#rule-priority') : $('#rule-order')); return; }
    if (state.rules.some(other => other.id !== rule.id && Number(other.priority) === Number(rule.priority) && Number(other.order) === Number(rule.order))) { ruleError('同优先级下的顺序值与其他规则重复。', $('#rule-order')); return; }
    function conditionError(node, root = false) {
      if (node.kind === 'condition') return node.value.trim() === '' || (variableCatalog[node.field].kind === 'Decimal' && !Number.isFinite(Number(node.value))) ? '条件比较值不能为空，数值变量需要有效数值。' : '';
      if (!root && !node.children.length) return '子条件组至少需要一个条件。';
      return node.children.map(child => conditionError(child)).find(Boolean) || '';
    }
    const invalidCondition = conditionError(rule.conditionTree, true);
    if (invalidCondition) { ruleError(invalidCondition, $$('#condition-rows input').find(input => !input.value.trim() || input.inputMode === 'decimal' && !Number.isFinite(Number(input.value)))); return; }
    const payload = rulePayload(rule);
    rule.saved = true; rule.dirty = false; state.stamp += 1; persistChange('rule', rule);
    showMessage('definition-message', `规则与条件已保存到当前草稿，可继续编制赋值。`, 'success');
    renderMenu(); renderDefinition(); renderSummary(); toast('规则与条件已保存');
  }
  function saveAssignment(id) {
    if (!requireWrite('assignment-message')) return;
    const rule = selected(); const assignment = rule?.assignments.find(item => item.id === id); if (!assignment) return;
    if (!rule.saved || rule.dirty) { showMessage('assignment-message', '先保存规则与条件，再保存赋值。', 'error'); return; }
    if (rule.assignments.some(item => item.id !== id && item.target === assignment.target)) { showMessage('assignment-message', '同一规则内不能重复写入同一个目标变量。', 'error'); return; }
    const target = variableCatalog[assignment.target];
    if (target.shape) {
      try { assignmentPayload(assignment); }
      catch (error) { showMessage('assignment-message', error.message, 'error'); return; }
      assignment.saved = true; assignment.dirty = false; state.stamp += 1; persistChange('assignment', rule, assignment);
      showMessage('assignment-message', '赋值已保存到当前草稿。', 'success');
      renderMenu(); renderAssignments(); renderSummary(); $(`[data-save-assignment="${id}"]`)?.focus({ preventScroll:true }); toast('赋值已保存');
      return;
    }
    if (assignment.kind === 'expression' && target.kind !== 'Decimal') { showMessage('assignment-message', '当前表达式产生数值，目标变量也必须是数值类型。', 'error'); return; }
    if (assignment.kind === 'expression') {
      updateFormula(assignment, assignment.formulaDraft ?? expressionNodeSource(assignment.expression));
      if (assignment.formulaError) {
        showMessage('assignment-message', `计算公式有误：${assignment.formulaError}`, 'error');
        renderAssignments();
        $(`[data-assignment-id="${id}"] [data-formula-edit]`)?.focus();
        return;
      }
    }
    if (assignment.kind === 'expression' && assignment.round !== '' && (!Number.isInteger(Number(assignment.round)) || Number(assignment.round) < 0 || Number(assignment.round) > 28)) { showMessage('assignment-message', '保留小数位应是 0–28 的整数。', 'error'); return; }
    function expressionValid(node) {
      if (node.kind === 'variable') return variableCatalog[node.variable]?.kind === 'Decimal';
      if (node.kind === 'constant') return node.value.trim() !== '' && Number.isFinite(Number(node.value));
      return expressionValid(node.left) && expressionValid(node.right);
    }
    if (assignment.kind === 'expression' && !expressionValid(assignment.expression)) { showMessage('assignment-message', '表达式中的数字常量不能为空，引用变量必须是数值类型。', 'error'); return; }
    if (assignment.kind === 'constant' && (assignment.constant === '' || (target.kind === 'Decimal' && !Number.isFinite(Number(assignment.constant))))) { showMessage('assignment-message', '填写与目标类型相符的常量值。', 'error'); return; }
    assignmentPayload(assignment);
    assignment.saved = true; assignment.dirty = false; state.stamp += 1; persistChange('assignment', rule, assignment);
    showMessage('assignment-message', '赋值已保存到当前草稿。', 'success');
    renderMenu(); renderAssignments(); renderSummary(); $(`[data-save-assignment="${id}"]`)?.focus({ preventScroll:true }); toast('赋值已保存');
  }
  let dialogTrigger = null;
  function openDelete(kind, id) {
    if (!requireWrite(kind === 'rule' ? 'definition-message' : 'assignment-message')) return;
    const rule = selected(); if (!rule) return;
    const assignment = kind === 'assignment' ? rule.assignments.find(item => item.id === id) : null;
    state.pendingDelete = { kind, id };
    $('#dialog-title').textContent = kind === 'rule' ? `删除「${rule.name || '未命名规则'}」？` : `删除「${variableCatalog[assignment.target].name}」赋值？`;
    $('#dialog-body').innerHTML = kind === 'rule'
      ? `<p>${rule.saved ? '这条规则将从当前草稿中移除，线上修订保持不变。' : '这条规则尚未保存，只需丢弃本地编辑内容。'}删除规则时，其 ${rule.assignments.length} 项赋值会一起移除。</p><ul><li>规则路径：${revisionLabel()} / ${html(rule.name || '未命名规则')}</li><li>相关发布校验规则：本规则集的回归用例（需重新验证）</li><li>删除后应重新进行完整校验与试算</li></ul>`
      : `<p>${assignment.saved ? '这项赋值将从当前草稿中移除，依赖该值的后续计算可能受到影响。' : '这条赋值尚未保存，只需丢弃本地编辑内容。'}</p><ul><li>赋值路径：${revisionLabel()} / ${html(rule.name)} / ${html(variableCatalog[assignment.target].name)}</li><li>删除后可能缺少必需出口，请重新校验</li></ul>`;
    dialogTrigger = document.activeElement;
    $('#dialog-backdrop').showModal();
    $('#dialog-cancel').focus();
  }
  function closeDialog() { $('#dialog-backdrop').close(); state.pendingDelete = null; }
  $('#dialog-backdrop').addEventListener('close', () => {
    state.pendingDelete = null;
    if (dialogTrigger?.isConnected) dialogTrigger.focus();
    else document.querySelector('.flow-tab.active')?.focus();
  });
  function confirmDelete() {
    const pending = state.pendingDelete; if (!pending) return;
    if (!requireWrite(pending.kind === 'rule' ? 'definition-message' : 'assignment-message')) { closeDialog(); return; }
    const rule = selected();
    if (pending.kind === 'rule') {
      state.rules.splice(state.rules.findIndex(item => item.id === rule.id), 1);
      state.selectedId = state.rules[0]?.id || null;
    } else {
      const index = rule.assignments.findIndex(item => item.id === pending.id);
      const item = rule.assignments[index];
      if (item) { rule.assignments.splice(index, 1); if (item.saved) state.stamp += 1; }
    }
    if (pending.kind === 'rule' && rule.saved) state.stamp += 1;
    persistChange(pending.kind === 'rule' ? 'delete-rule' : 'delete-assignment', rule, { id: pending.id });
    closeDialog(); render(); toast('已删除，请重新校验');
  }
  function runValidation() {
    const box = $('#validation-result'); box.hidden = false;
    const revision = { ...collection[state.revision], rules:state.rules };
    const issues = execution.validate(revision, variableCatalog);
    try { execution.checkSettings(collectionStore.getScene(collection.scene), revision); } catch (error) { issues.push(error.message); }
    if (allDirty()) issues.push('草稿中有未保存修改；先保存后再验证。');
    if (state.conflict) issues.push('草稿版本需要刷新。');
    box.className = `result-box${issues.length ? ' error' : ''}`;
    box.innerHTML = issues.length ? `<strong>校验预览：${issues.length} 项待处理</strong>${issues.map(issue => `<p>${html(issue)}</p>`).join('')}` : `<strong>校验预览：执行设置、重复赋值与结果覆盖未发现问题</strong><p>正式发布前仍需服务端检查完整类型、依赖环、绑定歧义与回归样本。</p>`;
  }
  function conditionMatches(rule, values) {
    function evaluate(node) {
      if (node.kind === 'condition') {
        if (values[node.field] === undefined || values[node.field] === null) throw new Error(`${variableCatalog[node.field].name} 尚未赋值，不能用于条件判断。`);
        const lhs = values[node.field]; const rhs = variableCatalog[node.field].kind === 'Decimal' ? Number(node.value) : node.value;
        return ({ eq: lhs === rhs, ne: lhs !== rhs, gt: lhs > rhs, gte: lhs >= rhs, lt: lhs < rhs, lte: lhs <= rhs })[node.op];
      }
      return node.op === 'and' ? node.children.every(evaluate) : node.children.length === 0 || node.children.some(evaluate);
    }
    return evaluate(rule.conditionTree);
  }
  function evaluateExpression(node, values) {
    if (node.kind === 'variable') {
      if (values[node.variable] === undefined || values[node.variable] === null) throw new Error(`${variableCatalog[node.variable]?.name || node.variable} 尚未赋值。`);
      const value = Number(values[node.variable]);
      if (!Number.isFinite(value)) throw new Error('公式读取了无效数值。');
      return value;
    }
    if (node.kind === 'constant') return Number(node.value);
    const left = evaluateExpression(node.left, values), right = evaluateExpression(node.right, values);
    if (node.op === 'div' && right === 0) throw new Error('除数不能为零。');
    return ({ add: left + right, sub: left - right, mul: left * right, div: left / right })[node.op];
  }
  function evaluateAssignment(assignment, values) {
    const target = variableCatalog[assignment.target];
    if (target.shape) return evaluateResult(assignment, values);
    if (assignment.kind === 'constant') return target.kind === 'Decimal' ? Number(assignment.constant) : assignment.constant;
    let value = evaluateExpression(assignment.expression, values);
    if (assignment.round !== '') value = Number(value.toFixed(Number(assignment.round)));
    return value;
  }
  function executionTrace(trace, expanded = false) {
    if (!trace.length) return '';
    return `<details class="simulation-trace"${expanded ? ' open' : ''}><summary>执行过程 · ${execution.modes[collection[state.revision].executionMode].name}</summary><ol>${trace.map(step => `<li><div class="trace-rule"><strong>${html(step.name)}</strong><span>${html(step.status)} · 优先级 ${html(step.priority)} · 顺序 ${html(step.order)}</span></div>${step.assignments.length ? `<ul>${step.assignments.map(write => `<li><span>${html(variableCatalog[write.target].name)} · ${write.action}</span><code>${write.previous !== undefined ? html(JSON.stringify(write.previous)) + ' → ' : ''}${html(JSON.stringify(write.value))}</code></li>`).join('')}</ul>` : ''}</li>`).join('')}</ol></details>`;
  }
  function resultValue(value) {
    if (Array.isArray(value)) return `<div class="result-values">${value.map(item => `<span>${html(item)}</span>`).join('')}</div>`;
    if (value && typeof value === 'object') return `${html(value.min)} <span class="subtle">至</span> ${html(value.max)}`;
    return html(value);
  }
  function resultRows(results, revision) {
    return `<div class="simulation-rows">${results.map((row, index) => `<section class="simulation-row" aria-label="结果 ${index + 1}"><div class="result-row-heading"><h3>结果 ${index + 1}</h3><span>命中规则：${row.sourceRuleIds.map(id => html(revision.rules.find(rule => rule.id === id)?.name || id)).join('、')}</span></div><dl>${Object.entries(row.outputs).map(([code,value]) => `<div><dt>${html(variableCatalog[code]?.name || code)}</dt><dd>${resultValue(value)}</dd></div>`).join('')}</dl></section>`).join('')}</div>`;
  }
  function runSimulation() {
    const box = $('#simulation-result'); box.hidden = false;
    if (!editable()) { box.className = 'result-box error'; box.innerHTML = '<strong>选择草稿再试算</strong><p>草稿试算需要指定草稿修订；线上版本在此只读展示。</p>'; return; }
    if (allDirty() || state.conflict) { box.className = 'result-box error'; box.innerHTML = '<strong>不能试算当前本地修改</strong><p>先保存草稿中的规则与赋值，并解决并发冲突。试算只读取已保存的草稿修订。</p>'; return; }
    const width = Number($('#sim-width').value);
    if (!$('#sim-width').value.trim() || !Number.isFinite(width) || !$('#sim-kind').value.trim() || !$('#sim-route').value.trim()) {
      box.className = 'result-box error'; box.textContent = '填写场景上下文与有效入口宽度。'; return;
    }
    const initial = { inputWidth: width, productKind: $('#sim-kind').value.trim(), factor: 1.05, offset: .5 };
    const revision = collectionStore.get(collection.id).draft;
    const result = execution.execute({ revision, scene:collectionStore.getScene(collection.scene), variables:variableCatalog, initial, match:conditionMatches, evaluate:evaluateAssignment });
    box.className = `result-box${result.error ? ' error' : ''}`;
    box.innerHTML = result.error ? `<strong>试算未通过</strong><p class="simulation-error">${html(result.error)}</p><p>本次不返回部分结果行。</p>${executionTrace(result.trace, true)}`
      : `<strong>草稿规则预览 · ${result.results.length ? result.results.length + ' 行结果' : '没有规则命中'}</strong><p>${execution.modes[revision.executionMode].name}${revision.executionMode === 'Sequential' ? ' · ' + execution.policies[revision.multipleWritePolicy].name : ''}</p>${resultRows(result.results, revision)}${executionTrace(result.trace)}<details class="result-data"><summary>查看原始结果 JSON</summary><pre>${html(JSON.stringify({ results:result.results }, null, 2))}</pre></details><p>此预览使用已保存的执行设置与规则；完整绑定校验、数值精度和正式结果由服务端试算提供。</p>`;
  }
  function markDirty(rule, assignment) {
    if (assignment) assignment.dirty = true; else rule.dirty = true;
    $('#validation-result').hidden = true; $('#simulation-result').hidden = true;
    renderMenu(); renderSummary();
    const badge = $('#rule-state'); if (!assignment && badge) { badge.textContent = '未保存修改'; badge.className = 'state-badge dirty'; }
  }

  document.addEventListener('click', event => {
    if (event.target.closest('.skip-link')) { event.preventDefault(); $('#main-content').focus(); return; }
    const stage = event.target.closest('[data-stage]'); if (stage) { showStage(stage.dataset.stage); return; }
    const go = event.target.closest('[data-go]'); if (go) { if (go.dataset.go === 'assignments' && state.stage === 'definition' && selected() && (!selected().saved || selected().dirty)) { showMessage('definition-message', '先保存规则与条件，再继续编制赋值。', 'error'); $('#save-rule').focus(); return; } showStage(go.dataset.go); document.querySelector('.flow-tab.active').focus(); return; }
    const ruleButton = event.target.closest('[data-select-rule]');
    if (ruleButton) { if (allDirty()) { toast('先保存当前修改，再切换规则'); return; } state.selectedId = ruleButton.dataset.selectRule; showMessage('definition-message', ''); showMessage('assignment-message', ''); render(); if (compactRules.matches) { $('#rule-picker').open = false; $('#rule-picker-label').focus(); } else document.querySelector(`[data-select-rule="${state.selectedId}"]`)?.focus(); return; }
    if (event.target.closest('#add-rule')) { if (!editable()) return; if (allDirty()) { toast('先保存当前修改，再新建规则'); return; } const rule = newRule(); state.rules.push(rule); state.selectedId = rule.id; showStage('definition'); render(); $('#rule-name').focus(); return; }
    const addCondition = event.target.closest('[data-add-condition]'); if (addCondition) { const rule = selected(); if (!rule || !editable()) return; const parent = findConditionNode(rule.conditionTree, addCondition.dataset.addCondition)?.node; parent.children.push(condition('inputWidth', 'gte', '')); markDirty(rule); renderConditionRows(); renderConditionPreview(); applyReadOnly(); $(`[data-condition-id="${parent.children.at(-1).id}"] input`)?.focus(); return; }
    const addGroup = event.target.closest('[data-add-group]'); if (addGroup) { const rule = selected(); if (!rule || !editable()) return; const parent = findConditionNode(rule.conditionTree, addGroup.dataset.addGroup)?.node; parent.children.push(group('or', [condition('inputWidth', 'gte', '')])); markDirty(rule); renderConditionRows(); renderConditionPreview(); applyReadOnly(); $(`[data-group-id="${parent.children.at(-1).id}"] input`)?.focus(); return; }
    const removeCondition = event.target.closest('[data-remove-condition]'); if (removeCondition) { const rule = selected(); if (!rule || !editable()) return; const found = findConditionNode(rule.conditionTree, removeCondition.dataset.removeCondition); if (!found?.parent) return; found.parent.children.splice(found.parent.children.indexOf(found.node), 1); markDirty(rule); renderConditionRows(); renderConditionPreview(); applyReadOnly(); $(`[data-add-condition="${found.parent.id}"]`)?.focus(); return; }
    if (event.target.closest('#toggle-ast')) { const node = $('#ast-preview'); node.hidden = !node.hidden; $('#toggle-ast').textContent = node.hidden ? '查看条件结构' : '收起条件结构'; $('#toggle-ast').setAttribute('aria-expanded', String(!node.hidden)); return; }
    if (event.target.closest('#save-rule')) { saveRule(); return; }
    if (event.target.closest('#delete-rule')) { openDelete('rule', state.selectedId); return; }
    if (event.target.closest('#add-assignment')) { const rule = selected(); if (!rule || !editable()) return; if (!rule.saved || rule.dirty) { showMessage('assignment-message', '先保存规则与条件，再添加赋值。', 'error'); return; } rule.assignments.push(newAssignment()); markDirty(rule, rule.assignments.at(-1)); renderAssignments(); return; }
    const candidateAction = event.target.closest('[data-part-action],[data-add-candidate]');
    if (candidateAction) {
      const rule = selected(); if (!rule || !editable()) return;
      const card = candidateAction.closest('[data-assignment-id]');
      const assignment = rule.assignments.find(item => item.id === card.dataset.assignmentId);
      const parts = resultParts(assignment);
      if (candidateAction.hasAttribute('data-add-candidate')) { if (parts.length >= 100) return; parts.push({ source: 'constant', value: '' }); }
      else {
        const index = Number(candidateAction.closest('[data-part-index]').dataset.partIndex);
        const action = candidateAction.dataset.partAction;
        if (action === 'remove') { if (parts.length === 1) return; parts.splice(index, 1); }
        else { const next = index + (action === 'up' ? -1 : 1); if (next < 0 || next >= parts.length) return; [parts[index], parts[next]] = [parts[next], parts[index]]; }
      }
      markDirty(rule, assignment); renderAssignments();
      return;
    }
    const formulaToken = event.target.closest('[data-formula-token]'); if (formulaToken) {
      const rule = selected(); if (!rule || !editable()) return;
      const card = formulaToken.closest('[data-assignment-id]');
      const assignment = rule.assignments.find(item => item.id === card.dataset.assignmentId);
      const field = card.querySelector('[data-formula-edit]');
      const position = field.dataset.cursorReady === 'true' ? field.selectionStart : field.value.length;
      const end = field.dataset.cursorReady === 'true' ? field.selectionEnd : position;
      field.setRangeText(` ${formulaToken.dataset.formulaToken} `, position, end, 'end');
      field.dataset.cursorReady = 'true';
      updateFormula(assignment, field.value);
      markDirty(rule, assignment);
      renderFormulaStatus(card, assignment);
      field.focus();
      return;
    }
    const saveAssignmentButton = event.target.closest('[data-save-assignment]'); if (saveAssignmentButton) { saveAssignment(saveAssignmentButton.dataset.saveAssignment); return; }
    const removeAssignmentButton = event.target.closest('[data-remove-assignment]'); if (removeAssignmentButton) { openDelete('assignment', removeAssignmentButton.dataset.removeAssignment); return; }
    if (event.target.closest('#run-validation')) { runValidation(); $('#validation-result').focus({ preventScroll:true }); $('#validation-result').scrollIntoView({ block:'start' }); return; }
    if (event.target.closest('#run-simulation')) { runSimulation(); window.PrototypeUI.enhance($('#simulation-result')); $('#simulation-result').focus({ preventScroll:true }); $('#simulation-result').scrollIntoView({ block:'start' }); return; }
    if (event.target.closest('#trigger-conflict')) { state.conflict = true; $('#validation-result').hidden = true; $('#simulation-result').hidden = true; renderSummary(); toast('草稿已被他人更新，请刷新草稿'); return; }
    if (event.target.closest('#refresh-draft')) { state.conflict = false; state.stamp += 1; showMessage('definition-message', ''); showMessage('assignment-message', ''); renderSummary(); toast('草稿已刷新，请核对当前修改后再保存'); return; }
    if (event.target.closest('#dialog-cancel')) { closeDialog(); return; }
    if (event.target.id === 'dialog-backdrop') { const rect = event.target.getBoundingClientRect(); if (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom) closeDialog(); return; }
    if (event.target.closest('#dialog-confirm')) { confirmDelete(); }
  });
  document.addEventListener('input', event => {
    const rule = selected(); if (!rule || !editable()) return;
    event.target.removeAttribute('aria-invalid');
    if (event.target.getAttribute('aria-describedby') === 'definition-message') event.target.removeAttribute('aria-describedby');
    const ids = { 'rule-name': 'name', 'rule-priority': 'priority', 'rule-order': 'order' };
    if (ids[event.target.id]) { rule[ids[event.target.id]] = event.target.value; markDirty(rule); return; }
    const row = event.target.closest('[data-condition-id]');
    if (row && event.target.dataset.conditionField === 'value') { findConditionNode(rule.conditionTree, row.dataset.conditionId).node.value = event.target.value; markDirty(rule); renderConditionPreview(); return; }
    const card = event.target.closest('[data-assignment-id]');
    if (card && event.target.dataset.partField === 'value' && !event.target.matches('select')) {
      const assignment = rule.assignments.find(item => item.id === card.dataset.assignmentId);
      resultParts(assignment)[Number(event.target.closest('[data-part-index]').dataset.partIndex)].value = event.target.value;
      markDirty(rule, assignment); card.querySelector('.assignment-source').textContent = assignmentSource(assignment);
      return;
    }
    if (card && ['constant','round'].includes(event.target.dataset.assignmentField)) { const assignment = rule.assignments.find(item => item.id === card.dataset.assignmentId); assignment[event.target.dataset.assignmentField] = event.target.value; markDirty(rule, assignment); card.querySelector('.assignment-source').textContent = assignmentSource(assignment); card.querySelector('.assignment-head span').textContent = `未保存 · ${kindName(variableCatalog[assignment.target].kind)}`; }
    if (card && event.target.matches('[data-formula-edit]')) {
      const assignment = rule.assignments.find(item => item.id === card.dataset.assignmentId);
      event.target.dataset.cursorReady = 'true';
      if (event.isComposing || event.target.dataset.composing === 'true') {
        assignment.formulaDraft = event.target.value;
        card.querySelector('.formula-status').textContent = '正在输入公式…';
        markDirty(rule, assignment);
        return;
      }
      updateFormula(assignment, event.target.value);
      markDirty(rule, assignment);
      renderFormulaStatus(card, assignment);
    }
  });
  document.addEventListener('change', event => {
    const rule = selected();
    if (event.target.id === 'revision-mode') {
      if (allDirty()) { event.target.value = state.revision; toast('先保存当前修改，再切换修订'); return; }
      state.revision = event.target.value;
      state.rules = collectionStore.clone(collectionStore.get(collection.id)[state.revision].rules);
      state.selectedId = state.rules.find(rule => rule.id === state.selectedId)?.id || state.rules[0]?.id || null;
      updateContext(); render();
      showMessage('definition-message', !editable() ? '当前修订为只读展示；需要修改时请返回规则集创建或选择草稿。' : '', !editable() ? 'success' : '');
      return;
    }
    if (!rule || !editable()) return;
    if (event.target.id === 'rule-enabled') { rule.enabled = event.target.checked; markDirty(rule); return; }
    if (event.target.dataset.groupOp) { findConditionNode(rule.conditionTree, event.target.dataset.groupOp).node.op = event.target.value; markDirty(rule); renderConditionPreview(); return; }
    const row = event.target.closest('[data-condition-id]');
    if (row && event.target.dataset.conditionField) { const item = findConditionNode(rule.conditionTree, row.dataset.conditionId).node; item[event.target.dataset.conditionField] = event.target.value; if (event.target.dataset.conditionField === 'field') { const kind = variableCatalog[item.field].kind; if (kind === 'String' && !['eq','ne'].includes(item.op)) item.op = 'eq'; item.value = ''; renderConditionRows(); applyReadOnly(); } markDirty(rule); renderConditionPreview(); return; }
    const card = event.target.closest('[data-assignment-id]');
    if (card && event.target.matches('select') && event.target.dataset.partField) {
      const assignment = rule.assignments.find(item => item.id === card.dataset.assignmentId);
      const part = resultParts(assignment)[Number(event.target.closest('[data-part-index]').dataset.partIndex)];
      part[event.target.dataset.partField] = event.target.value;
      if (event.target.dataset.partField === 'source') part.value = '';
      markDirty(rule, assignment); renderAssignments(); return;
    }
    if (card && event.target.matches('select') && event.target.dataset.assignmentField) { const assignment = rule.assignments.find(item => item.id === card.dataset.assignmentId); if (event.target.dataset.assignmentField === 'target') { delete assignment.parts; assignment.kind = 'constant'; assignment.constant = ''; assignment.round = ''; assignment.expression = defaultExpression(); delete assignment.formulaDraft; delete assignment.formulaError; } assignment[event.target.dataset.assignmentField] = event.target.value; markDirty(rule, assignment); renderAssignments(); }
  });
  document.addEventListener('focusin', event => {
    if (event.target.matches('[data-formula-edit]')) event.target.dataset.cursorReady = 'true';
  });
  document.addEventListener('compositionstart', event => {
    if (event.target.matches('[data-formula-edit]')) event.target.dataset.composing = 'true';
  });
  document.addEventListener('compositionend', event => {
    if (!event.target.matches('[data-formula-edit]')) return;
    event.target.dataset.composing = 'false';
    const card = event.target.closest('[data-assignment-id]');
    const assignment = selected()?.assignments.find(item => item.id === card.dataset.assignmentId);
    if (!assignment) return;
    updateFormula(assignment, event.target.value);
    markDirty(selected(), assignment);
    renderFormulaStatus(card, assignment);
  });
  document.addEventListener('keydown', event => {
    const tab = event.target.closest('.flow-tab');
    if (!tab || !['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const tabs = $$('.flow-tab'), index = tabs.indexOf(tab);
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
    showStage(tabs[next].dataset.stage);
    tabs[next].focus();
  });
  if (params.get('mode') === 'new' && editable()) { const rule = newRule(); state.rules.push(rule); state.selectedId = rule.id; }
  updateContext();
  showStage(state.stage);
  if (['definition','assignments','review'].includes(params.get('stage'))) showStage(params.get('stage'));
  window.addEventListener('beforeunload', event => { if (allDirty()) { event.preventDefault(); event.returnValue = ''; } });
  render();
})();

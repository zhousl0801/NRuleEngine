(() => {
  'use strict';
  const key = 'nruleengine-prototype-collections-v1';
  const clone = value => JSON.parse(JSON.stringify(value));
  const sceneDefinitions = [
    { code: 'size-expansion', kind: 'rule', name: '尺寸展开', description: '根据入口宽度和产品类别，计算展开宽度及可选规格。' },
    { code: 'finishing-width', kind: 'rule', name: '精整宽度修正', description: '按精整产品类别计算修正宽度，输出可选规格。' },
    { code: 'trial-expansion', kind: 'rule', name: '试验规格展开', description: '为试验产品单独编制宽度计算与规格选择规则。' },
    { code: 'candidate-eligibility', kind: 'condition', name: '候选适用判定', description: '根据入口宽度和产品类别，判断给定候选是否适用。' },
    { code: 'finishing-eligibility', kind: 'condition', name: '精整候选筛选', description: '判断精整产品是否满足指定候选的宽度与类别要求。' },
    { code: 'trial-eligibility', kind: 'condition', name: '试验候选判定', description: '为试验产品维护独立的候选适用条件。' }
  ];
  const getScene = code => sceneDefinitions.find(scene => scene.code === code);
  const variable = name => ({ kind: 'variable', variable: name });
  const formula = () => ({ kind: 'binary', op: 'mul', left: variable('inputWidth'), right: variable('factor') });
  function rule(id, name, product, threshold, priority, order) {
    return { id, name, priority, order, enabled: true, saved: true, dirty: false,
      conditionTree: { kind: 'group', id: `${id}-group`, op: 'and', children: product ? [
        { kind: 'condition', id: `${id}-width`, field: 'inputWidth', op: 'gte', value: String(threshold) },
        { kind: 'condition', id: `${id}-product`, field: 'productKind', op: 'eq', value: product }
      ] : [] },
      assignments: [
        { id: `${id}-width-result`, target: 'outputWidth', kind: product ? 'expression' : 'constant', expression: formula(), round: '2', constant: product ? '' : '0', saved: true, dirty: false },
        { id: `${id}-status`, target: 'calcStatus', kind: 'constant', constant: product ? '已计算' : '使用默认值', expression: formula(), round: '', saved: true, dirty: false },
        { id: `${id}-range`, target: 'widthRange', parts: [{ source: 'expression', value: '入口宽度' }, { source: 'expression', value: '入口宽度 × 展开系数' }], saved: true, dirty: false },
        { id: `${id}-widths`, target: 'candidateWidths', parts: [{ source: 'expression', value: '入口宽度' }, { source: 'expression', value: '入口宽度 + 10' }], saved: true, dirty: false },
        { id: `${id}-codes`, target: 'candidateCodes', parts: (['C','D'].includes(product) ? [product, product === 'C' ? 'D' : 'E'] : ['A','B']).map(value => ({ source: 'constant', value })), saved: true, dirty: false }
      ] };
  }
  function model(id, code, name, description, no, rules, bindings, hasDraft, scene = 'size-expansion') {
    const published = { no, rules, bindings, gateStatus: 'passed' };
    return { id, kind: 'rule', scene, code, name, description, archived: false,
      published, draft: hasDraft ? { ...clone(published), no: no + 1, gateStatus: 'pending' } : null, nextNo: no + (hasDraft ? 2 : 1) };
  }
  function condition(id, code, name, product, threshold, priority, no, hasDraft, scene = 'candidate-eligibility') {
    const published = { no, product, threshold, operator: 'gte', priority, gateStatus: 'passed' };
    return { id, kind: 'condition', scene, code, name,
      description: `产品 ${product} 的候选适用条件`, archived: false, published,
      draft: hasDraft ? { ...clone(published), no: no + 1, gateStatus: 'pending' } : null, nextNo: no + (hasDraft ? 2 : 1) };
  }
  function initialState() {
    const standard = rule('standard', '标准规格计算', 'A', 120, 100, 1);
    const special = rule('special', '特殊规格修正', 'B', 160, 60, 2);
    special.conditionTree.children[0].op = 'gt';
    special.assignments[0].expression = { kind: 'binary', op: 'add', left: formula(), right: variable('offset') };
    special.assignments[1].constant = '已修正';
    return { records: [
      model('expansion', 'SIZE-EXP-01', '尺寸展开规则', '常规与特殊产品的规格计算', 3,
        [standard, special, rule('fallback', '默认值处理', '', 0, 10, 3)],
        [{ name: '常规产品', product: 'A', processes: ['通用','精整'], text: '产品类别 = A · 工艺路线属于通用、精整', priority: 100 }, { name: '特殊产品', product: 'B', processes: ['精整'], text: '产品类别 = B · 工艺路线 = 精整', priority: 80 }], true),
      model('line', 'SIZE-EXP-LINE', '精整线展开规则', '精整线产品独立维护的计算规则', 2,
        [rule('line-rule', '精整线规格计算', 'C', 120, 100, 1)], [{ name: '精整线产品', product: 'C', processes: ['精整'], text: '产品类别 = C · 工艺路线 = 精整', priority: 90 }], false),
      model('trial', 'SIZE-EXP-TRIAL', '试验规格规则', '小范围试验规格的计算规则', 1,
        [rule('trial-rule', '试验规格计算', 'D', 100, 100, 1)], [{ name: '试验产品', product: 'D', processes: ['试验'], text: '产品类别 = D · 工艺路线 = 试验', priority: 70 }], false),
      condition('cond-a', 'COND-A', '标准候选适用', 'A', 120, 100, 2, true),
      condition('cond-b', 'COND-B', '特殊候选适用', 'B', 160, 60, 1, false),
      model('finishing-standard', 'FINISH-WIDTH-C', '常规精整宽度', '产品 C 的精整宽度计算', 2,
        [rule('finish-c-rule', '常规精整计算', 'C', 120, 100, 1)], [{ name: '常规精整', product: 'C', processes: ['精整'], text: '产品类别 = C · 工艺路线 = 精整', priority: 100 }], true, 'finishing-width'),
      model('finishing-special', 'FINISH-WIDTH-D', '特殊精整宽度', '产品 D 的精整宽度计算', 1,
        [rule('finish-d-rule', '特殊精整计算', 'D', 160, 100, 1)], [{ name: '特殊精整', product: 'D', processes: ['精整'], text: '产品类别 = D · 工艺路线 = 精整', priority: 80 }], false, 'finishing-width'),
      condition('finish-cond-c', 'FINISH-COND-C', '常规精整候选', 'C', 120, 100, 1, true, 'finishing-eligibility'),
      condition('finish-cond-d', 'FINISH-COND-D', '特殊精整候选', 'D', 160, 80, 1, false, 'finishing-eligibility')
    ] };
  }
  let state;
  try {
    state = JSON.parse(sessionStorage.getItem(key) || 'null');
    if (!state || !Array.isArray(state.records)) state = initialState();
  } catch { state = initialState(); }
  // 补入新增场景的样例，保留当前标签页已经保存的编辑和归档状态。
  for (const item of initialState().records) if (!state.records.some(saved => saved.id === item.id)) state.records.push(item);
  function persist() { try { sessionStorage.setItem(key, JSON.stringify(state)); } catch { /* 禁用存储时保留本页内的演示状态。 */ } }
  function get(id) {
    const item = state.records.find(record => record.id === id);
    if (!item) throw new Error('找不到这个集合，请返回列表重新选择。');
    return item;
  }
  function identity(kind, input) {
    const code = input.code.trim(), name = input.name.trim();
    if (!code || !name) throw new Error('请填写名称和编码。');
    if (name.length > 256 || code.length > 128) throw new Error('名称最多 256 个字符，编码最多 128 个字符。');
    if (state.records.some(record => record.kind === kind && record.code === code)) throw new Error('编码已存在，请使用其他编码。');
    return { code, name, description: (input.description || '').trim() };
  }
  function create(kind, input) {
    if (!['rule', 'condition'].includes(kind)) throw new Error('集合类型无效。');
    const scene = getScene(input.scene);
    if (!scene || scene.kind !== kind) throw new Error('请选择所属场景。');
    const info = identity(kind, input);
    const item = { id: `set-${Date.now()}-${state.records.length}`, kind, scene: scene.code,
      ...info, archived: false, published: null, nextNo: 2,
      draft: kind === 'rule' ? { no: 1, rules: [], bindings: [], gateStatus: 'pending' }
        : { no: 1, product: '', threshold: '', operator: 'gte', priority: 100, gateStatus: 'pending' } };
    state.records.push(item); persist(); return item;
  }
  function copy(id, input, sourceMode) {
    const source = get(id);
    if (source.kind !== 'rule') throw new Error('当前仅支持复制规则集。');
    const revision = source[sourceMode];
    if (!revision) throw new Error('所选来源修订不存在。');
    const item = create('rule', { ...input, scene: source.scene });
    item.draft = { ...clone(revision), no: 1, gateStatus: 'pending' };
    persist(); return item;
  }
  function createDraft(id) {
    const item = get(id);
    if (item.archived) throw new Error('已归档的集合不能创建草稿。');
    if (!item.draft) {
      if (!item.published) throw new Error('没有可用于创建草稿的线上修订。');
      item.draft = { ...clone(item.published), no: item.nextNo++, gateStatus: 'pending' };
      persist();
    }
    return item.draft;
  }
  function saveDraft(id, changes) {
    const item = get(id);
    if (item.archived || !item.draft) throw new Error('当前集合没有可编辑的草稿。');
    const allowed = item.kind === 'rule' ? ['rules', 'bindings'] : ['product', 'threshold', 'operator', 'priority'];
    for (const field of allowed) if (Object.hasOwn(changes, field)) item.draft[field] = clone(changes[field]);
    item.draft.gateStatus = 'pending'; persist();
  }
  function archive(id) { const item = get(id); item.archived = true; persist(); }
  function evaluate(ids, inputs, mode) {
    if (!ids.length) return { error: '请至少选择一个已发布候选。' };
    const candidates = ids.map(get);
    if (candidates.some(item => item.kind !== 'condition' || item.archived || !item.published)) return { error: '候选必须是尚未归档的已发布条件集。' };
    if (new Set(candidates.map(item => item.scene)).size > 1) return { error: '一次只能试算同一场景的候选。' };
    if (String(inputs.width).trim() === '' || !Number.isFinite(Number(inputs.width)) || !inputs.product.trim()) return { error: '请填写有效入口宽度和产品类别。' };
    const matches = candidates.filter(item => {
      const d = item.published;
      const widthMatch = ({ gt: Number(inputs.width) > Number(d.threshold), gte: Number(inputs.width) >= Number(d.threshold), lt: Number(inputs.width) < Number(d.threshold), lte: Number(inputs.width) <= Number(d.threshold) })[d.operator];
      return widthMatch && inputs.product.trim() === d.product;
    }).sort((a, b) => b.published.priority - a.published.priority);
    if (mode === 'first' && matches.length > 1 && matches[0].published.priority === matches[1].published.priority) return { error: '最高优先级存在并列命中，无法确定唯一候选。' };
    return { matches: mode === 'first' ? matches.slice(0, 1) : matches };
  }
  window.CollectionStore = { scenes: kind => sceneDefinitions.filter(scene => !kind || scene.kind === kind), getScene, list: kind => state.records.filter(item => item.kind === kind), get, create, copy, createDraft, saveDraft, archive, evaluate, clone };
})();

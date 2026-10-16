(() => {
  'use strict';
  const store = window.CollectionStore;
  const $ = selector => document.querySelector(selector);
  const $$ = selector => [...document.querySelectorAll(selector)];
  const html = value => String(value ?? '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);
  const names = { rule: '规则集', condition: '条件集' };
  const lists = { rule: 'model-list', condition: 'conditions' };
  const editors = { rule: 'models', condition: 'condition-editor' };
  const filters = { rule: { query: '', status: 'active' }, condition: { query: '', status: 'active' } };
  const filterKey = 'nruleengine-prototype-list-filters';
  try {
    const saved = JSON.parse(sessionStorage.getItem(filterKey) || 'null');
    for (const kind of ['rule','condition']) if (saved?.[kind] && typeof saved[kind].query === 'string' && ['active','draft','published','unpublished','archived'].includes(saved[kind].status)) filters[kind] = saved[kind];
  } catch { /* 无会话存储时，列表筛选只保留在当前页面。 */ }
  function rememberFilters() { try { sessionStorage.setItem(filterKey, JSON.stringify(filters)); } catch { /* 保留页内筛选。 */ } }
  const variableNames = { inputWidth: '入口宽度', productKind: '产品类别', factor: '展开系数', offset: '偏移量', outputWidth: '展开宽度', calcStatus: '计算状态', widthRange: '宽度范围', candidateWidths: '候选宽度', candidateCodes: '候选编码' };
  const operators = { eq: '=', ne: '≠', gt: '>', gte: '≥', lt: '<', lte: '≤' };
  let catalogQuery = '', route = {}, dirtyCondition = false, dialogSubmit = null, dialogTrigger = null, toastTimer, selectedRule = null;
  const revisionName = no => `R${String(no).padStart(2, '0')}`;
  const option = (value, text, selected) => `<option value="${html(value)}"${value === selected ? ' selected' : ''}>${html(text)}</option>`;
  function address(view, params = {}) { return `#${view}${Object.keys(params).length ? '?' + new URLSearchParams(params) : ''}`; }
  function parseRoute() {
    const [view, query = ''] = location.hash.slice(1).split('?');
    return { ...Object.fromEntries(new URLSearchParams(query)), view: view || 'catalog' };
  }
  function toast(message) { $('#toast').textContent = message; $('#toast').classList.add('show'); clearTimeout(toastTimer); toastTimer = setTimeout(() => $('#toast').classList.remove('show'), 3200); }
  function navigate(view, params = {}, replace = false, force = false) {
    if (dirtyCondition && !force) {
      showDialog('放弃未保存的修改？', '当前条件草稿有未保存的修改。保存后再切换可以保留这些内容。', '', '放弃修改并离开', () => { dirtyCondition = false; closeDialog(); navigate(view, params, replace, true); });
      return;
    }
    const hash = address(view, params);
    if (location.hash !== hash) history[replace ? 'replaceState' : 'pushState'](null, '', hash);
    renderRoute(true);
  }
  function listParams() { return route.scene ? { scene: route.scene } : {}; }
  function detailParams(item, extra = {}) {
    const scope = [lists.rule, lists.condition].includes(route.view) ? route.scene || 'all' : route.listScene || 'all';
    return { set: item.id, listScene: scope, ...extra };
  }
  function returnParams() { return route.listScene && route.listScene !== 'all' ? { scene: route.listScene } : {}; }
  function sceneOptions(kind, selected, all = true) {
    return (all ? option('', '全部场景', selected) : option('', '请选择场景', selected)) + store.scenes(kind).map(scene => option(scene.code, scene.name, selected)).join('');
  }
  function sceneDefinition(scene) {
    const table = (title, rows, binding = false) => `<div><h4>${title}</h4><div class="scenario-table-wrap" role="region" aria-label="${html(scene.name)}的${title}" tabindex="0"><table class="scenario-table"><thead><tr><th scope="col">名称</th><th scope="col">编码</th><th scope="col">类型</th><th scope="col">${binding ? '比较方式' : '取值要求'}</th></tr></thead><tbody>${rows.map(([name,code,type]) => `<tr><td>${name}</td><td><code>${code}</code></td><td>${type}</td><td>${binding ? '等于、属于' : '不可为空'}</td></tr>`).join('')}</tbody></table></div></div>`;
    return `<details class="scenario-details"><summary>查看场景说明</summary><div class="scenario-definition"><div class="scenario-code"><span>场景编码</span><code>${html(scene.code)}</code></div>${scene.kind === 'rule' ? `<p class="hint">${scene.resultCardinality === 'Many' ? '全部命中分别计算，每条规则返回一行完整结果。' : '成功时返回一行结果。'}</p>` : ''}
      ${table('输入数据', [['入口宽度','inputWidth','数值'],['产品类别','productKind','文本']])}
      ${scene.kind === 'rule' ? table('必须产出的结果', [['展开宽度','outputWidth','数值'],['计算状态','calcStatus','文本'],['宽度范围','widthRange','数值区间'],['候选宽度','candidateWidths','数值列表'],['候选编码','candidateCodes','文本列表']]) + table('可设置的适用范围', [['产品类别','productKind','文本'],['工艺路线','processRoute','文本']], true) : '<p class="hint">对调用方指定的候选返回满足条件或不满足条件。</p>'}
      </div></details>`;
  }
  function renderCatalog() {
    $('#view-catalog').innerHTML = `<div class="page-head"><div><h1 tabindex="-1">场景</h1><p class="head-copy">按业务用途选择场景，进入对应的规则集或条件集。</p></div><label class="collection-search catalog-search"><span>搜索场景</span><input type="search" data-scene-search autocomplete="off" placeholder="场景名称或编码" value="${html(catalogQuery)}"></label></div><div class="catalog-groups" id="catalog-groups"></div><p class="catalog-hint">场景说明展示当前的输入与结果要求；已有规则修订中的变量和类型以该修订为准。</p>`;
    renderCatalogGroups();
  }
  function renderCatalogGroups() {
    const query = catalogQuery.trim().toLowerCase();
    $('#catalog-groups').innerHTML = ['rule','condition'].map(kind => {
      const scenes = store.scenes(kind).filter(scene => `${scene.name} ${scene.code}`.toLowerCase().includes(query));
      return `<section class="catalog-group" aria-labelledby="catalog-${kind}-title"><div class="catalog-group-heading"><div><h2 id="catalog-${kind}-title">${names[kind]}场景 <span class="catalog-group-count">${scenes.length} 个场景</span></h2><p>${kind === 'rule' ? '根据输入计算结果，适合生成业务参数或其他输出。' : '判断指定候选是否满足条件，适合筛选候选或判断适用性。'}</p></div><button class="btn small ghost" data-view="${lists[kind]}">全部${names[kind]} →</button></div><div class="catalog-scenes">${scenes.map(scene => {
        const count = store.list(kind).filter(item => item.scene === scene.code && !item.archived).length;
        return `<article class="catalog-card" aria-labelledby="scene-${scene.code}-title"><h3 id="scene-${scene.code}-title">${html(scene.name)}</h3><p>${html(scene.description)}</p>${sceneDefinition(scene)}<div class="bottom"><span>${count ? `${count} 个${names[kind]}` : `尚无${names[kind]}`}</span><button class="btn small" data-open-scene="${scene.code}" data-kind="${kind}">${count ? '查看' + names[kind] : '进入场景'}</button></div></article>`;
      }).join('') || '<div class="catalog-no-results">没有匹配的场景，请调整名称或编码。</div>'}</div></section>`;
    }).join('');
  }
  function rowAction(action, id, label, extra = '') { return `<button type="button" class="btn small ghost" data-collection-action="${action}" data-set="${html(id)}" ${extra}>${label}</button>`; }
  function revisionBadges(item) {
    return `${item.published ? `<button class="status-pill live revision-badge" data-open-set="${html(item.id)}" data-mode="published">${item.archived ? '归档前发布' : '线上'} ${revisionName(item.published.no)}</button>` : '<span class="tag">未发布</span>'}
      ${item.draft ? `<button class="status-pill draft revision-badge" data-open-set="${html(item.id)}" data-mode="draft">草稿 ${revisionName(item.draft.no)}</button>` : ''}`;
  }
  function rowMenu(item) {
    if (item.archived) return '<span class="tag">已归档</span>';
    return `<details class="collection-menu"><summary aria-label="${html(item.name)}的更多操作">更多</summary><div>
      ${!item.draft && item.published ? rowAction('draft', item.id, '从线上创建草稿') : ''}
      ${item.kind === 'rule' ? rowAction('copy', item.id, '复制为新规则集') : ''}
      ${rowAction('archive', item.id, '归档')}
      </div></details>`;
  }
  function renderList(kind) {
    const type = names[kind], list = $(`#view-${lists[kind]}`), scene = store.getScene(route.scene);
    if (route.scene && (!scene || scene.kind !== kind)) {
      list.innerHTML = `<div class="collection-empty"><h1 tabindex="-1">找不到这个场景</h1><p>请选择${type}所属的场景。</p><button class="btn" data-view="${lists[kind]}">查看全部${type}</button></div>`;
      return;
    }
    list.innerHTML = `${scene ? '<div class="collection-back"><button class="btn small ghost" data-view="catalog">← 返回场景</button><button class="btn small ghost" data-view="' + lists[kind] + '">查看全部' + type + '</button></div>' : ''}
      <div class="page-head"><div><h1 tabindex="-1">${scene ? html(scene.name) + ' · ' : ''}${type}</h1><p class="head-copy">${scene ? `当前仅展示“${html(scene.name)}”中的${type}。` : `查看全部场景中的${type}，也可以按所属场景筛选。`}${kind === 'rule' ? '每个规则集独立维护规则、适用范围与修订。' : '每个条件集独立维护判定条件、优先级与修订。'}</p></div>
      <div class="head-actions">${kind === 'condition' ? '<button class="btn ghost" data-collection-action="evaluate">试算已发布候选</button>' : ''}<button class="btn primary" data-collection-action="new" data-kind="${kind}">＋ 新建${type}</button></div></div>
      <div class="panel collections-panel"><div class="collections-toolbar">
        <label class="collection-filter scene-filter"><span>所属场景</span><select data-list-scene="${kind}">${sceneOptions(kind, route.scene || '')}</select></label>
        <label class="collection-search"><span>搜索${type}</span><input type="search" autocomplete="off" placeholder="名称或编码" data-collection-search="${kind}" value="${html(filters[kind].query)}"></label>
        <label class="collection-filter"><span>维护状态</span><select data-collection-filter="${kind}">${[['active','全部在用'],['draft','有草稿'],['published','仅线上'],['unpublished','尚未发布'],['archived','已归档']].map(([v,t]) => option(v,t,filters[kind].status)).join('')}</select></label>
      </div><div class="collection-list-heading"><span data-collection-count="${kind}" role="status" aria-live="polite"></span><span>点击修订标签可直接查看对应内容</span></div>
      <div class="collection-columns" aria-hidden="true"><span>${type} / 所属场景</span><span>${kind === 'rule' ? '线上适用范围' : '线上判定优先级'}</span><span>修订</span><span>操作</span></div><div class="collection-rows" data-collection-rows="${kind}"></div></div>
      <div class="set-note"><strong>${kind === 'rule' ? '规则集在各自的场景内参与选择' : '在同一场景内选择候选进行判定'}</strong><p>${kind === 'rule' ? '多个规则集可以同时发布。运行时在同一场景内比较命中的绑定，选择唯一的最高优先级绑定；不同场景之间独立运行。编辑草稿不会改变当前线上结果。' : '调用方选择需要判定的候选。可返回全部命中，或按已发布修订的优先级返回唯一的首条命中；不同场景的候选分开试算。'}</p></div>`;
    renderRows(kind);
  }
  function renderRows(kind) {
    const filter = filters[kind], query = filter.query.trim().toLowerCase();
    const rows = store.list(kind).filter(item => {
      const status = filter.status === 'archived' ? item.archived : !item.archived && (filter.status === 'active' || filter.status === 'draft' && item.draft || filter.status === 'published' && item.published && !item.draft || filter.status === 'unpublished' && !item.published);
      return (!route.scene || item.scene === route.scene) && status && `${item.name} ${item.code}`.toLowerCase().includes(query);
    });
    $(`[data-collection-count="${kind}"]`).textContent = `${route.scene ? store.getScene(route.scene).name : '全部场景'} · ${rows.length} 个${names[kind]}${query ? '符合搜索条件' : ''}`;
    $(`[data-collection-rows="${kind}"]`).innerHTML = rows.map(item => `<article class="collection-row">
      <div class="collection-identity"><span class="set-code">${html(item.code)}</span><button class="collection-name" data-open-set="${html(item.id)}">${html(item.name)}</button><p>${html(item.description || '尚未填写说明')}</p><button class="collection-scene-link" data-open-scene="${html(item.scene)}" data-kind="${kind}">所属场景：${html(store.getScene(item.scene)?.name || item.scene)}</button></div>
      <div class="collection-field"><span class="collection-mobile-label">${kind === 'rule' ? '线上适用范围' : '线上判定优先级'}</span>${kind === 'rule' ? `<p>${html(item.published?.bindings.map(binding => binding.text).join('；') || '发布并配置绑定后参与运行选择')}</p>` : `<strong>${item.published ? html(item.published.priority) : '尚未发布'}</strong><p>${item.published ? '数值越大越先判定' : '优先级随修订发布后生效'}</p>`}</div>
      <div class="collection-revisions">${revisionBadges(item)}</div><div class="collection-row-actions"><button class="btn small${item.draft && !item.archived ? '' : ' ghost'}" data-open-set="${html(item.id)}" data-mode="${item.draft && !item.archived ? 'draft' : item.published ? 'published' : 'draft'}">${item.draft && !item.archived ? '编辑草稿' : '查看'}</button>${rowMenu(item)}</div></article>`).join('') || `<div class="collection-empty"><h2>${route.scene && !store.list(kind).some(item => item.scene === route.scene && !item.archived) && !query && filter.status === 'active' ? `这个场景还没有${names[kind]}` : `没有找到${names[kind]}`}</h2><p>${route.scene ? '可以在当前场景新建集合，或调整筛选条件查看已有内容。' : '调整名称、编码或维护状态后再试，也可以选择一个场景新建集合。'}</p><div class="empty-actions"><button class="btn primary" data-collection-action="new" data-kind="${kind}">新建${names[kind]}</button><button class="btn ghost" data-collection-action="clear" data-kind="${kind}">清除搜索与状态</button>${route.scene ? `<button class="btn ghost" data-view="${lists[kind]}">查看全部场景</button>` : ''}</div></div>`;
  }
  function detailHeader(item, mode) {
    const sameScene = store.list(item.kind).filter(x => x.scene === item.scene && (!x.archived || x.id === item.id));
    return `<div class="collection-back"><button class="btn small ghost" data-return-list="${item.kind}">← 返回${names[item.kind]}</button><label class="collection-switch">切换${names[item.kind]}<select data-switch-set="${item.kind}">${sameScene.map(x => option(x.id, `${x.name} · ${x.code}`, item.id)).join('')}</select></label></div>
      <div class="page-head"><div><div class="collection-context">${html(store.getScene(item.scene)?.name || item.scene)}</div><h1 tabindex="-1">${html(item.name)}</h1><p class="head-copy">${html(item.description || '尚未填写说明')}</p><div class="meta-row"><span class="mono">${html(item.code)}</span>${item.archived ? '<span class="tag">已归档</span>' : ''}</div></div><div class="head-actions">${item.kind === 'rule' && !item.archived ? rowAction('copy', item.id, '复制规则集') : ''}${!item.archived ? rowAction('archive', item.id, '归档') : ''}</div></div>
      <div class="revision-toolbar"><div class="revision-switch" role="group" aria-label="查看修订">${item.draft ? `<button aria-pressed="${mode === 'draft'}" data-revision="draft">草稿 ${revisionName(item.draft.no)}</button>` : ''}${item.published ? `<button aria-pressed="${mode === 'published'}" data-revision="published">${item.archived ? '归档前发布' : '线上'} ${revisionName(item.published.no)}</button>` : ''}</div><p>${item.archived ? '归档内容只读，不参与后续运行。' : mode === 'draft' ? '当前编辑草稿，线上修订独立保留。' : '线上修订只读。需要修改时，从它创建新草稿。'}</p>${!item.draft && !item.archived ? rowAction('draft', item.id, '从线上创建草稿') : ''}</div>`;
  }
  function source(node) {
    if (node.kind === 'condition') return `${variableNames[node.field] || node.field} ${operators[node.op]} ${node.value}`;
    return node.children.length ? node.children.map(child => child.kind === 'group' ? `(${source(child)})` : source(child)).join(node.op === 'and' ? ' 且 ' : ' 或 ') : '始终满足';
  }
  function expression(node) {
    if (node.kind === 'variable') return variableNames[node.variable] || node.variable;
    if (node.kind === 'constant') return String(node.value);
    return `(${expression(node.left)} ${({ add: '+', sub: '−', mul: '×', div: '÷' })[node.op]} ${expression(node.right)})`;
  }
  function assignmentText(assignment) {
    if (assignment.parts) return `[${assignment.parts.map(part => part.source === 'constant' ? JSON.stringify(part.value) : part.value).join(', ')}]`;
    if (assignment.kind === 'constant') return assignment.target === 'calcStatus' ? JSON.stringify(assignment.constant) : assignment.constant;
    const text = expression(assignment.expression);
    return assignment.round === '' ? text : `Round(${text}, ${assignment.round})`;
  }
  function editorUrl(item, mode, ruleId = '', isNew = false) { return 'rule-maintenance.html?' + new URLSearchParams({ collection: item.id, revision: mode, listScene: detailParams(item).listScene, ...(ruleId ? { ruleId } : {}), ...(isNew ? { mode: 'new' } : {}) }); }
  function rulePanel(item, revision, editable) {
    const active = revision.rules.find(rule => rule.id === selectedRule) || revision.rules[0];
    selectedRule = active?.id;
    return `<div class="section-top"><div><h2>规则编制</h2><p class="subtle">${revision.rules.length} 条规则 · 优先级和顺序属于当前修订</p></div>${editable ? `<a class="btn small" href="${html(editorUrl(item, route.revision, '', true))}">＋ 新建规则</a>` : ''}</div>
      ${active ? `<div class="rule-layout"><div class="rule-list">${revision.rules.map(rule => `<button class="rule-card${rule.id === active.id ? ' active' : ''}" aria-pressed="${rule.id === active.id}" data-select-collection-rule="${html(rule.id)}"><span class="rule-no">优先级 ${html(rule.priority)} · 顺序 ${html(rule.order)}</span><strong>${html(rule.name)}</strong><small>${rule.enabled ? '启用' : '停用'} · ${rule.assignments.length} 项赋值</small></button>`).join('')}</div><div class="rule-editor"><h3>${html(active.name)}</h3><div class="collection-rule-source"><span class="tiny-label">命中条件</span><p>${html(source(active.conditionTree))}</p></div><span class="tiny-label">写入结果</span>${active.assignments.map(assignment => `<div class="assignment"><span class="chip">${html(variableNames[assignment.target])}</span><span class="arrow">←</span><span class="assignment-value">${html(assignmentText(assignment))}</span></div>`).join('')}<div class="editor-foot"><span>${editable ? '修改仅保存到当前草稿' : '当前修订只读'}</span><a class="btn small" href="${html(editorUrl(item, route.revision, active.id))}">${editable ? '编辑规则' : '查看规则'}</a></div></div></div>` : `<div class="collection-empty"><h3>当前修订还没有规则</h3><p>${editable ? '先添加命中条件与赋值，再配置适用范围。' : '当前修订没有规则。'}</p>${editable ? `<a class="btn" href="${html(editorUrl(item, route.revision, '', true))}">添加第一条规则</a>` : ''}</div>`}`;
  }
  function variablesPanel() {
    return `<div class="section-top"><div><h2>变量</h2><p class="subtle">输入、结果与中间变量随规则集修订保存。</p></div></div><div class="collection-table-scroll"><table class="data-table"><thead><tr><th>变量</th><th>编码</th><th>方向</th><th>类型</th></tr></thead><tbody>${Object.entries(variableNames).map(([code,name]) => `<tr><td><strong>${name}</strong></td><td class="mono">${code}</td><td>${['inputWidth','productKind'].includes(code) ? '输入' : ['factor','offset'].includes(code) ? '中间变量' : '结果'}</td><td>${({productKind:'文本',calcStatus:'文本',widthRange:'数值区间',candidateWidths:'数值列表',candidateCodes:'文本列表'})[code] || '数值'}</td></tr>`).join('')}</tbody></table></div>`;
  }
  function versionsPanel(item, mode) {
    return `<h2>修订</h2><p class="hint">每个集合独立保留草稿和线上修订。修改草稿不会写入线上快照。</p><div class="release-list" style="margin-top:16px">${['draft','published'].filter(key => item[key]).map(key => `<div class="release-card"><div><strong>${revisionName(item[key].no)} · ${key === 'draft' ? '当前草稿' : '当前线上'}</strong><p>${key === 'draft' ? '待校验与发布' : '只读快照'}</p></div>${key === mode ? '<span class="tag">正在查看</span>' : `<button class="btn small" data-revision="${key}">查看</button>`}</div>`).join('')}</div>`;
  }
  function bindingsPanel(item, revision, editable) {
    return `<div class="section-top"><div><h2>场景绑定</h2><p class="subtle">优先级配置在绑定上，用于整个场景内的规则集选择。</p></div>${editable ? rowAction('binding', item.id, '＋ 添加绑定') : ''}</div><div class="binding-list">${revision.bindings.map((binding,index) => `<div class="binding-card"><div><strong>${html(binding.name)}</strong><p>${html(binding.text)}</p></div><div class="binding-actions"><span class="tag">优先级 ${html(binding.priority)}</span>${editable ? rowAction('binding', item.id, '编辑', `data-binding="${index}"`) : ''}</div></div>`).join('') || '<div class="collection-empty"><h3>尚未设置适用范围</h3><p>添加绑定后，这个规则集才能参与场景内的运行选择。</p></div>'}</div>`;
  }
  function renderModel(item) {
    const revision = item[route.revision], editable = route.revision === 'draft' && !item.archived;
    const tabs = [['rules','规则'],['variables','变量'],['bindings','场景绑定'],['cases','门禁用例'],['versions','版本']];
    if (!tabs.some(([key]) => key === route.tab)) route.tab = 'rules';
    let content;
    if (route.tab === 'rules') content = rulePanel(item, revision, editable);
    if (route.tab === 'variables') content = variablesPanel();
    if (route.tab === 'bindings') content = bindingsPanel(item, revision, editable);
    if (route.tab === 'cases') content = `<h2>门禁用例</h2><div class="collection-empty"><h3>${revision.gateStatus === 'passed' ? '已发布修订的门禁已通过' : '草稿需要重新验证'}</h3><p>新增或修改规则后，请重新验证发布用例，再检查必需结果与适用范围。</p></div>`;
    if (route.tab === 'versions') content = versionsPanel(item, route.revision);
    $('#view-models').innerHTML = detailHeader(item, route.revision) + `<div class="workspace"><div class="main-panel"><div class="tabs" role="tablist" aria-label="规则集内容">${tabs.map(([key,label]) => `<button id="collection-tab-${key}" class="tab${route.tab === key ? ' active' : ''}" role="tab" tabindex="${route.tab === key ? 0 : -1}" aria-selected="${route.tab === key}" aria-controls="collection-tab-panel" data-collection-tab="${key}">${label}</button>`).join('')}</div><div class="tab-pane active" id="collection-tab-panel" role="tabpanel" aria-labelledby="collection-tab-${route.tab}">${content}</div></div><aside class="aside-panel"><div class="aside-block"><h3>当前查看</h3><div class="collection-revisions" style="margin-top:12px">${revisionBadges(item)}</div><p class="hint">${html(item.name)} · ${revisionName(revision.no)}</p></div><div class="aside-block"><h3>发布准备</h3><p class="hint">${editable ? '完成规则、适用范围与门禁验证后，再执行发布检查。' : '当前内容只读；在草稿中编制下一次修改。'}</p>${editable ? rowAction('publish-check', item.id, '发布检查') : ''}</div><div class="aside-block"><h3>场景内选择</h3><p class="hint">查看另一规则集不会改变运行时选择。线上执行仍依据命中的绑定与优先级。</p></div></aside></div>`;
  }
  function conditionSource(d) { return `入口宽度 ${operators[d.operator]} ${d.threshold === '' ? '…' : d.threshold} 且 产品类别 = ${d.product || '…'}`; }
  function renderCondition(item) {
    const d = item[route.revision], editable = route.revision === 'draft' && !item.archived;
    $('#view-condition-editor').innerHTML = detailHeader(item, route.revision) + `<div class="condition-workspace"><form id="condition-editor-form" class="panel condition-form"><div class="section-top"><div><h2>判定条件</h2><p class="subtle">这组条件仅维护当前候选的适用性，不配置赋值或结果变量。</p></div><span class="tag" id="condition-save-state">${editable ? '已保存' : '只读'}</span></div>
      <fieldset ${editable ? '' : 'disabled'}><legend class="sr-only">候选判定条件</legend><label class="field">判定优先级<input name="priority" type="number" step="1" min="-2147483648" max="2147483647" required value="${html(d.priority)}"><small>随修订发布后生效；数值越大越先判定</small></label>
      <div class="condition-fields"><label class="field">入口宽度<select name="operator">${[['gte','大于或等于'],['gt','大于'],['lte','小于或等于'],['lt','小于']].map(([v,t]) => option(v,t,d.operator)).join('')}</select></label><label class="field">比较值<input name="threshold" type="number" step="any" required value="${html(d.threshold)}"></label><span class="condition-and">且</span><label class="field">产品类别等于<input name="product" required maxlength="128" value="${html(d.product)}" placeholder="例如：A"></label></div></fieldset>
      <div class="collection-condition-preview"><span class="tiny-label">条件预览</span><p id="condition-source">${html(conditionSource(d))}</p></div>${editable ? '<div class="condition-save-bar"><button class="btn primary" type="submit">保存条件草稿</button></div>' : ''}</form>
      <aside class="panel condition-guidance"><h3>发布样本</h3><p>${d.gateStatus === 'passed' ? '此修订已有通过发布检查的样本。' : '当前草稿需要补充或重新验证正反样本。'}</p><p class="hint">发布前至少包含一个预期满足、一个预期不满足的样本。编辑草稿不会改变线上候选的判定结果。</p>${editable ? rowAction('publish-check', item.id, '发布检查') : ''}<div class="divider"></div><h3>在多个候选间判定</h3><p class="hint">场景列表中的“试算已发布候选”可选择候选范围，并切换全部命中或首条命中。</p></aside></div>`;
  }
  function renderRoute(focus = false) {
    route = parseRoute();
    if (!['catalog','model-list','models','conditions','condition-editor','releases'].includes(route.view)) { navigate('catalog', {}, true, true); return; }
    if (['models','condition-editor'].includes(route.view)) {
      const kind = route.view === 'models' ? 'rule' : 'condition';
      if (!route.set) { navigate(lists[kind], {}, true, true); return; }
      const id = route.set;
      let item;
      try { item = store.get(id); } catch { item = null; }
      if (!item || item.kind !== kind) {
        $(`#view-${route.view}`).innerHTML = `<div class="collection-empty"><h1 tabindex="-1">找不到这个${names[kind]}</h1><button class="btn" data-view="${lists[kind]}">返回列表</button></div>`;
      } else {
        route.set = item.id;
        route.revision = ['draft','published'].includes(route.revision) && item[route.revision] ? route.revision : item.draft && !item.archived ? 'draft' : item.published ? 'published' : 'draft';
        if (kind === 'rule') renderModel(item); else renderCondition(item);
      }
    }
    if (route.view === 'catalog') renderCatalog();
    if (route.view === 'model-list') renderList('rule');
    if (route.view === 'conditions') renderList('condition');
    $$('.screen').forEach(node => node.classList.toggle('active', node.id === `view-${route.view}`));
    const nav = route.view === 'models' ? 'model-list' : route.view === 'condition-editor' ? 'conditions' : route.view;
    $$('.nav-item').forEach(node => { node.classList.toggle('active', node.dataset.view === nav); if (node.dataset.view === nav) node.setAttribute('aria-current','page'); else node.removeAttribute('aria-current'); });
    const kind = ['conditions','condition-editor'].includes(route.view) ? 'condition' : ['model-list','models'].includes(route.view) ? 'rule' : null;
    const item = kind && route.set ? store.list(kind).find(record => record.id === route.set) : null;
    const scene = store.getScene(item?.scene || route.scene);
    const validScene = scene && scene.kind === kind ? scene : null;
    $('#crumb-current').textContent = kind ? `${validScene ? validScene.name + ' / ' : ''}${names[kind]}${item ? ' / ' + item.name : ''}` : route.view === 'catalog' ? '场景' : '发布记录';
    $('#scope-note').hidden = !validScene;
    $('#scope-note .scope-row').innerHTML = validScene ? `<span class="scope-dot"></span>${html(validScene.name)}` : '';
    if (focus) { $('.screen.active h1')?.focus({ preventScroll: true }); window.scrollTo({ top:0, behavior:'instant' }); }
  }
  function showDialog(title, description, content, confirm, submit) {
    dialogTrigger = document.activeElement;
    $('#collection-dialog-title').textContent = title;
    $('#collection-dialog-description').textContent = description;
    $('#collection-dialog-content').innerHTML = content;
    $('#collection-dialog-error').textContent = '';
    $('#collection-dialog-confirm').textContent = confirm;
    dialogSubmit = submit;
    const destructive = confirm === '确认归档' || confirm.startsWith('放弃');
    $('#collection-dialog-confirm').classList.toggle('danger', destructive);
    $('#collection-dialog').showModal();
    $(destructive ? '#collection-dialog-cancel' : '#collection-dialog-content select, #collection-dialog-content input:not([type="checkbox"]):not([type="hidden"]):not([readonly]), #collection-dialog-confirm')?.focus();
  }
  function closeDialog() { $('#collection-dialog').close(); }
  function identityFields(item) { return `<label class="field">名称<input name="name" required maxlength="256" value="${html(item?.name || '')}" autocomplete="off"></label><label class="field">编码<input name="code" required maxlength="128" value="${html(item?.code || '')}" autocomplete="off"><small>同一应用内唯一，归档后的编码也保留</small></label><label class="field">用途说明<textarea name="description" rows="2" maxlength="1000">${html(item?.description || '')}</textarea></label>`; }
  function openIdentity(kind, source) {
    const defaults = source ? { name: `${source.name} · 副本`, code: `${source.code}-COPY`, description: source.description } : null;
    const scene = store.getScene(source?.scene || route.scene);
    const sceneField = scene ? `<label class="field">所属场景<input value="${html(scene.name)}" readonly><input type="hidden" name="scene" value="${scene.code}"></label>` : `<label class="field">所属场景<select name="scene" required>${sceneOptions(kind, '', false)}</select><small>创建后在所选场景中编制和运行。</small></label>`;
    const sourceChoices = source ? `<label class="field">复制来源<select name="source">${source.draft ? option('draft',`草稿 ${revisionName(source.draft.no)}`,'draft') : ''}${source.published ? option('published',`线上 ${revisionName(source.published.no)}`,source.draft ? 'draft' : 'published') : ''}</select></label>` : '';
    showDialog(source ? '复制规则集' : `新建${names[kind]}`, source ? '在原场景内复制所选修订，创建独立规则集及首个草稿。复制后需要重新校验并发布。' : '明确所属场景，再创建集合及首个草稿。', sceneField + identityFields(defaults) + sourceChoices, source ? '创建副本' : '创建并编制', form => {
      const data = Object.fromEntries(new FormData(form));
      const item = source ? store.copy(source.id, data, data.source) : store.create(kind, data);
      const params = detailParams(item, { revision:'draft' });
      filters[kind] = { query:'', status:'active' }; rememberFilters();
      closeDialog(); navigate(editors[kind], params); toast(`${names[kind]}草稿已创建`);
    });
  }
  function openBinding(item, index) {
    const current = index === undefined ? null : item.draft.bindings[Number(index)];
    const fields = `<label class="field">绑定名称<input name="name" required value="${html(current?.name || '')}"></label><label class="field">产品类别等于<input name="product" required value="${html(current?.product || '')}" placeholder="例如：C"></label><fieldset class="candidate-choices"><legend>工艺路线属于（可多选）</legend>${['通用','精整','试验'].map(value => `<label><input type="checkbox" name="process" value="${value}"${(current?.processes || ['通用']).includes(value) ? ' checked' : ''}><span>${value}</span></label>`).join('')}</fieldset><label class="field">绑定优先级<input name="priority" type="number" step="1" min="-2147483648" max="2147483647" required value="${html(current?.priority ?? 100)}"></label>`;
    showDialog(current ? '编辑场景绑定' : '添加场景绑定', '绑定用于选择规则集。相同优先级的绑定若同时命中，将造成运行歧义。', fields, '保存绑定', form => {
      const formData = new FormData(form), data = Object.fromEntries(formData), processes = formData.getAll('process');
      if (!processes.length) throw new Error('请至少选择一条工艺路线。');
      if (!data.name.trim() || !data.product.trim()) throw new Error('请填写绑定名称和产品类别。');
      const bindings = store.clone(item.draft.bindings), binding = { name:data.name.trim(), product:data.product.trim(), processes, text:`产品类别 = ${data.product.trim()} · 工艺路线${processes.length === 1 ? ' = ' : '属于'}${processes.join('、')}`, priority:Number(data.priority) };
      if (current) bindings[Number(index)] = binding; else bindings.push(binding);
      store.saveDraft(item.id, { bindings }); closeDialog(); renderRoute(); toast('绑定已保存到当前草稿');
    });
  }
  function evaluationCandidates(scene) {
    const candidates = store.list('condition').filter(item => item.scene === scene && !item.archived && item.published);
    return candidates.map(item => `<label><input type="checkbox" name="candidate" value="${html(item.id)}" checked><span>${html(item.name)}<small>${html(item.code)} · 线上 ${revisionName(item.published.no)} · 优先级 ${item.published.priority}</small></span></label>`).join('') || `<p>${scene ? '这个场景还没有已发布的候选。' : '请先选择场景，再选择候选。'}</p>`;
  }
  function openEvaluation() {
    const fields = `<label class="field">所属场景<select name="evaluationScene" required data-evaluation-scene>${sceneOptions('condition', route.scene || '', false)}</select></label><fieldset class="candidate-choices"><legend>选择要判定的候选</legend><div id="evaluation-candidates">${evaluationCandidates(route.scene)}</div></fieldset><div class="dialog-field-grid"><label class="field">入口宽度<input name="width" type="number" step="any" required value="120"></label><label class="field">产品类别<input name="product" required value="A"></label></div><label class="field">判定方式<select name="mode"><option value="all">全部命中</option><option value="first">首条命中</option></select></label><div id="candidate-result" class="candidate-result" role="status" aria-live="polite"></div>`;
    showDialog('试算已发布候选', '每次试算限定在同一场景，只判定选中的线上候选。草稿条件不参与本次预览。', fields, '试算候选', form => {
      const data = new FormData(form), scene = store.getScene(data.get('evaluationScene'));
      if (!scene || scene.kind !== 'condition') throw new Error('请先选择需要试算的场景。');
      const ids = data.getAll('candidate');
      if (ids.some(id => store.get(id).scene !== scene.code)) throw new Error('候选必须属于所选场景。');
      const result = store.evaluate(ids, { width:data.get('width'), product:data.get('product') }, data.get('mode'));
      $('#candidate-result').textContent = result.error || (result.matches.length ? `命中：${result.matches.map(item => `${item.name}（${item.code}）`).join('、')}` : '没有候选满足条件。');
      $('#candidate-result').classList.toggle('error', !!result.error);
    });
  }
  function perform(action, button) {
    const item = button.dataset.set ? store.get(button.dataset.set) : null;
    if (action === 'new') openIdentity(button.dataset.kind);
    if (action === 'copy') openIdentity('rule', item);
    if (action === 'clear') { filters[button.dataset.kind] = { query:'', status:'active' }; rememberFilters(); renderList(button.dataset.kind); $(`[data-collection-search="${button.dataset.kind}"]`).focus(); }
    if (action === 'draft') { store.createDraft(item.id); navigate(editors[item.kind], detailParams(item, { revision:'draft' })); toast('已打开独立草稿，线上修订未受影响'); }
    if (action === 'archive') showDialog(`归档“${item.name}”？`, `${item.published ? `线上 ${revisionName(item.published.no)} 将停止参与后续运行。` : ''}草稿和已发布内容保留，归档后只读。`, `<div class="callout"><strong>${html(item.code)}</strong>请确认归档的是当前${names[item.kind]}。</div>`, '确认归档', () => { store.archive(item.id); dirtyCondition = false; filters[item.kind] = { query:'', status:'active' }; rememberFilters(); const params = [lists.rule,lists.condition].includes(route.view) ? listParams() : returnParams(); closeDialog(); navigate(lists[item.kind], params); toast('已归档，可在“已归档”中查看'); });
    if (action === 'binding') openBinding(item, button.dataset.binding);
    if (action === 'evaluate') openEvaluation();
    if (action === 'publish-check') showDialog('发布检查', '发布需要服务端完成完整校验与门禁。当前页面提供编制交互预览，尚未连接发布服务。', `<div class="callout"><strong>待完成的检查</strong>${item.kind === 'rule' ? '规则与变量类型、必需结果、场景绑定冲突、回归用例。' : '条件类型、输入快照、至少一条正样本和一条反样本。'}</div>`, '返回编制', closeDialog);
  }
  document.addEventListener('click', event => {
    const clickedMenu = event.target.closest('details.collection-menu');
    $$('details.collection-menu[open]').forEach(menu => { if (menu !== clickedMenu) menu.removeAttribute('open'); });
    const target = event.target.closest('button, a');
    if (!target) return;
    if (target.dataset.openScene) { navigate(lists[target.dataset.kind], { scene:target.dataset.openScene }); return; }
    if (target.dataset.returnList) { navigate(lists[target.dataset.returnList], returnParams()); return; }
    if (target.dataset.view) { event.preventDefault(); navigate(target.dataset.view); return; }
    if (target.dataset.openSet) { const item = store.get(target.dataset.openSet); navigate(editors[item.kind], detailParams(item, target.dataset.mode ? { revision:target.dataset.mode } : {})); return; }
    if (target.dataset.revision) { if (target.dataset.revision !== route.revision) navigate(route.view, detailParams(store.get(route.set), { revision:target.dataset.revision, ...(route.tab ? { tab:route.tab } : {}) })); return; }
    if (target.dataset.collectionTab) { navigate(route.view, detailParams(store.get(route.set), { revision:route.revision, tab:target.dataset.collectionTab })); $(`#collection-tab-${route.tab}`)?.focus(); return; }
    if (target.dataset.selectCollectionRule) { selectedRule = target.dataset.selectCollectionRule; renderModel(store.get(route.set)); $(`[data-select-collection-rule="${selectedRule}"]`)?.focus(); return; }
    if (target.dataset.collectionAction) { target.closest('details')?.removeAttribute('open'); try { perform(target.dataset.collectionAction, target); } catch (error) { toast(error.message); } return; }
    if (target.id === 'collection-dialog-cancel') closeDialog();
    if (target.dataset.action) showDialog('历史发布操作', '历史修订和回滚需要读取服务端的发布记录。此入口当前仅提供流程说明。', '', '关闭', closeDialog);
  });
  document.addEventListener('input', event => {
    const node = event.target;
    if (node.hasAttribute('data-scene-search')) { catalogQuery = node.value; renderCatalogGroups(); }
    if (node.dataset.collectionSearch) { filters[node.dataset.collectionSearch].query = node.value; rememberFilters(); renderRows(node.dataset.collectionSearch); }
    if (node.closest('#condition-editor-form')) {
      node.setCustomValidity?.('');
      dirtyCondition = true; $('#condition-save-state').textContent = '未保存';
      const data = Object.fromEntries(new FormData($('#condition-editor-form'))); $('#condition-source').textContent = conditionSource(data);
    }
    if (node.closest('#collection-form')) $('#collection-dialog-error').textContent = '';
  });
  document.addEventListener('change', event => {
    const node = event.target;
    if (node.dataset.listScene) navigate(lists[node.dataset.listScene], node.value ? { scene:node.value } : {});
    if (node.hasAttribute('data-evaluation-scene')) { $('#evaluation-candidates').innerHTML = evaluationCandidates(node.value); $('#candidate-result').textContent = ''; $('#collection-dialog-error').textContent = ''; }
    if (node.dataset.collectionFilter) { filters[node.dataset.collectionFilter].status = node.value; rememberFilters(); renderRows(node.dataset.collectionFilter); }
    if (node.dataset.switchSet) { const id = node.value; node.value = route.set; if (id !== route.set) navigate(editors[node.dataset.switchSet], detailParams(store.get(id))); }
  });
  document.addEventListener('submit', event => {
    if (event.target.id === 'collection-form') {
      event.preventDefault();
      if (!event.target.reportValidity()) return;
      try { dialogSubmit?.(event.target); } catch (error) { $('#collection-dialog-error').textContent = error.message; }
    }
    if (event.target.id === 'condition-editor-form') {
      event.preventDefault(); const data = Object.fromEntries(new FormData(event.target));
      if (!data.product.trim()) { event.target.elements.product.setCustomValidity('请填写产品类别。'); event.target.elements.product.reportValidity(); return; }
      data.priority = Number(data.priority); data.threshold = Number(data.threshold);
      store.saveDraft(route.set, data); dirtyCondition = false; renderRoute(); toast('条件已保存到当前草稿');
    }
  });
  $('#collection-dialog').addEventListener('close', () => { dialogSubmit = null; if (dialogTrigger?.isConnected) dialogTrigger.focus(); else $('.screen.active h1')?.focus({ preventScroll:true }); });
  $('#collection-dialog').addEventListener('click', event => { if (event.target === $('#collection-dialog')) closeDialog(); });
  window.addEventListener('hashchange', () => {
    if (dirtyCondition) { const requested = parseRoute(); history.replaceState(null,'',address(route.view,Object.fromEntries(Object.entries(route).filter(([key]) => key !== 'view')))); navigate(requested.view,Object.fromEntries(Object.entries(requested).filter(([key]) => key !== 'view'))); }
    else renderRoute(true);
  });
  document.addEventListener('keydown', event => {
    const tab = event.target.closest('[data-collection-tab]');
    if (tab && ['ArrowLeft','ArrowRight','Home','End'].includes(event.key)) {
      event.preventDefault();
      const tabs = $$('[data-collection-tab]'), position = tabs.indexOf(tab);
      const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : (position + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
      const key = tabs[next].dataset.collectionTab;
      navigate(route.view,detailParams(store.get(route.set), {revision:route.revision,tab:key}));
      $(`#collection-tab-${key}`).focus();
    }
    if (event.key === 'Escape' && !$('#collection-dialog').open) {
      const menu = $('details.collection-menu[open]');
      if (menu) { menu.removeAttribute('open'); menu.querySelector('summary').focus(); }
    }
  });
  window.addEventListener('beforeunload', event => { if (dirtyCondition) { event.preventDefault(); event.returnValue = ''; } });
  renderRoute();
})();

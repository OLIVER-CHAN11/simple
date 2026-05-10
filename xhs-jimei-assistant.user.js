// ==UserScript==
// @name         小红书集美租房截流助手
// @namespace    https://github.com/OLIVER-CHAN11/simple
// @version      1.1.0
// @description  辅助识别小红书评论区中的集美区租房需求，生成评论回复和私信话术，一键填入/复制。不自动发送任何内容，账号零风险。
// @author       you
// @match        *://*.xiaohongshu.com/*
// @grant        GM_setValue
// @grant        GM_getValue
// @grant        GM_addStyle
// @run-at       document-idle
// @license      MIT
// ==/UserScript==

(function () {
  'use strict';

  // ============================================================
  // 关键词词库（可自行增删）
  // ============================================================

  // 地区关键词
  const KW_AREA = [
    '集美', '集美区', '杏林', '灌口', '后溪', '侨英', '杏滨',
    '集美学村', '诚毅学院', '华侨大学', '集美大学',
    '软件园三期', '园博苑', '嘉庚体育馆', '集美万达', '龙舟池',
  ];

  // 租房意图关键词（强信号）
  const KW_INTENT_STRONG = [
    '有房吗', '有没有房', '还有房吗', '房子还有吗',
  ];
  const KW_INTENT = [
    '求租', '想租', '租房', '找房', '怎么租', '哪里租',
    '可以租吗', '短租', '长租', '整租', '合租',
  ];

  // 房型关键词
  const KW_ROOM = [
    '一室一厅', '一房一厅', '单间', '大单间',
    '两室一厅', '两房一厅', '三室', '公寓', '套房', '独卫', '带阳台',
  ];

  // 疑问关键词
  const KW_QUESTION = [
    '有吗', '有没有', '还有吗', '多少', '多少钱',
    '怎么租', '在哪里', '可以吗',
  ];

  // 时间关键词
  const KW_TIME = [
    '近期', '马上', '月底', '这个月', '下个月',
    '现在住', '这两天', '最近',
  ];

  // 负面 / 排除关键词（房东、中介、同行）
  const KW_NEGATIVE = [
    '我有房', '出租', '转租', '房东直租', '中介勿扰',
    '招租', '房源', '带看', '私我看房',
  ];

  // 导航 / 按钮文案黑名单（精确匹配，短文本噪音过滤）
  const UI_NOISE = new Set([
    '登录', '注册', '首页', '发现', '关注', '消息', '搜索', '更多',
    '查看', '回复', '点赞', '收藏', '分享', '举报', '购物车',
    '全部', '最新', '最热', '商品', '笔记', '视频', '用户',
    '展开', '收起', '确定', '取消', '提交', '发送', '完成',
  ]);

  // ============================================================
  // 存储
  // ============================================================

  const STORAGE_KEY = 'jm_assistant_leads_v1';

  function loadLeads() {
    try { return JSON.parse(GM_getValue(STORAGE_KEY, '[]')) || []; }
    catch { return []; }
  }
  function saveLeads(arr) {
    GM_setValue(STORAGE_KEY, JSON.stringify(arr));
  }

  let leads = loadLeads();

  // 内存去重集：同一条评论文本在本页面扫描期间不重复添加
  const pageSeenTexts = new Set();
  // 已持久化过的 id 集合
  const persistedIds = new Set(leads.map(l => l.id));

  // ============================================================
  // 评分逻辑
  // ============================================================

  function containsAny(text, words) {
    return words.filter(w => text.indexOf(w) !== -1);
  }

  // 预算相关匹配：数字（3-5 位）+ 元/块/月，或"预算"/"一个月"
  function matchBudget(text) {
    const hits = [];
    if (/预算|元\/?\s*月|一个月|月租/.test(text)) hits.push('预算');
    // 数字预算，如 "1500"、"2000以内"、"1500-2000"
    const m = text.match(/(\d{3,5})(?:\s*[-~到至]\s*\d{3,5})?\s*(?:元|块|一个月|\/月)?/);
    if (m && parseInt(m[1]) >= 500 && parseInt(m[1]) <= 20000) {
      hits.push(m[0].trim());
    }
    return hits;
  }

  function scoreText(text) {
    let score = 0;
    const matched = [];

    // 1. 强租房意图：+5
    const strong = containsAny(text, KW_INTENT_STRONG);
    if (strong.length) { score += 5; matched.push(...strong); }

    // 2. 普通租房意图：+4
    const intent = containsAny(text, KW_INTENT);
    if (intent.length) { score += 4; matched.push(...intent); }

    // 3. 地区关键词：+3
    const area = containsAny(text, KW_AREA);
    if (area.length) { score += 3; matched.push(...area); }

    // 4. 房型关键词：+3
    const room = containsAny(text, KW_ROOM);
    if (room.length) { score += 3; matched.push(...room); }

    // 5. 疑问关键词：+2
    const q = containsAny(text, KW_QUESTION);
    if (q.length) { score += 2; matched.push(...q); }

    // 6. 预算：+2
    const budget = matchBudget(text);
    if (budget.length) { score += 2; matched.push(...budget); }

    // 7. 时间关键词：+2
    const t = containsAny(text, KW_TIME);
    if (t.length) { score += 2; matched.push(...t); }

    // 8. 负面关键词：-5（一旦命中大概率是房东/中介）
    const neg = containsAny(text, KW_NEGATIVE);
    if (neg.length) { score -= 5; matched.push(...neg.map(n => '⚠' + n)); }

    // 去重关键词列表
    const matchedUniq = [...new Set(matched)];

    let level = 'C';
    if (score >= 9) level = 'S';
    else if (score >= 6) level = 'A';
    else if (score >= 3) level = 'B';

    return { score, level, matched: matchedUniq };
  }

  // ============================================================
  // 话术生成
  // ============================================================
  // 按"从具体到泛化"的顺序匹配，命中第一条即停

  function genCommentReply(text) {
    if (/一室一厅|一房一厅/.test(text)) {
      return '集美一室一厅要看位置和预算，你想找哪一块？预算大概多少？';
    }
    if (/软件园三期/.test(text)) {
      return '软件园三期附近有一些选择，你预算大概多少？更想近一点还是性价比高一点？';
    }
    if (/集美大学|华侨大学|诚毅学院/.test(text)) {
      return '学校附近要看预算和入住时间，你想找单间还是一室一厅？';
    }
    if (/集美/.test(text)) {
      return '集美这边有的，你想找哪个位置附近？预算大概多少？';
    }
    if (/预算/.test(text)) {
      return '这个预算可以先看位置和房型，你更想住近一点还是房子舒服一点？';
    }
    return '你想找哪个位置附近？预算和入住时间大概是什么时候？';
  }

  function genDmReply(text) {
    if (/一室一厅|一房一厅/.test(text)) {
      return '你好，看到你在找集美一室一厅，想问下你预算大概多少？是自己住还是两个人住？大概什么时候入住？';
    }
    if (/软件园三期/.test(text)) {
      return '你好，看到你在问软件园三期附近租房，你是想通勤近一点，还是预算低一点？预算大概多少？';
    }
    if (/集美大学|华侨大学|诚毅学院/.test(text)) {
      return '你好，看到你在找学校附近的房子，你是想找单间、合租还是一室一厅？预算大概多少？';
    }
    if (/集美/.test(text)) {
      return '你好，看到你在找集美这边的房子，你想找哪一块？预算、房型和入住时间大概是什么？';
    }
    return '你好，看到你在评论区问租房，想问下你想找哪个位置、什么房型、预算多少？我可以先帮你看看合适范围。';
  }

  // ============================================================
  // 页面文本扫描
  // ============================================================

  // 判断某个节点是否位于 UI 噪音容器内（按钮、导航、脚本面板自身等）
  function isInNoiseAncestor(el) {
    // 直接跳过脚本面板内部元素
    if (el.closest('#jm-assistant')) return true;
    // 常见 UI 容器
    const noisy = el.closest(
      'button, a[role="button"], nav, header, aside,' +
      '[class*="tab"], [class*="menu"], [class*="btn"], [class*="nav"]'
    );
    if (noisy) {
      // 如果是"tab"容器但里面就是评论列表，别误杀 —— 只在短文本时才认为是 UI
      const t = (el.innerText || '').trim();
      if (t.length < 8) return true;
    }
    return false;
  }

  // 取该元素"最贴近叶子"的可见文本（避免父 div 吃掉整块文本）
  function getLeafText(el) {
    // 如果本身有多个子元素并且总文本很长，可能是容器，跳过交给更深的节点
    const text = (el.innerText || '').trim();
    if (!text) return '';
    // 有子元素 text 完全相同的更深层节点，让它们各自处理
    // 这里先简单返回 text
    return text;
  }

  function scanPage() {
    const nodes = document.querySelectorAll('div, span, p');
    const newLeads = [];

    nodes.forEach(el => {
      if (isInNoiseAncestor(el)) return;
      if (el.dataset._jmaScanned === '1') return;

      const text = getLeafText(el);
      if (!text) return;

      // 长度过滤：2 - 100
      if (text.length < 2 || text.length > 100) return;

      // 重复文本跳过（本次扫描内）
      if (pageSeenTexts.has(text)) return;

      // UI 噪音黑名单
      if (UI_NOISE.has(text.replace(/\s+/g, ''))) return;

      // 纯数字 / 纯表情 / 无中文 直接跳过
      if (!/[\u4e00-\u9fa5]/.test(text)) return;

      // 打分
      const { score, level, matched } = scoreText(text);

      pageSeenTexts.add(text);
      el.dataset._jmaScanned = '1';

      // 只展示 S / A / B
      if (level === 'C') return;

      const id = hashStr(location.pathname + '|' + text);

      // 高亮对应元素
      el.classList.add('jma-hit-' + level.toLowerCase());
      el.setAttribute('data-jma-id', id);

      // 已持久化过的跳过，只把它显示在面板（重新扫描时）
      if (persistedIds.has(id)) return;

      const lead = {
        id,
        platform: '小红书',
        username: '', // 小红书 DOM 变化较大，首版不强行抓用户名，留空
        commentText: text,
        pageUrl: location.href,
        score,
        level,
        matchedKeywords: matched,
        commentReply: genCommentReply(text),
        dmReply: genDmReply(text),
        status: '待处理',
        createdAt: Date.now(),
      };
      newLeads.push(lead);
      persistedIds.add(id);
    });

    if (newLeads.length) {
      // 新线索追加在前
      leads = [...newLeads, ...leads];
      // 限制本地存储规模，避免无限增长
      if (leads.length > 1000) leads = leads.slice(0, 1000);
      saveLeads(leads);
    }

    return newLeads.length;
  }

  // ============================================================
  // UI 面板
  // ============================================================

  GM_addStyle(`
    #jm-assistant {
      position: fixed; top: 80px; right: 16px; z-index: 999999;
      width: 380px; height: 85vh; max-height: 85vh;
      background: #fff; border: 1px solid #e5e7eb; border-radius: 10px;
      box-shadow: 0 10px 30px rgba(0,0,0,0.15);
      font-family: -apple-system, "PingFang SC", "Microsoft YaHei", sans-serif;
      font-size: 13px; color: #111827;
      display: flex; flex-direction: column;
      overflow: hidden;
    }
    #jm-assistant.collapsed { width: 160px; height: auto; }
    #jm-assistant.collapsed .jma-body { display: none; }
    #jm-assistant .jma-body {
      flex: 1; min-height: 0;
      display: flex; flex-direction: column;
    }
    #jm-assistant .jma-header {
      flex-shrink: 0;
      padding: 10px 12px;
      background: linear-gradient(90deg, #ff2442, #ff5b5b);
      color: #fff; border-radius: 10px 10px 0 0;
      display: flex; align-items: center; justify-content: space-between;
      cursor: move; user-select: none;
    }
    #jm-assistant.collapsed .jma-header { border-radius: 10px; }
    #jm-assistant .jma-title { font-weight: 600; font-size: 14px; }
    #jm-assistant .jma-collapse {
      background: rgba(255,255,255,0.2); border: none; color: #fff;
      width: 24px; height: 24px; border-radius: 4px; cursor: pointer;
    }
    #jm-assistant .jma-toolbar {
      flex-shrink: 0;
      padding: 8px 12px; display: flex; gap: 6px; flex-wrap: wrap;
      border-bottom: 1px solid #f3f4f6;
    }
    #jm-assistant .jma-btn {
      flex: 1; min-width: 70px;
      border: 1px solid #e5e7eb; background: #fff; color: #374151;
      padding: 6px 8px; border-radius: 5px; font-size: 12px;
      cursor: pointer; transition: all 0.15s;
    }
    #jm-assistant .jma-btn:hover { border-color: #ff2442; color: #ff2442; }
    #jm-assistant .jma-btn.primary {
      background: #ff2442; color: #fff; border-color: #ff2442;
    }
    #jm-assistant .jma-btn.primary:hover { background: #e91e3a; color: #fff; }
    #jm-assistant .jma-btn.danger { color: #dc2626; }

    #jm-assistant .jma-stats {
      flex-shrink: 0;
      padding: 6px 12px; font-size: 11px; color: #6b7280;
      background: #fafafa; border-bottom: 1px solid #f3f4f6;
      display: flex; justify-content: space-between;
    }

    #jm-assistant .jma-list {
      flex: 1 1 auto; min-height: 0;
      overflow-y: auto; overflow-x: hidden;
      padding: 8px 12px;
      overscroll-behavior: contain;
    }
    #jm-assistant .jma-empty {
      text-align: center; color: #9ca3af; padding: 32px 0; font-size: 12px;
    }

    .jma-card {
      border: 1px solid #e5e7eb; border-radius: 8px;
      padding: 10px; margin-bottom: 10px; background: #fff;
      transition: opacity 0.2s;
    }
    .jma-card.status-忽略 { opacity: 0.45; }
    .jma-card .c-meta {
      display: flex; align-items: center; gap: 6px;
      font-size: 11px; color: #6b7280; margin-bottom: 6px;
    }
    .jma-card .c-level {
      font-weight: 600; padding: 1px 6px; border-radius: 4px;
      color: #fff; font-size: 11px;
    }
    .jma-card .c-level.S { background: #ef4444; }
    .jma-card .c-level.A { background: #f59e0b; }
    .jma-card .c-level.B { background: #3b82f6; }
    .jma-card .c-status {
      margin-left: auto; padding: 1px 6px; border-radius: 4px;
      background: #f3f4f6; font-size: 10px; color: #6b7280;
    }
    .jma-card .c-status.已评论 { background: #dbeafe; color: #1e40af; }
    .jma-card .c-status.已私信 { background: #e9d5ff; color: #6b21a8; }
    .jma-card .c-status.已回复 { background: #d1fae5; color: #065f46; }
    .jma-card .c-status.忽略 { background: #f3f4f6; color: #6b7280; }

    .jma-card .c-text {
      background: #fffbeb; padding: 6px 8px; border-radius: 5px;
      font-size: 13px; line-height: 1.5; color: #111827;
      margin-bottom: 6px; word-break: break-all;
    }
    .jma-card .c-text mark {
      background: #fde047; padding: 0 2px; border-radius: 2px;
    }
    .jma-card .c-text mark.neg { background: #fecaca; }
    .jma-card .c-kw {
      font-size: 11px; color: #6b7280; margin-bottom: 8px;
    }
    .jma-card .c-kw .chip {
      display: inline-block; padding: 1px 6px; margin-right: 4px;
      background: #fef3c7; border-radius: 8px; color: #92400e;
    }
    .jma-card .c-kw .chip.neg { background: #fee2e2; color: #991b1b; }

    .jma-card .c-script-block {
      border: 1px dashed #e5e7eb; border-radius: 5px;
      padding: 6px 8px; margin-bottom: 6px; font-size: 12px;
      line-height: 1.5; color: #374151; background: #fafafa;
    }
    .jma-card .c-script-block .c-script-label {
      font-size: 10px; color: #9ca3af; margin-bottom: 2px;
      display: flex; justify-content: space-between; align-items: center;
    }
    .jma-card .c-script-block .c-mini-copy {
      font-size: 10px; padding: 1px 6px; border: 1px solid #d1d5db;
      background: #fff; border-radius: 3px; cursor: pointer; color: #374151;
    }
    .jma-card .c-script-block .c-mini-copy:hover { border-color: #ff2442; color: #ff2442; }
    .jma-card .c-script-block .c-mini-primary {
      background: #ff2442; color: #fff; border-color: #ff2442;
      margin-right: 4px;
    }
    .jma-card .c-script-block .c-mini-primary:hover { background: #e91e3a; color: #fff; border-color: #e91e3a; }

    .jma-card .c-actions {
      display: grid; grid-template-columns: repeat(3, 1fr);
      gap: 4px; margin-top: 6px;
    }
    .jma-card .c-actions .jma-btn {
      font-size: 11px; padding: 4px 6px;
    }

    /* 页面内高亮 */
    .jma-hit-s, .jma-hit-a, .jma-hit-b {
      outline-offset: 2px !important;
      border-radius: 4px !important;
      transition: outline 0.2s;
    }
    .jma-hit-s { outline: 2px solid #ef4444 !important; background: #fef3c7 !important; }
    .jma-hit-a { outline: 2px solid #f59e0b !important; background: #fefce8 !important; }
    .jma-hit-b { outline: 1px dashed #3b82f6 !important; }

    /* toast */
    .jma-toast {
      position: fixed; bottom: 30px; left: 50%;
      transform: translateX(-50%);
      background: rgba(0,0,0,0.8); color: #fff;
      padding: 8px 16px; border-radius: 20px;
      z-index: 9999999; font-size: 13px;
    }
  `);

  const panel = document.createElement('div');
  panel.id = 'jm-assistant';
  panel.innerHTML = `
    <div class="jma-header" id="jma-header">
      <span class="jma-title">🏠 集美租房截流助手</span>
      <button class="jma-collapse" id="jma-collapse" title="折叠">—</button>
    </div>
    <div class="jma-body">
      <div class="jma-toolbar">
        <button class="jma-btn primary" id="jma-rescan">重新扫描</button>
        <button class="jma-btn" id="jma-export">导出 CSV</button>
        <button class="jma-btn danger" id="jma-clear-page">清空本页结果</button>
      </div>
      <div class="jma-stats" id="jma-stats"></div>
      <div class="jma-list" id="jma-list"></div>
    </div>
  `;
  document.body.appendChild(panel);

  // ============================================================
  // 事件绑定
  // ============================================================

  panel.querySelector('#jma-collapse').onclick = () => {
    panel.classList.toggle('collapsed');
    panel.querySelector('#jma-collapse').textContent =
      panel.classList.contains('collapsed') ? '▢' : '—';
  };

  panel.querySelector('#jma-rescan').onclick = () => {
    const n = scanPage();
    toast(`扫描完成：新增 ${n} 条线索`);
    render();
  };

  panel.querySelector('#jma-export').onclick = exportCSV;

  panel.querySelector('#jma-clear-page').onclick = () => {
    const pageUrl = location.href;
    const before = leads.length;
    if (!confirm('将清空当前页面（同一链接下）的所有线索记录，确定？')) return;
    leads = leads.filter(l => l.pageUrl !== pageUrl);
    saveLeads(leads);
    // 同步清理内存集合和 DOM 标记
    persistedIds.clear();
    leads.forEach(l => persistedIds.add(l.id));
    pageSeenTexts.clear();
    document.querySelectorAll('[data-_jma-scanned="1"]').forEach(el => {
      el.removeAttribute('data-_jma-scanned');
    });
    document.querySelectorAll('.jma-hit-s, .jma-hit-a, .jma-hit-b').forEach(el => {
      el.classList.remove('jma-hit-s', 'jma-hit-a', 'jma-hit-b');
    });
    toast(`已清空 ${before - leads.length} 条本页线索`);
    render();
  };

  // 拖拽
  makeDraggable(panel, panel.querySelector('#jma-header'));

  // ============================================================
  // 渲染
  // ============================================================

  function render() {
    renderStats();
    renderList();
  }

  function renderStats() {
    const pageLeads = leads.filter(l => l.pageUrl === location.href);
    const s = pageLeads.filter(l => l.level === 'S').length;
    const a = pageLeads.filter(l => l.level === 'A').length;
    const b = pageLeads.filter(l => l.level === 'B').length;
    const pending = pageLeads.filter(l => l.status === '待处理').length;
    panel.querySelector('#jma-stats').innerHTML = `
      <span>本页 S <b style="color:#ef4444">${s}</b> · A <b style="color:#f59e0b">${a}</b> · B <b style="color:#3b82f6">${b}</b></span>
      <span>待处理 <b>${pending}</b> · 全站 <b>${leads.length}</b></span>
    `;
  }

  function renderList() {
    const wrap = panel.querySelector('#jma-list');
    // 本页的 S/A/B 线索，按等级+分数排序，已处理的排后面
    const pageLeads = leads
      .filter(l => l.pageUrl === location.href)
      .sort((x, y) => {
        const statusOrder = { '待处理': 0, '已评论': 1, '已私信': 2, '已回复': 3, '忽略': 4 };
        if (statusOrder[x.status] !== statusOrder[y.status]) {
          return statusOrder[x.status] - statusOrder[y.status];
        }
        const levelOrder = { S: 0, A: 1, B: 2 };
        if (levelOrder[x.level] !== levelOrder[y.level]) {
          return levelOrder[x.level] - levelOrder[y.level];
        }
        return y.score - x.score;
      });

    if (!pageLeads.length) {
      wrap.innerHTML = `
        <div class="jma-empty">
          当前页面还没扫出线索<br/><br/>
          打开一篇小红书笔记，滚动加载评论后点<br/>"重新扫描"
        </div>`;
      return;
    }

    wrap.innerHTML = pageLeads.map(renderCard).join('');

    // 绑定卡片内按钮
    wrap.querySelectorAll('[data-lead-act]').forEach(btn => {
      btn.onclick = (e) => {
        e.preventDefault();
        const act = btn.dataset.leadAct;
        const id = btn.dataset.leadId;
        const lead = leads.find(l => l.id === id);
        if (!lead) return;
        handleAction(act, lead);
      };
    });
  }

  function renderCard(lead) {
    const kwChips = (lead.matchedKeywords || []).map(k => {
      const isNeg = k.startsWith('⚠');
      return `<span class="chip ${isNeg ? 'neg' : ''}">${escapeHtml(k)}</span>`;
    }).join('');

    const textHtml = highlightKeywords(escapeHtml(lead.commentText), lead.matchedKeywords);

    return `
      <div class="jma-card status-${lead.status}">
        <div class="c-meta">
          <span class="c-level ${lead.level}">${lead.level}</span>
          <span>分数 ${lead.score}</span>
          ${lead.username ? `<span>@${escapeHtml(lead.username)}</span>` : ''}
          <span class="c-status ${lead.status}">${lead.status}</span>
        </div>
        <div class="c-text">${textHtml}</div>
        ${kwChips ? `<div class="c-kw">${kwChips}</div>` : ''}

        <div class="c-script-block">
          <div class="c-script-label">
            <span>💬 评论区回复话术</span>
            <span>
              <button class="c-mini-copy c-mini-primary" data-lead-act="fill-comment" data-lead-id="${lead.id}" title="滚到评论框并填入话术（不自动发送，你自己按发送）">📝 填入</button>
              <button class="c-mini-copy" data-lead-act="copy-comment" data-lead-id="${lead.id}">复制</button>
            </span>
          </div>
          <div>${escapeHtml(lead.commentReply)}</div>
        </div>
        <div class="c-script-block">
          <div class="c-script-label">
            <span>📨 私信开场话术</span>
            <button class="c-mini-copy" data-lead-act="copy-dm" data-lead-id="${lead.id}">复制</button>
          </div>
          <div>${escapeHtml(lead.dmReply)}</div>
        </div>

        <div class="c-actions">
          <button class="jma-btn" data-lead-act="mark-comment" data-lead-id="${lead.id}">✅已评论</button>
          <button class="jma-btn" data-lead-act="mark-dm" data-lead-id="${lead.id}">✅已私信</button>
          <button class="jma-btn" data-lead-act="mark-reply" data-lead-id="${lead.id}">✅已回复</button>
          <button class="jma-btn" data-lead-act="locate" data-lead-id="${lead.id}">定位</button>
          <button class="jma-btn" data-lead-act="reset" data-lead-id="${lead.id}">↺重置</button>
          <button class="jma-btn danger" data-lead-act="ignore" data-lead-id="${lead.id}">✕忽略</button>
        </div>
      </div>
    `;
  }

  function handleAction(act, lead) {
    switch (act) {
      case 'copy-comment':
        copyToClipboard(lead.commentReply);
        toast('评论回复已复制');
        break;
      case 'copy-dm':
        copyToClipboard(lead.dmReply);
        toast('私信话术已复制');
        break;
      case 'fill-comment':
        // 同时把话术塞到剪贴板（兜底，万一填入失败用户可以手动粘贴）
        copyToClipboard(lead.commentReply);
        fillCommentBox(lead);
        break;
      case 'mark-comment':
        lead.status = '已评论';
        saveLeads(leads);
        toast('已标记：已评论');
        render();
        break;
      case 'mark-dm':
        lead.status = '已私信';
        saveLeads(leads);
        toast('已标记：已私信');
        render();
        break;
      case 'mark-reply':
        lead.status = '已回复';
        saveLeads(leads);
        toast('已标记：已回复');
        render();
        break;
      case 'ignore':
        lead.status = '忽略';
        saveLeads(leads);
        toast('已忽略');
        render();
        break;
      case 'reset':
        lead.status = '待处理';
        saveLeads(leads);
        render();
        break;
      case 'locate': {
        const el = document.querySelector(`[data-jma-id="${lead.id}"]`);
        if (el) {
          el.scrollIntoView({ behavior: 'smooth', block: 'center' });
          el.style.transition = 'outline 0.3s';
          const orig = el.style.outline;
          el.style.outline = '3px solid #ff2442';
          setTimeout(() => el.style.outline = orig, 2000);
          toast('已定位到页面');
        } else {
          toast('此条已不在当前 DOM 中');
        }
        break;
      }
    }
  }

  // ============================================================
  // 一键填入评论框（不自动发送）
  // ============================================================
  // 安全边界：
  // - 不做任何 click/keyboard 事件合成
  // - 不调用任何发送按钮
  // - 只滚动定位 + 聚焦 + 写入文字
  // - 用户自己检查内容后自己按"发送"

  function findCommentInput() {
    // 小红书评论输入框可能的形态（按命中概率排序）
    const selectors = [
      // 新版主评论输入
      '#content-textarea',
      '.content-input',
      '.comment-input',
      // 通用 contenteditable
      '[contenteditable="true"][data-placeholder]',
      '[contenteditable="true"][placeholder]',
      '[contenteditable="true"].content-edit',
      // 常见输入框 class 模糊匹配
      '[class*="comment"] [contenteditable="true"]',
      '[class*="CommentInput"] [contenteditable="true"]',
      '[class*="comment-input"] textarea',
      '[class*="reply"] [contenteditable="true"]',
      // 兜底：任何可见 contenteditable
      'div[contenteditable="true"]',
      'textarea',
    ];
    for (const sel of selectors) {
      const nodes = document.querySelectorAll(sel);
      for (const n of nodes) {
        // 跳过脚本面板自己
        if (n.closest('#jm-assistant')) continue;
        // 必须可见
        const r = n.getBoundingClientRect();
        if (r.width < 30 || r.height < 10) continue;
        if (getComputedStyle(n).visibility === 'hidden') continue;
        if (getComputedStyle(n).display === 'none') continue;
        return n;
      }
    }
    return null;
  }

  // 往 contenteditable 里写入文字，触发 React/Vue 的 input 事件让组件感知到
  function setEditorText(el, text) {
    if (el.tagName === 'TEXTAREA' || el.tagName === 'INPUT') {
      // 用原生 setter 保证 React 能收到变更
      const proto = el.tagName === 'TEXTAREA'
        ? window.HTMLTextAreaElement.prototype
        : window.HTMLInputElement.prototype;
      const setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
      setter.call(el, text);
      el.dispatchEvent(new Event('input', { bubbles: true }));
      el.dispatchEvent(new Event('change', { bubbles: true }));
      return;
    }
    // contenteditable
    el.focus();
    // 清空原有内容（如果是占位符）
    el.innerHTML = '';
    // 插入文本节点
    const tn = document.createTextNode(text);
    el.appendChild(tn);
    // 把光标移到最后
    try {
      const range = document.createRange();
      const sel = window.getSelection();
      range.selectNodeContents(el);
      range.collapse(false);
      sel.removeAllRanges();
      sel.addRange(range);
    } catch (e) {}
    // 触发多种事件让 React 组件同步状态
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new InputEvent('input', { bubbles: true, data: text, inputType: 'insertText' }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  }

  function fillCommentBox(lead) {
    let editor = findCommentInput();
    if (!editor) {
      // 有的页面评论框在笔记详情侧边栏里，如果没找到提示用户点一下"说点什么"
      toast('没找到评论输入框，请先手动点击"说点什么"再试');
      return;
    }
    // 滚到可见
    editor.scrollIntoView({ behavior: 'smooth', block: 'center' });
    // 小延迟确保滚动结束
    setTimeout(() => {
      try {
        setEditorText(editor, lead.commentReply);
        // 聚焦让用户看见
        editor.focus();
        // 闪烁一下边框提示"已填入,请检查后按发送"
        const origOutline = editor.style.outline;
        editor.style.transition = 'outline 0.2s';
        editor.style.outline = '3px solid #ff2442';
        setTimeout(() => editor.style.outline = origOutline, 1500);
        toast('已填入评论框,请检查后自己按发送键');
      } catch (err) {
        console.error('[集美助手] 填入失败', err);
        toast('填入失败,话术已复制到剪贴板,请手动粘贴');
      }
    }, 300);
  }


  // ============================================================
  // 导出 CSV
  // ============================================================

  function exportCSV() {
    if (!leads.length) { toast('暂无数据'); return; }
    const headers = [
      '时间', '平台', '等级', '分数', '评论内容',
      '命中关键词', '评论回复话术', '私信话术', '状态', '页面链接',
    ];
    const rows = leads.map(l => [
      new Date(l.createdAt).toLocaleString('zh-CN'),
      l.platform,
      l.level,
      l.score,
      l.commentText,
      (l.matchedKeywords || []).join(';'),
      l.commentReply,
      l.dmReply,
      l.status,
      l.pageUrl,
    ]);
    const csv = [headers, ...rows].map(r =>
      r.map(c => `"${String(c == null ? '' : c).replace(/"/g, '""')}"`).join(',')
    ).join('\n');
    // 加 BOM 保证 Excel 识别中文
    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `集美租房截流线索_${fmtFile(Date.now())}.csv`;
    document.body.appendChild(a);
    a.click();
    setTimeout(() => {
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    }, 200);
  }

  // ============================================================
  // 工具函数
  // ============================================================

  function hashStr(s) {
    let h = 0;
    for (let i = 0; i < s.length; i++) h = ((h << 5) - h + s.charCodeAt(i)) | 0;
    return 'jma_' + (h >>> 0).toString(36);
  }

  function escapeHtml(s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, c => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
  }

  function escapeRegex(s) { return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); }

  function highlightKeywords(html, keywords) {
    if (!keywords || !keywords.length) return html;
    let out = html;
    // 按长度降序，避免短词破坏长词
    const sorted = [...new Set(keywords)].sort((a, b) => b.length - a.length);
    for (const k of sorted) {
      const isNeg = k.startsWith('⚠');
      const pureK = isNeg ? k.slice(1) : k;
      if (!pureK) continue;
      const re = new RegExp(escapeRegex(pureK), 'g');
      out = out.replace(re, m => `<mark class="${isNeg ? 'neg' : ''}">${m}</mark>`);
    }
    return out;
  }

  function fmtFile(ts) {
    const d = new Date(ts);
    const pad = n => String(n).padStart(2, '0');
    return `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}_${pad(d.getHours())}${pad(d.getMinutes())}`;
  }

  function copyToClipboard(text) {
    // 优先用 navigator.clipboard，fallback 到 textarea
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(() => fallbackCopy(text));
    } else {
      fallbackCopy(text);
    }
  }
  function fallbackCopy(text) {
    const ta = document.createElement('textarea');
    ta.value = text;
    ta.style.cssText = 'position:fixed;opacity:0;pointer-events:none;';
    document.body.appendChild(ta);
    ta.select();
    try { document.execCommand('copy'); } catch (e) {}
    document.body.removeChild(ta);
  }

  function toast(msg) {
    const t = document.createElement('div');
    t.className = 'jma-toast';
    t.textContent = msg;
    document.body.appendChild(t);
    setTimeout(() => t.remove(), 1800);
  }

  function makeDraggable(el, handle) {
    let ox = 0, oy = 0, sx = 0, sy = 0, dragging = false;
    handle.addEventListener('mousedown', e => {
      if (e.target.tagName === 'BUTTON') return;
      dragging = true;
      const r = el.getBoundingClientRect();
      ox = r.left; oy = r.top;
      sx = e.clientX; sy = e.clientY;
      e.preventDefault();
    });
    document.addEventListener('mousemove', e => {
      if (!dragging) return;
      const nx = ox + (e.clientX - sx);
      const ny = oy + (e.clientY - sy);
      el.style.left = Math.max(0, Math.min(window.innerWidth - el.offsetWidth, nx)) + 'px';
      el.style.top = Math.max(0, Math.min(window.innerHeight - 40, ny)) + 'px';
      el.style.right = 'auto';
    });
    document.addEventListener('mouseup', () => { dragging = false; });
  }

  // ============================================================
  // 启动
  // ============================================================

  // 初始渲染（把历史数据显示出来）
  render();

  // 2 秒后自动扫描一次（等页面评论加载完）
  setTimeout(() => {
    const n = scanPage();
    if (n > 0) toast(`首次扫描：发现 ${n} 条线索`);
    render();
  }, 2000);

  console.log('[集美租房截流助手] 已启动，共 %d 条历史线索', leads.length);
})();

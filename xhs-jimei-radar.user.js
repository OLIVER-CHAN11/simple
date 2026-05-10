// ==UserScript==
// @name         小红书集美租房线索雷达
// @namespace    https://github.com/OLIVER-CHAN11/simple
// @version      1.0.0
// @description  自动扫描小红书评论区的集美租房需求，分级打标+生成话术+打开用户主页+状态跟进+CSV导出。所有发送动作由人工完成，不自动评论/私信/关注/批量操作，账号零风险。
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
  // 【配置区】关键词 / 评分规则 / 话术，全部集中在这里，改这里即可
  // ============================================================

  const CONFIG = {
    // -------- 关键词词库 --------
    // 地区关键词
    KW_AREA: [
      '集美', '集美区', '杏林', '灌口', '后溪', '侨英', '杏滨',
      '集美学村', '诚毅学院', '华侨大学', '集美大学',
      '软件园三期', '园博苑', '嘉庚体育馆', '集美万达', '龙舟池',
    ],
    // 强租房意图（命中权重最高）
    KW_INTENT_STRONG: [
      '有房吗', '有没有房', '还有房吗', '房子还有吗',
    ],
    // 普通租房意图
    KW_INTENT: [
      '求租', '想租', '租房', '找房', '怎么租', '哪里租',
      '可以租吗', '短租', '长租', '整租', '合租',
    ],
    // 房型
    KW_ROOM: [
      '一室一厅', '一房一厅', '单间', '大单间',
      '两室一厅', '两房一厅', '三室', '公寓', '套房', '独卫', '带阳台',
    ],
    // 价格 / 预算
    KW_PRICE: [
      '多少钱', '价格', '月租', '租金', '贵不贵',
      '预算', '元/月', '一个月', '押一付一', '押二付一',
    ],
    // 疑问
    KW_QUESTION: [
      '有吗', '有没有', '还有吗', '多少', '多少钱',
      '怎么租', '在哪里', '可以吗',
    ],
    // 时间
    KW_TIME: [
      '近期', '马上', '月底', '这个月', '下个月',
      '现在住', '这两天', '最近', '什么时候能住',
    ],
    // 排除（房东、中介、同行）
    KW_NEGATIVE: [
      '我有房', '出租', '转租', '房东直租', '中介勿扰',
      '招租', '房源', '带看', '私我看房', '本人房东', '业主直租',
    ],
    // 数字预算（出现即 +2）
    PRICE_NUMBERS: [800, 1000, 1200, 1500, 2000, 2500, 3000],

    // -------- 评分权重（可按实际效果调整） --------
    SCORE: {
      INTENT_STRONG: 5,
      INTENT: 4,
      AREA: 3,
      ROOM: 3,
      PRICE: 3,
      QUESTION: 2,
      TIME: 2,
      PRICE_NUM: 2,
      NEGATIVE: -5,
    },

    // -------- 分级阈值 --------
    LEVEL: {
      S: 9, // >=9
      A: 6, // 6-8
      B: 3, // 3-5
      // 其余 = C（不展示）
    },

    // -------- UI 噪音黑名单（短文本精确匹配，跳过不参与打分） --------
    UI_NOISE: new Set([
      '登录', '注册', '首页', '发现', '关注', '消息', '搜索', '更多',
      '查看', '回复', '点赞', '收藏', '分享', '举报', '购物车',
      '全部', '最新', '最热', '商品', '笔记', '视频', '用户',
      '展开', '收起', '确定', '取消', '提交', '发送', '完成',
      '赞', '评论', '说点什么', '表情', '@', '话题',
    ]),

    // -------- 文本长度限制 --------
    MIN_LEN: 2,
    MAX_LEN: 120,

    // -------- 存储 --------
    MAX_LEADS: 2000,      // 本地最多保存多少条
    STORAGE_KEY: 'jm_radar_leads_v1',
  };

  // 评论回复话术生成（按从具体到泛化的顺序匹配，命中即返回）
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
    if (/多少钱|价格|月租|预算/.test(text)) {
      return '集美不同位置价格差别挺大，你预算大概多少？想找单间还是一室一厅？';
    }
    if (/集美/.test(text)) {
      return '集美这边要看具体位置和预算，你想找哪一块？什么房型？';
    }
    return '你想找哪个位置附近？预算和入住时间大概是什么时候？';
  }

  // 私信开场话术生成
  function genDmReply(text) {
    if (/一室一厅|一房一厅/.test(text)) {
      return '你好，看到你在问集美一室一厅，想问下你预算大概多少？一个人住还是两个人住？大概什么时候入住？';
    }
    if (/软件园三期/.test(text)) {
      return '你好，看到你在问软件园三期附近租房，你是想通勤近一点，还是预算低一点？预算大概多少？';
    }
    if (/集美大学|华侨大学|诚毅学院/.test(text)) {
      return '你好，看到你在找学校附近的房子，你是想找单间、合租还是一室一厅？预算大概多少？';
    }
    if (/多少钱|价格|月租|预算|租金/.test(text)) {
      return '你好，看到你在问集美租房价格，想问下你预算大概多少？想找哪个位置和什么房型？';
    }
    if (/集美/.test(text)) {
      return '你好，看到你在找集美这边的房子，你想找哪一块？预算、房型和入住时间大概是什么？';
    }
    return '你好，看到你在评论区问租房，想问下你想找哪个位置、什么房型、预算多少？我可以先帮你判断一下范围。';
  }

  // ============================================================
  // 存储层
  // ============================================================

  function loadLeads() {
    try { return JSON.parse(GM_getValue(CONFIG.STORAGE_KEY, '[]')) || []; }
    catch { return []; }
  }
  function saveLeads(arr) {
    // 控制规模
    if (arr.length > CONFIG.MAX_LEADS) arr = arr.slice(0, CONFIG.MAX_LEADS);
    GM_setValue(CONFIG.STORAGE_KEY, JSON.stringify(arr));
  }

  let leads = loadLeads();
  // 快速查重：commentText + pageUrl
  const keyIndex = new Map();
  leads.forEach(l => keyIndex.set(dedupeKey(l.commentText, l.pageUrl), l.id));

  // UI 过滤视图状态（不落盘，只影响显示）
  const view = {
    onlyS: false,
    onlyPending: false,
  };

  function dedupeKey(text, pageUrl) {
    return (pageUrl || '') + '||' + (text || '').slice(0, 80);
  }

  // ============================================================
  // 评分
  // ============================================================

  function containsAny(text, words) {
    return words.filter(w => text.indexOf(w) !== -1);
  }

  function matchPriceNumbers(text) {
    const hits = [];
    for (const n of CONFIG.PRICE_NUMBERS) {
      // 简单数字匹配：前后允许非数字分隔
      const re = new RegExp('(?<!\\d)' + n + '(?!\\d)');
      if (re.test(text)) hits.push(String(n));
    }
    return hits;
  }

  function scoreText(text) {
    let score = 0;
    const matched = [];

    const strong = containsAny(text, CONFIG.KW_INTENT_STRONG);
    if (strong.length) { score += CONFIG.SCORE.INTENT_STRONG; matched.push(...strong); }

    const intent = containsAny(text, CONFIG.KW_INTENT);
    if (intent.length) { score += CONFIG.SCORE.INTENT; matched.push(...intent); }

    const area = containsAny(text, CONFIG.KW_AREA);
    if (area.length) { score += CONFIG.SCORE.AREA; matched.push(...area); }

    const room = containsAny(text, CONFIG.KW_ROOM);
    if (room.length) { score += CONFIG.SCORE.ROOM; matched.push(...room); }

    const price = containsAny(text, CONFIG.KW_PRICE);
    if (price.length) { score += CONFIG.SCORE.PRICE; matched.push(...price); }

    const question = containsAny(text, CONFIG.KW_QUESTION);
    if (question.length) { score += CONFIG.SCORE.QUESTION; matched.push(...question); }

    const time = containsAny(text, CONFIG.KW_TIME);
    if (time.length) { score += CONFIG.SCORE.TIME; matched.push(...time); }

    const priceNums = matchPriceNumbers(text);
    if (priceNums.length) { score += CONFIG.SCORE.PRICE_NUM; matched.push(...priceNums); }

    const neg = containsAny(text, CONFIG.KW_NEGATIVE);
    if (neg.length) { score += CONFIG.SCORE.NEGATIVE; matched.push(...neg.map(n => '⚠' + n)); }

    let level = 'C';
    if (score >= CONFIG.LEVEL.S) level = 'S';
    else if (score >= CONFIG.LEVEL.A) level = 'A';
    else if (score >= CONFIG.LEVEL.B) level = 'B';

    return {
      score,
      level,
      matched: [...new Set(matched)],
    };
  }

  // ============================================================
  // 扫描逻辑
  // ============================================================

  // 判断某个节点是否在 UI 噪音容器内（按钮、导航、脚本面板自身等）
  function isInNoiseAncestor(el) {
    if (!el || !el.closest) return true;
    if (el.closest('#jmr-radar')) return true; // 跳过脚本自身面板
    const noisy = el.closest(
      'button, a[role="button"], nav, header, aside,' +
      '[class*="tab"], [class*="menu"], [class*="btn"], [class*="nav"]'
    );
    if (noisy) {
      const t = (el.innerText || '').trim();
      if (t.length < 10) return true; // 短文本 + 在按钮容器内：噪音
    }
    return false;
  }

  // 尝试从一个文本节点的"上下文"里找用户主页链接 + 用户名
  // 小红书的评论项通常是：
  //   <div class="comment-item">
  //     <a href="/user/profile/{id}">...<span>用户名</span></a>
  //     <div>评论内容</div>
  //   </div>
  // 所以我们往上找最近的"评论容器"，再在里面找 user profile 链接
  function findCommentContext(textEl) {
    // 最多往上找 5 层，避免把整个页面当容器
    let p = textEl;
    for (let i = 0; i < 5 && p; i++) {
      const userLink = p.querySelector
        ? p.querySelector('a[href*="/user/profile/"]')
        : null;
      if (userLink) {
        const username = (userLink.innerText || '').trim().slice(0, 40);
        let userUrl = '';
        try { userUrl = new URL(userLink.getAttribute('href'), location.origin).href; }
        catch {}
        return { username, userUrl };
      }
      p = p.parentElement;
    }
    return { username: '', userUrl: '' };
  }

  function scanOnce() {
    // 第一版按规格要求：从 div/span/p/a 里找可见文本
    const nodes = document.querySelectorAll('div, span, p, a');
    const newLeads = [];
    const seenThisScan = new Set();

    nodes.forEach(el => {
      if (el.dataset._jmrScanned === '1') return;
      if (isInNoiseAncestor(el)) return;

      const text = (el.innerText || '').trim();
      if (!text) return;

      // 文本长度过滤
      if (text.length < CONFIG.MIN_LEN || text.length > CONFIG.MAX_LEN) return;

      // 本次扫描内去重（避免父子节点重复处理同一段文字）
      if (seenThisScan.has(text)) return;

      // UI 噪音精确匹配
      if (CONFIG.UI_NOISE.has(text.replace(/\s+/g, ''))) return;

      // 必须含中文
      if (!/[\u4e00-\u9fa5]/.test(text)) return;

      el.dataset._jmrScanned = '1';
      seenThisScan.add(text);

      // 全局去重：同一条评论文本在同一页面只保留一条
      const key = dedupeKey(text, location.href);
      if (keyIndex.has(key)) {
        // 已存在，尝试补充用户信息（可能之前没抓到）
        const existingId = keyIndex.get(key);
        const existing = leads.find(l => l.id === existingId);
        if (existing && !existing.userUrl) {
          const ctx = findCommentContext(el);
          if (ctx.userUrl) {
            existing.userUrl = ctx.userUrl;
            existing.username = ctx.username || existing.username;
            existing.updatedAt = Date.now();
          }
        }
        return;
      }

      // 评分
      const { score, level, matched } = scoreText(text);
      if (level === 'C') return; // 不展示 C 级

      // 尝试抓用户名和主页链接
      const ctx = findCommentContext(el);

      const id = 'jmr_' + hashStr(key);
      const lead = {
        id,
        platform: '小红书',
        username: ctx.username || '',
        userUrl: ctx.userUrl || '',
        commentText: text,
        pageUrl: location.href,
        pageTitle: document.title || '',
        score,
        level,
        matchedKeywords: matched,
        commentReply: genCommentReply(text),
        dmReply: genDmReply(text),
        status: '待处理',
        createdAt: Date.now(),
        updatedAt: Date.now(),
      };

      // 在页面对应元素上打标，便于"定位/高亮"
      el.setAttribute('data-jmr-id', id);
      el.classList.add('jmr-hit-' + level.toLowerCase());

      newLeads.push(lead);
      keyIndex.set(key, id);
    });

    if (newLeads.length) {
      leads = [...newLeads, ...leads];
      saveLeads(leads);
    }
    return newLeads.length;
  }

  // ============================================================
  // UI 面板
  // ============================================================

  GM_addStyle(`
    #jmr-radar {
      position: fixed; top: 72px; right: 16px; z-index: 999999;
      width: 400px; height: 85vh;
      background: #fff; border: 1px solid #e5e7eb; border-radius: 10px;
      box-shadow: 0 10px 30px rgba(0,0,0,0.15);
      font-family: -apple-system, "PingFang SC", "Microsoft YaHei", sans-serif;
      font-size: 13px; color: #111827;
      display: flex; flex-direction: column;
      overflow: hidden;
    }
    #jmr-radar.collapsed { width: 180px; height: auto; }
    #jmr-radar.collapsed .jmr-body { display: none; }

    #jmr-radar .jmr-header {
      flex-shrink: 0;
      padding: 10px 12px;
      background: linear-gradient(90deg, #ff2442, #ff6b6b);
      color: #fff;
      display: flex; justify-content: space-between; align-items: center;
      cursor: move; user-select: none;
    }
    #jmr-radar .jmr-title { font-weight: 600; font-size: 14px; }
    #jmr-radar .jmr-collapse {
      background: rgba(255,255,255,0.25); border: none; color: #fff;
      width: 24px; height: 24px; border-radius: 4px; cursor: pointer;
      font-weight: bold;
    }

    #jmr-radar .jmr-body {
      flex: 1; min-height: 0; display: flex; flex-direction: column;
    }

    /* 今日统计 */
    #jmr-radar .jmr-stats {
      flex-shrink: 0;
      display: grid; grid-template-columns: repeat(3, 1fr);
      gap: 4px; padding: 8px 12px;
      background: #fafafa; border-bottom: 1px solid #f3f4f6;
    }
    #jmr-radar .jmr-stat {
      text-align: center; padding: 4px; border-radius: 4px;
      background: #fff; border: 1px solid #f3f4f6;
    }
    #jmr-radar .jmr-stat .v { font-size: 15px; font-weight: 600; line-height: 1.2; }
    #jmr-radar .jmr-stat .l { font-size: 10px; color: #6b7280; margin-top: 2px; }
    #jmr-radar .jmr-stat.s .v { color: #ef4444; }
    #jmr-radar .jmr-stat.a .v { color: #f59e0b; }
    #jmr-radar .jmr-stat.comment .v { color: #1e40af; }
    #jmr-radar .jmr-stat.dm .v { color: #6b21a8; }
    #jmr-radar .jmr-stat.reply .v { color: #065f46; }
    #jmr-radar .jmr-stat.contact .v { color: #db2777; }

    /* 工具栏 */
    #jmr-radar .jmr-toolbar {
      flex-shrink: 0;
      padding: 6px 10px; display: flex; gap: 4px; flex-wrap: wrap;
      border-bottom: 1px solid #f3f4f6;
    }
    #jmr-radar .jmr-btn {
      border: 1px solid #e5e7eb; background: #fff; color: #374151;
      padding: 5px 8px; border-radius: 5px; font-size: 11px;
      cursor: pointer; transition: all 0.15s;
    }
    #jmr-radar .jmr-btn:hover { border-color: #ff2442; color: #ff2442; }
    #jmr-radar .jmr-btn.primary {
      background: #ff2442; color: #fff; border-color: #ff2442;
    }
    #jmr-radar .jmr-btn.primary:hover { background: #e91e3a; color: #fff; }
    #jmr-radar .jmr-btn.active {
      background: #fef3c7; border-color: #f59e0b; color: #92400e;
    }
    #jmr-radar .jmr-btn.danger { color: #dc2626; }

    /* 列表 */
    #jmr-radar .jmr-list {
      flex: 1 1 auto; min-height: 0;
      overflow-y: auto; overflow-x: hidden;
      padding: 8px 12px;
      overscroll-behavior: contain;
    }
    #jmr-radar .jmr-empty {
      text-align: center; color: #9ca3af; padding: 32px 12px; font-size: 12px;
    }

    /* 卡片 */
    .jmr-card {
      border: 1px solid #e5e7eb; border-radius: 8px;
      padding: 10px; margin-bottom: 10px; background: #fff;
      transition: opacity 0.2s;
    }
    .jmr-card.status-忽略 { opacity: 0.45; }
    .jmr-card .c-meta {
      display: flex; align-items: center; gap: 6px;
      font-size: 11px; color: #6b7280; margin-bottom: 6px; flex-wrap: wrap;
    }
    .jmr-card .c-level {
      font-weight: 600; padding: 1px 7px; border-radius: 4px;
      color: #fff; font-size: 11px;
    }
    .jmr-card .c-level.S { background: #ef4444; }
    .jmr-card .c-level.A { background: #f59e0b; }
    .jmr-card .c-level.B { background: #3b82f6; }
    .jmr-card .c-status {
      margin-left: auto; padding: 1px 6px; border-radius: 4px;
      background: #f3f4f6; font-size: 10px; color: #6b7280;
    }
    .jmr-card .c-status.已评论 { background: #dbeafe; color: #1e40af; }
    .jmr-card .c-status.已私信 { background: #e9d5ff; color: #6b21a8; }
    .jmr-card .c-status.已回复 { background: #d1fae5; color: #065f46; }
    .jmr-card .c-status.已留资 { background: #fce7f3; color: #be185d; }
    .jmr-card .c-status.忽略 { background: #f3f4f6; color: #6b7280; }

    .jmr-card .c-text {
      background: #fffbeb; padding: 6px 8px; border-radius: 5px;
      font-size: 13px; line-height: 1.5; color: #111827;
      margin-bottom: 6px; word-break: break-all;
    }
    .jmr-card .c-text mark {
      background: #fde047; padding: 0 2px; border-radius: 2px;
    }
    .jmr-card .c-text mark.neg { background: #fecaca; }

    .jmr-card .c-kw {
      font-size: 11px; margin-bottom: 8px;
    }
    .jmr-card .c-kw .chip {
      display: inline-block; padding: 1px 6px; margin: 1px 3px 1px 0;
      background: #fef3c7; border-radius: 8px; color: #92400e;
    }
    .jmr-card .c-kw .chip.neg { background: #fee2e2; color: #991b1b; }

    .jmr-card .c-script-block {
      border: 1px dashed #e5e7eb; border-radius: 5px;
      padding: 6px 8px; margin-bottom: 6px; font-size: 12px;
      line-height: 1.5; color: #374151; background: #fafafa;
    }
    .jmr-card .c-script-label {
      font-size: 10px; color: #9ca3af; margin-bottom: 3px;
      display: flex; justify-content: space-between;
    }
    .jmr-card .c-script-label .c-mini-btn {
      font-size: 10px; padding: 1px 7px; border: 1px solid #d1d5db;
      background: #fff; border-radius: 3px; cursor: pointer; color: #374151;
    }
    .jmr-card .c-script-label .c-mini-btn:hover { border-color: #ff2442; color: #ff2442; }

    .jmr-card .c-actions {
      display: grid; grid-template-columns: repeat(4, 1fr);
      gap: 4px; margin-top: 6px;
    }
    .jmr-card .c-actions .jmr-btn {
      font-size: 11px; padding: 4px 4px;
    }
    .jmr-card .c-actions .jmr-btn.open-home {
      grid-column: span 2; background: #eff6ff; color: #1e40af; border-color: #bfdbfe;
    }
    .jmr-card .c-actions .jmr-btn.open-home:hover { background: #dbeafe; }
    .jmr-card .c-actions .jmr-btn.open-home.disabled {
      background: #f3f4f6; color: #9ca3af; border-color: #e5e7eb;
      cursor: not-allowed; pointer-events: none;
    }

    /* 页面内命中高亮 */
    .jmr-hit-s, .jmr-hit-a, .jmr-hit-b {
      outline-offset: 2px !important;
      border-radius: 4px !important;
    }
    .jmr-hit-s { outline: 2px solid #ef4444 !important; background: #fef3c7 !important; }
    .jmr-hit-a { outline: 2px solid #f59e0b !important; background: #fefce8 !important; }
    .jmr-hit-b { outline: 1px dashed #3b82f6 !important; }

    /* Toast */
    .jmr-toast {
      position: fixed; bottom: 30px; left: 50%; transform: translateX(-50%);
      background: rgba(0,0,0,0.8); color: #fff;
      padding: 8px 16px; border-radius: 20px;
      z-index: 9999999; font-size: 13px;
    }
  `);

  const panel = document.createElement('div');
  panel.id = 'jmr-radar';
  panel.innerHTML = `
    <div class="jmr-header" id="jmr-header">
      <span class="jmr-title">🏠 集美租房线索雷达</span>
      <button class="jmr-collapse" id="jmr-collapse" title="折叠">—</button>
    </div>
    <div class="jmr-body">
      <div class="jmr-stats" id="jmr-stats"></div>
      <div class="jmr-toolbar">
        <button class="jmr-btn primary" id="jmr-rescan">重新扫描</button>
        <button class="jmr-btn" id="jmr-only-s">只看S级</button>
        <button class="jmr-btn" id="jmr-only-pending">只看待处理</button>
        <button class="jmr-btn" id="jmr-export">导出CSV</button>
        <button class="jmr-btn danger" id="jmr-clear">清空本页</button>
      </div>
      <div class="jmr-list" id="jmr-list"></div>
    </div>
  `;
  document.body.appendChild(panel);

  // 拖拽
  makeDraggable(panel, panel.querySelector('#jmr-header'));

  // 折叠
  panel.querySelector('#jmr-collapse').onclick = () => {
    panel.classList.toggle('collapsed');
    panel.querySelector('#jmr-collapse').textContent =
      panel.classList.contains('collapsed') ? '▢' : '—';
  };

  // 顶部按钮
  panel.querySelector('#jmr-rescan').onclick = () => {
    // 强扫：清掉 dataset 标记让所有节点重新参与
    document.querySelectorAll('[data-_jmr-scanned="1"]').forEach(el => {
      delete el.dataset._jmrScanned;
    });
    const n = scanOnce();
    toast(`扫描完成：新增 ${n} 条`);
    render();
  };
  panel.querySelector('#jmr-only-s').onclick = () => {
    view.onlyS = !view.onlyS;
    render();
  };
  panel.querySelector('#jmr-only-pending').onclick = () => {
    view.onlyPending = !view.onlyPending;
    render();
  };
  panel.querySelector('#jmr-export').onclick = exportCSV;
  panel.querySelector('#jmr-clear').onclick = () => {
    const pageUrl = location.href;
    const before = leads.length;
    if (!confirm('将清空当前页面（同一链接下）的所有线索记录。确定？')) return;
    // 删除索引
    leads.forEach(l => {
      if (l.pageUrl === pageUrl) {
        keyIndex.delete(dedupeKey(l.commentText, l.pageUrl));
      }
    });
    leads = leads.filter(l => l.pageUrl !== pageUrl);
    saveLeads(leads);
    // 清 DOM 标记
    document.querySelectorAll('[data-jmr-id]').forEach(el => {
      el.removeAttribute('data-jmr-id');
      el.classList.remove('jmr-hit-s', 'jmr-hit-a', 'jmr-hit-b');
    });
    document.querySelectorAll('[data-_jmr-scanned="1"]').forEach(el => {
      delete el.dataset._jmrScanned;
    });
    toast(`已清空 ${before - leads.length} 条本页线索`);
    render();
  };

  // ============================================================
  // 渲染
  // ============================================================

  function render() {
    renderStats();
    renderList();
    renderFilterButtons();
  }

  function renderFilterButtons() {
    panel.querySelector('#jmr-only-s').classList.toggle('active', view.onlyS);
    panel.querySelector('#jmr-only-pending').classList.toggle('active', view.onlyPending);
  }

  function isToday(ts) {
    const d = new Date(ts);
    const now = new Date();
    return d.getFullYear() === now.getFullYear()
        && d.getMonth() === now.getMonth()
        && d.getDate() === now.getDate();
  }

  function renderStats() {
    // 今日统计（按 createdAt 或 updatedAt 落在今天）
    const todayLeads = leads.filter(l => isToday(l.createdAt) || isToday(l.updatedAt));
    const s = todayLeads.filter(l => l.level === 'S').length;
    const a = todayLeads.filter(l => l.level === 'A').length;
    const commented = todayLeads.filter(l => l.status === '已评论').length;
    const dmed = todayLeads.filter(l => l.status === '已私信').length;
    const replied = todayLeads.filter(l => l.status === '已回复').length;
    const contact = todayLeads.filter(l => l.status === '已留资').length;

    panel.querySelector('#jmr-stats').innerHTML = `
      <div class="jmr-stat s"><div class="v">${s}</div><div class="l">S级</div></div>
      <div class="jmr-stat a"><div class="v">${a}</div><div class="l">A级</div></div>
      <div class="jmr-stat comment"><div class="v">${commented}</div><div class="l">已评论</div></div>
      <div class="jmr-stat dm"><div class="v">${dmed}</div><div class="l">已私信</div></div>
      <div class="jmr-stat reply"><div class="v">${replied}</div><div class="l">已回复</div></div>
      <div class="jmr-stat contact"><div class="v">${contact}</div><div class="l">已留资</div></div>
    `;
  }

  function renderList() {
    const wrap = panel.querySelector('#jmr-list');
    // 只展示本页的 S/A/B，应用筛选
    const statusOrder = { '待处理': 0, '已评论': 1, '已私信': 2, '已回复': 3, '已留资': 4, '忽略': 5 };
    const levelOrder = { S: 0, A: 1, B: 2 };

    const filtered = leads
      .filter(l => l.pageUrl === location.href)
      .filter(l => ['S', 'A', 'B'].includes(l.level))
      .filter(l => !view.onlyS || l.level === 'S')
      .filter(l => !view.onlyPending || l.status === '待处理')
      .sort((x, y) => {
        if (statusOrder[x.status] !== statusOrder[y.status]) {
          return statusOrder[x.status] - statusOrder[y.status];
        }
        if (levelOrder[x.level] !== levelOrder[y.level]) {
          return levelOrder[x.level] - levelOrder[y.level];
        }
        return y.score - x.score;
      });

    if (!filtered.length) {
      wrap.innerHTML = `
        <div class="jmr-empty">
          当前页面还没扫出线索<br/><br/>
          打开一篇小红书笔记，滚动加载评论后点<br/>"重新扫描"<br/><br/>
          <span style="font-size:11px;">(新评论会随滚动自动扫描)</span>
        </div>`;
      return;
    }

    wrap.innerHTML = filtered.map(renderCard).join('');

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

    // 用户主页按钮：有链接就启用，没有就禁用
    const openHomeBtn = lead.userUrl
      ? `<button class="jmr-btn open-home" data-lead-act="open-home" data-lead-id="${lead.id}">🏠 打开用户主页</button>`
      : `<button class="jmr-btn open-home disabled" title="这条没抓到用户主页链接">🏠 无主页链接</button>`;

    return `
      <div class="jmr-card status-${lead.status}">
        <div class="c-meta">
          <span class="c-level ${lead.level}">${lead.level}</span>
          <span>分数 ${lead.score}</span>
          ${lead.username ? `<span style="color:#ff2442;font-weight:500;">@${escapeHtml(lead.username)}</span>` : ''}
          <span class="c-status ${lead.status}">${lead.status}</span>
        </div>
        <div class="c-text">${textHtml}</div>
        ${kwChips ? `<div class="c-kw">${kwChips}</div>` : ''}

        <div class="c-script-block">
          <div class="c-script-label">
            <span>💬 评论区回复话术</span>
            <button class="c-mini-btn" data-lead-act="copy-comment" data-lead-id="${lead.id}">复制</button>
          </div>
          <div>${escapeHtml(lead.commentReply)}</div>
        </div>
        <div class="c-script-block">
          <div class="c-script-label">
            <span>📨 私信开场话术</span>
            <button class="c-mini-btn" data-lead-act="copy-dm" data-lead-id="${lead.id}">复制</button>
          </div>
          <div>${escapeHtml(lead.dmReply)}</div>
        </div>

        <div class="c-actions">
          ${openHomeBtn}
          <button class="jmr-btn" data-lead-act="locate" data-lead-id="${lead.id}">定位</button>
          <button class="jmr-btn danger" data-lead-act="ignore" data-lead-id="${lead.id}">✕忽略</button>

          <button class="jmr-btn" data-lead-act="mark-comment" data-lead-id="${lead.id}">✅已评论</button>
          <button class="jmr-btn" data-lead-act="mark-dm" data-lead-id="${lead.id}">✅已私信</button>
          <button class="jmr-btn" data-lead-act="mark-reply" data-lead-id="${lead.id}">✅已回复</button>
          <button class="jmr-btn" data-lead-act="mark-contact" data-lead-id="${lead.id}">💎已留资</button>
        </div>
      </div>
    `;
  }

  function handleAction(act, lead) {
    const setStatus = (s) => {
      lead.status = s;
      lead.updatedAt = Date.now();
      saveLeads(leads);
      render();
    };
    switch (act) {
      case 'copy-comment':
        copyToClipboard(lead.commentReply);
        toast('评论回复已复制');
        break;
      case 'copy-dm':
        copyToClipboard(lead.dmReply);
        toast('私信话术已复制');
        break;
      case 'open-home':
        if (lead.userUrl) {
          window.open(lead.userUrl, '_blank', 'noopener');
          toast('已打开用户主页');
        } else {
          toast('没抓到用户主页链接');
        }
        break;
      case 'locate': {
        const el = document.querySelector(`[data-jmr-id="${lead.id}"]`);
        if (el) {
          el.scrollIntoView({ behavior: 'smooth', block: 'center' });
          const orig = el.style.outline;
          el.style.outline = '3px solid #ff2442';
          setTimeout(() => { el.style.outline = orig; }, 1800);
          toast('已定位到页面');
        } else {
          toast('此条已不在当前 DOM 中');
        }
        break;
      }
      case 'mark-comment': setStatus('已评论'); toast('已标记：已评论'); break;
      case 'mark-dm': setStatus('已私信'); toast('已标记：已私信'); break;
      case 'mark-reply': setStatus('已回复'); toast('已标记：已回复'); break;
      case 'mark-contact': setStatus('已留资'); toast('已标记：已留资 💎'); break;
      case 'ignore': setStatus('忽略'); toast('已忽略'); break;
    }
  }

  // ============================================================
  // 导出 CSV
  // ============================================================

  function exportCSV() {
    if (!leads.length) { toast('暂无数据'); return; }
    const headers = [
      '时间', '平台', '等级', '分数', '用户名',
      '评论内容', '命中关键词', '评论回复话术', '私信话术',
      '状态', '用户主页', '页面链接',
    ];
    const rows = leads.map(l => [
      new Date(l.createdAt).toLocaleString('zh-CN'),
      l.platform,
      l.level,
      l.score,
      l.username || '',
      l.commentText,
      (l.matchedKeywords || []).join(';'),
      l.commentReply,
      l.dmReply,
      l.status,
      l.userUrl || '',
      l.pageUrl,
    ]);
    const csv = [headers, ...rows].map(r =>
      r.map(c => `"${String(c == null ? '' : c).replace(/"/g, '""')}"`).join(',')
    ).join('\n');
    // BOM 保证 Excel 识别中文
    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `集美租房线索_${fmtFile(Date.now())}.csv`;
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
    return (h >>> 0).toString(36);
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
    t.className = 'jmr-toast';
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
  // MutationObserver：自动扫描新增的评论
  // ============================================================

  let scanDebounceTimer = null;
  let lastAutoScanAt = 0;

  function scheduleAutoScan() {
    clearTimeout(scanDebounceTimer);
    scanDebounceTimer = setTimeout(() => {
      // 防抖 + 限频：每 2 秒最多扫一次
      const now = Date.now();
      if (now - lastAutoScanAt < 2000) return;
      lastAutoScanAt = now;
      const added = scanOnce();
      if (added > 0) render();
    }, 600);
  }

  const observer = new MutationObserver((mutations) => {
    // 忽略纯属性变化
    const hasNewNodes = mutations.some(m =>
      m.addedNodes && m.addedNodes.length > 0
    );
    if (hasNewNodes) scheduleAutoScan();
  });
  observer.observe(document.body, { childList: true, subtree: true });

  // 监听 SPA 路由切换，切笔记时也重新扫
  let lastHref = location.href;
  setInterval(() => {
    if (location.href !== lastHref) {
      lastHref = location.href;
      // 清 DOM 标记，重新扫
      document.querySelectorAll('[data-_jmr-scanned="1"]').forEach(el => {
        delete el.dataset._jmrScanned;
      });
      setTimeout(() => {
        scanOnce();
        render();
      }, 1500);
    }
  }, 1000);

  // ============================================================
  // 启动
  // ============================================================

  render();
  setTimeout(() => {
    const n = scanOnce();
    if (n > 0) toast(`首次扫描：发现 ${n} 条线索`);
    render();
  }, 1800);

  console.log('[集美租房线索雷达] 已启动，历史线索 %d 条', leads.length);
})();

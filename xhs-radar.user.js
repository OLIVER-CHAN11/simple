// ==UserScript==
// @name         集美租房截流雷达 · 小红书版
// @namespace    https://github.com/OLIVER-CHAN11/simple
// @version      1.1.0
// @description  自动监控小红书搜索页 / 笔记评论区，命中租房需求关键词时响铃提醒并收集线索。纯被动监听，不做任何发送/点赞/私信操作，账号安全。
// @author       you
// @match        https://www.xiaohongshu.com/*
// @match        https://xiaohongshu.com/*
// @grant        GM_setValue
// @grant        GM_getValue
// @grant        GM_notification
// @grant        GM_addStyle
// @run-at       document-idle
// @license      MIT
// ==/UserScript==

(function () {
  'use strict';

  // ==================== 配置 ====================
  const DEFAULT_CONFIG = {
    // 需求关键词（命中任一即视为"有求租意向"）
    needKeywords: [
      '求租', '找房', '租房', '有房吗', '有房源吗', '合租', '求合租', '招室友',
      '求主卧', '求次卧', '单间', '出租吗', '还租吗', '短租', '想租',
      '有没有推荐', '有推荐吗', '有合适的吗', '求推荐',
    ],
    // 地域关键词（命中任一即视为"集美相关"）
    areaKeywords: [
      '集美', '集大', '集美大学', '诚毅', '华厦', '理工学院',
      '软件园三期', '软三', '软件园', '杏林', '杏锦', '杏西', '杏东',
      '灌口', '后溪', '北站', '侨英', '孙坂', '龙湖', '岑东', '岑西',
      '印斗', '厦门北', 'SM',
    ],
    // 必须同时命中 "需求 + 地域" 才算有效线索（强过滤）
    requireBoth: true,
    // 响铃提醒
    enableSound: true,
    // 桌面通知
    enableNotify: true,
    // 自动滚动搜索页（每 N 秒滚动一次，模拟真人浏览）
    autoScroll: false,
    autoScrollInterval: 45,
    // 夜间静默（23:00 - 7:00 不响铃不通知）
    quietNight: true,
    // 命中后高亮原内容
    highlightInPage: true,
  };

  const STORAGE_KEY_CONFIG = 'jm_radar_config_v1';
  const STORAGE_KEY_HITS = 'jm_radar_hits_v1';
  const STORAGE_KEY_SEEN = 'jm_radar_seen_v1';

  let config = Object.assign({}, DEFAULT_CONFIG, loadJSON(STORAGE_KEY_CONFIG, {}));
  let hits = loadJSON(STORAGE_KEY_HITS, []);
  let seen = new Set(loadJSON(STORAGE_KEY_SEEN, []));
  let scanCount = 0;
  let lastScanAt = 0;
  let autoScrollTimer = null;
  let scanTimer = null;
  let scanEnabled = true;  // 扫描总开关（不落盘，刷新即恢复）
  let panelCollapsed = false;

  function loadJSON(k, fallback) {
    try { return JSON.parse(GM_getValue(k, '')) || fallback; } catch { return fallback; }
  }
  function saveJSON(k, v) { GM_setValue(k, JSON.stringify(v)); }

  // ==================== 样式 ====================
  GM_addStyle(`
    #jm-radar {
      position: fixed; top: 80px; right: 16px; z-index: 999999;
      width: 360px; max-height: 80vh;
      background: #fff; border: 1px solid #e5e7eb; border-radius: 10px;
      box-shadow: 0 10px 30px rgba(0,0,0,0.15);
      font-family: -apple-system, BlinkMacSystemFont, "PingFang SC", "Microsoft YaHei", sans-serif;
      font-size: 12px; color: #111827;
      display: flex; flex-direction: column;
      transition: all 0.2s;
    }
    #jm-radar.collapsed { width: 140px; }
    #jm-radar.collapsed .jm-body { display: none; }
    #jm-radar .jm-header {
      padding: 10px 12px; background: linear-gradient(90deg, #ef4444, #f59e0b);
      color: #fff; border-radius: 10px 10px 0 0;
      display: flex; align-items: center; justify-content: space-between;
      cursor: move; user-select: none;
    }
    #jm-radar.collapsed .jm-header { border-radius: 10px; }
    #jm-radar .jm-title { font-weight: 600; font-size: 13px; }
    #jm-radar .jm-status-dot {
      display: inline-block; width: 8px; height: 8px; border-radius: 50%;
      background: #10b981; margin-right: 6px;
      animation: jm-pulse 2s infinite;
    }
    @keyframes jm-pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.3; }
    }
    #jm-radar .jm-header-btns { display: flex; gap: 4px; }
    #jm-radar .jm-icon-btn {
      background: rgba(255,255,255,0.2); border: none; color: #fff;
      width: 22px; height: 22px; border-radius: 4px; cursor: pointer;
      font-size: 12px; line-height: 1;
    }
    #jm-radar .jm-icon-btn:hover { background: rgba(255,255,255,0.35); }
    #jm-radar .jm-body { flex: 1; overflow-y: auto; }
    #jm-radar .jm-tabs {
      display: flex; border-bottom: 1px solid #e5e7eb; background: #fafafa;
    }
    #jm-radar .jm-tab {
      flex: 1; padding: 8px; text-align: center; cursor: pointer;
      font-size: 12px; color: #6b7280; border-bottom: 2px solid transparent;
    }
    #jm-radar .jm-tab.active { color: #ef4444; border-bottom-color: #ef4444; background: #fff; }
    #jm-radar .jm-pane { padding: 10px 12px; }
    #jm-radar .jm-stats {
      display: grid; grid-template-columns: repeat(3, 1fr); gap: 6px;
      margin-bottom: 10px;
    }
    #jm-radar .jm-stat {
      background: #f9fafb; padding: 6px; border-radius: 6px; text-align: center;
    }
    #jm-radar .jm-stat .v { font-size: 16px; font-weight: 600; color: #111827; }
    #jm-radar .jm-stat .l { font-size: 10px; color: #6b7280; }

    #jm-radar .jm-hit {
      border: 1px solid #e5e7eb; border-left: 3px solid #ef4444;
      border-radius: 6px; padding: 8px; margin-bottom: 8px;
      background: #fffbeb; font-size: 12px;
    }
    #jm-radar .jm-hit.new { animation: jm-flash 1.5s ease; }
    @keyframes jm-flash {
      0%, 100% { background: #fffbeb; }
      30% { background: #fecaca; }
    }
    #jm-radar .jm-hit .h-meta { color: #6b7280; font-size: 10px; margin-bottom: 3px; }
    #jm-radar .jm-hit .h-author { font-weight: 600; color: #ef4444; margin-right: 6px; }
    #jm-radar .jm-hit .h-text { line-height: 1.5; color: #374151; word-break: break-all; }
    #jm-radar .jm-hit .h-text mark { background: #fde047; padding: 0 2px; border-radius: 2px; }
    #jm-radar .jm-hit .h-actions { margin-top: 6px; display: flex; gap: 6px; }
    #jm-radar .jm-hit .h-btn {
      flex: 1; padding: 4px 6px; font-size: 11px; border: 1px solid #d1d5db;
      background: #fff; border-radius: 4px; cursor: pointer; color: #374151;
    }
    #jm-radar .jm-hit .h-btn:hover { border-color: #ef4444; color: #ef4444; }
    #jm-radar .jm-hit .h-btn.primary { background: #ef4444; color: #fff; border-color: #ef4444; text-decoration: none; display: inline-flex; align-items: center; justify-content: center; }
    #jm-radar .jm-hit .h-btn.primary:hover { background: #dc2626; color: #fff; }
    #jm-radar .jm-hit .h-btn.primary.disabled { background: #d1d5db; border-color: #d1d5db; cursor: not-allowed; pointer-events: none; }

    /* 顶部快捷开关 */
    #jm-radar .jm-quickbar {
      display: flex; gap: 6px; padding: 8px 12px;
      background: #fafafa; border-bottom: 1px solid #e5e7eb;
    }
    #jm-radar .jm-quick-btn {
      flex: 1; padding: 6px 4px; font-size: 11px;
      border: 1px solid #d1d5db; background: #fff; border-radius: 5px;
      cursor: pointer; color: #6b7280; transition: all 0.15s;
      display: flex; align-items: center; justify-content: center; gap: 4px;
    }
    #jm-radar .jm-quick-btn:hover { border-color: #9ca3af; }
    #jm-radar .jm-quick-btn.on {
      background: #10b981; color: #fff; border-color: #10b981;
    }
    #jm-radar .jm-quick-btn.on.warn {
      background: #f59e0b; border-color: #f59e0b;
    }
    #jm-radar .jm-quick-btn .dot {
      width: 6px; height: 6px; border-radius: 50%; background: #d1d5db;
    }
    #jm-radar .jm-quick-btn.on .dot { background: #fff; }

    #jm-radar .jm-empty { text-align: center; color: #9ca3af; padding: 20px 0; }

    #jm-radar label { display: block; font-size: 11px; color: #6b7280; margin: 8px 0 3px; }
    #jm-radar textarea, #jm-radar input[type="number"] {
      width: 100%; padding: 6px 8px; border: 1px solid #d1d5db;
      border-radius: 4px; font-size: 12px; font-family: inherit; box-sizing: border-box;
    }
    #jm-radar textarea { min-height: 60px; resize: vertical; }
    #jm-radar .jm-switch-row {
      display: flex; align-items: center; justify-content: space-between;
      padding: 6px 0; border-bottom: 1px dashed #f3f4f6;
    }
    #jm-radar .jm-switch-row:last-child { border-bottom: none; }
    #jm-radar .jm-primary-btn {
      width: 100%; padding: 8px; background: #ef4444; color: #fff;
      border: none; border-radius: 6px; cursor: pointer; font-size: 13px;
      font-weight: 500; margin-top: 10px;
    }
    #jm-radar .jm-primary-btn:hover { background: #dc2626; }
    #jm-radar .jm-secondary-btn {
      width: 100%; padding: 6px; background: #fff; color: #374151;
      border: 1px solid #d1d5db; border-radius: 6px; cursor: pointer;
      font-size: 12px; margin-top: 6px;
    }

    /* 页面内高亮 */
    .jm-radar-highlight {
      outline: 2px solid #ef4444 !important;
      outline-offset: 2px;
      background: #fef3c7 !important;
      border-radius: 4px;
      transition: outline 0.3s;
    }
  `);

  // ==================== UI ====================
  const panel = document.createElement('div');
  panel.id = 'jm-radar';
  panel.innerHTML = `
    <div class="jm-header" id="jm-header">
      <div>
        <span class="jm-status-dot"></span>
        <span class="jm-title">🎯 集美截流雷达</span>
      </div>
      <div class="jm-header-btns">
        <button class="jm-icon-btn" id="jm-collapse" title="折叠">—</button>
      </div>
    </div>
    <div class="jm-body">
      <div class="jm-quickbar">
        <button class="jm-quick-btn" id="jm-q-scan" title="开关扫描">
          <span class="dot"></span><span>扫描中</span>
        </button>
        <button class="jm-quick-btn" id="jm-q-scroll" title="开关自动滚动">
          <span class="dot"></span><span>自动滚动</span>
        </button>
        <button class="jm-quick-btn" id="jm-q-sound" title="开关响铃">
          <span class="dot"></span><span>响铃</span>
        </button>
      </div>
      <div class="jm-tabs">
        <div class="jm-tab active" data-tab="hits">🔥 命中 <span id="jm-hits-badge" style="background:#ef4444;color:#fff;border-radius:8px;padding:0 5px;font-size:10px;display:none;">0</span></div>
        <div class="jm-tab" data-tab="status">📊 状态</div>
        <div class="jm-tab" data-tab="config">⚙️ 配置</div>
      </div>
      <div class="jm-pane" data-pane="hits">
        <div id="jm-hits-list"></div>
      </div>
      <div class="jm-pane" data-pane="status" style="display:none;">
        <div class="jm-stats">
          <div class="jm-stat"><div class="v" id="jm-s-scans">0</div><div class="l">累计扫描</div></div>
          <div class="jm-stat"><div class="v" id="jm-s-seen">0</div><div class="l">已见内容</div></div>
          <div class="jm-stat"><div class="v" id="jm-s-hits">0</div><div class="l">命中线索</div></div>
        </div>
        <div style="font-size:11px; color:#6b7280; line-height:1.7;">
          <div>页面类型：<b id="jm-s-pagetype">-</b></div>
          <div>上次扫描：<b id="jm-s-lastscan">-</b></div>
          <div>运行时长：<b id="jm-s-uptime">0秒</b></div>
        </div>
        <button class="jm-secondary-btn" id="jm-btn-scan">立即扫描</button>
        <button class="jm-secondary-btn" id="jm-btn-clear-seen">清除"已见"缓存（重新扫全部）</button>
        <button class="jm-secondary-btn" id="jm-btn-export">导出命中线索 CSV</button>
        <button class="jm-secondary-btn" id="jm-btn-clear-hits" style="color:#ef4444;">清空命中记录</button>
      </div>
      <div class="jm-pane" data-pane="config" style="display:none;">
        <label>需求关键词（逗号或换行分隔）</label>
        <textarea id="jm-cfg-need"></textarea>
        <label>地域关键词（逗号或换行分隔）</label>
        <textarea id="jm-cfg-area"></textarea>
        <div class="jm-switch-row">
          <span>必须同时命中 需求+地域</span>
          <input type="checkbox" id="jm-cfg-both" />
        </div>
        <div class="jm-switch-row">
          <span>响铃提醒</span>
          <input type="checkbox" id="jm-cfg-sound" />
        </div>
        <div class="jm-switch-row">
          <span>桌面通知</span>
          <input type="checkbox" id="jm-cfg-notify" />
        </div>
        <div class="jm-switch-row">
          <span>页面内高亮命中内容</span>
          <input type="checkbox" id="jm-cfg-highlight" />
        </div>
        <div class="jm-switch-row">
          <span>自动滚动（模拟浏览）</span>
          <input type="checkbox" id="jm-cfg-autoscroll" />
        </div>
        <div class="jm-switch-row">
          <span>滚动间隔（秒）</span>
          <input type="number" id="jm-cfg-scrollint" min="15" max="300" style="width:70px;" />
        </div>
        <div class="jm-switch-row">
          <span>夜间静默（23:00-7:00）</span>
          <input type="checkbox" id="jm-cfg-night" />
        </div>
        <button class="jm-primary-btn" id="jm-btn-save-cfg">保存配置</button>
        <button class="jm-secondary-btn" id="jm-btn-reset-cfg">恢复默认</button>
      </div>
    </div>
  `;
  document.body.appendChild(panel);

  // 拖拽
  makeDraggable(panel, panel.querySelector('#jm-header'));
  // 折叠
  panel.querySelector('#jm-collapse').onclick = () => {
    panelCollapsed = !panelCollapsed;
    panel.classList.toggle('collapsed', panelCollapsed);
    panel.querySelector('#jm-collapse').textContent = panelCollapsed ? '▢' : '—';
  };

  // Tab 切换
  panel.querySelectorAll('.jm-tab').forEach(t => {
    t.onclick = () => {
      panel.querySelectorAll('.jm-tab').forEach(x => x.classList.remove('active'));
      t.classList.add('active');
      const name = t.dataset.tab;
      panel.querySelectorAll('.jm-pane').forEach(p => {
        p.style.display = p.dataset.pane === name ? '' : 'none';
      });
    };
  });

  // 配置 UI 双向绑定
  function fillConfigUI() {
    panel.querySelector('#jm-cfg-need').value = config.needKeywords.join('\n');
    panel.querySelector('#jm-cfg-area').value = config.areaKeywords.join('\n');
    panel.querySelector('#jm-cfg-both').checked = config.requireBoth;
    panel.querySelector('#jm-cfg-sound').checked = config.enableSound;
    panel.querySelector('#jm-cfg-notify').checked = config.enableNotify;
    panel.querySelector('#jm-cfg-highlight').checked = config.highlightInPage;
    panel.querySelector('#jm-cfg-autoscroll').checked = config.autoScroll;
    panel.querySelector('#jm-cfg-scrollint').value = config.autoScrollInterval;
    panel.querySelector('#jm-cfg-night').checked = config.quietNight;
  }
  fillConfigUI();

  panel.querySelector('#jm-btn-save-cfg').onclick = () => {
    config.needKeywords = splitKws(panel.querySelector('#jm-cfg-need').value);
    config.areaKeywords = splitKws(panel.querySelector('#jm-cfg-area').value);
    config.requireBoth = panel.querySelector('#jm-cfg-both').checked;
    config.enableSound = panel.querySelector('#jm-cfg-sound').checked;
    config.enableNotify = panel.querySelector('#jm-cfg-notify').checked;
    config.highlightInPage = panel.querySelector('#jm-cfg-highlight').checked;
    config.autoScroll = panel.querySelector('#jm-cfg-autoscroll').checked;
    config.autoScrollInterval = Math.max(15, parseInt(panel.querySelector('#jm-cfg-scrollint').value) || 45);
    config.quietNight = panel.querySelector('#jm-cfg-night').checked;
    saveJSON(STORAGE_KEY_CONFIG, config);
    setupAutoScroll();
    updateQuickBtns();
    toast('配置已保存');
  };
  panel.querySelector('#jm-btn-reset-cfg').onclick = () => {
    if (!confirm('恢复默认配置？')) return;
    config = Object.assign({}, DEFAULT_CONFIG);
    saveJSON(STORAGE_KEY_CONFIG, config);
    fillConfigUI();
    setupAutoScroll();
    updateQuickBtns();
  };
  panel.querySelector('#jm-btn-scan').onclick = () => scan(true);
  panel.querySelector('#jm-btn-clear-seen').onclick = () => {
    if (!confirm('清除已见缓存后会重新扫描页面所有内容，可能会产生大量命中提醒，确定？')) return;
    seen.clear(); saveJSON(STORAGE_KEY_SEEN, []); toast('已清除');
  };
  panel.querySelector('#jm-btn-clear-hits').onclick = () => {
    if (!confirm('清空命中记录？建议先导出 CSV')) return;
    hits = []; saveJSON(STORAGE_KEY_HITS, hits); renderHits();
  };
  panel.querySelector('#jm-btn-export').onclick = exportCSV;

  // 顶部快捷开关
  function updateQuickBtns() {
    const $scan = panel.querySelector('#jm-q-scan');
    const $scroll = panel.querySelector('#jm-q-scroll');
    const $sound = panel.querySelector('#jm-q-sound');
    $scan.classList.toggle('on', scanEnabled);
    $scan.querySelector('span:last-child').textContent = scanEnabled ? '扫描中' : '已暂停';
    $scroll.classList.toggle('on', config.autoScroll);
    $scroll.classList.toggle('warn', config.autoScroll);
    $scroll.querySelector('span:last-child').textContent = config.autoScroll ? `滚动中(${config.autoScrollInterval}s)` : '自动滚动';
    $sound.classList.toggle('on', config.enableSound);
    $sound.querySelector('span:last-child').textContent = config.enableSound ? '响铃开' : '静音';
  }
  panel.querySelector('#jm-q-scan').onclick = () => {
    scanEnabled = !scanEnabled;
    setupScanTimer();
    updateQuickBtns();
    toast(scanEnabled ? '已开启扫描' : '已暂停扫描');
  };
  panel.querySelector('#jm-q-scroll').onclick = () => {
    config.autoScroll = !config.autoScroll;
    saveJSON(STORAGE_KEY_CONFIG, config);
    setupAutoScroll();
    fillConfigUI();
    updateQuickBtns();
    toast(config.autoScroll ? `已开启自动滚动（每${config.autoScrollInterval}秒）` : '已关闭自动滚动');
  };
  panel.querySelector('#jm-q-sound').onclick = () => {
    config.enableSound = !config.enableSound;
    saveJSON(STORAGE_KEY_CONFIG, config);
    fillConfigUI();
    updateQuickBtns();
    toast(config.enableSound ? '响铃已开启' : '已静音');
  };

  function splitKws(s) {
    return s.split(/[,，\n]/).map(x => x.trim()).filter(Boolean);
  }

  // ==================== 核心：扫描逻辑 ====================
  function detectPageType() {
    const href = location.href;
    if (/\/search_result/.test(href)) return 'search';
    if (/\/explore\//.test(href) || /\/discovery\/item\//.test(href)) return 'note';
    if (/\/user\/profile\//.test(href)) return 'user';
    return 'other';
  }

  // 提取"一块内容" —— 返回 [{id, author, text, element, url}]
  function extractCandidates() {
    const pageType = detectPageType();
    const out = [];

    // 1) 搜索结果页：笔记卡片
    if (pageType === 'search' || pageType === 'other') {
      // 小红书笔记卡片常见结构：section.note-item / a[href*="/explore/"] / a[href*="/search_result/"]
      const cards = document.querySelectorAll(
        'section.note-item, .note-item, a.cover[href], a[href*="/explore/"], a[href*="/search_result/"]'
      );
      cards.forEach(el => {
        if (el.dataset._jmScanned === '1') return;
        const text = (el.innerText || '').trim();
        if (!text || text.length < 5) return;
        // 作者
        const authorEl = el.querySelector('.author, [class*="author"], .name, [class*="user-name"]');
        const author = authorEl ? authorEl.innerText.trim() : '';
        // 找链接：卡片本身是 <a> 或者内部有带 xsec_token 的 <a>
        const url = extractNoteUrl(el);
        const id = hashStr(url + '|' + text.slice(0, 80));
        out.push({ id, author, text, element: el, url, type: 'note-card' });
      });
    }

    // 2) 笔记详情页：笔记正文 + 评论
    if (pageType === 'note' || pageType === 'other') {
      // 笔记正文 —— 多种可能的结构
      const noteTextEl = document.querySelector('#detail-desc, .note-content, [class*="desc"]');
      if (noteTextEl && noteTextEl.dataset._jmScanned !== '1') {
        const text = noteTextEl.innerText.trim();
        if (text.length >= 5) {
          const authorEl = document.querySelector('.author-wrapper .username, .user-nickname, [class*="user-name"]');
          const author = authorEl ? authorEl.innerText.trim() : '';
          const id = hashStr(location.href + '|note|' + text.slice(0, 80));
          out.push({ id, author, text, element: noteTextEl, url: location.href, type: 'note-body' });
        }
      }
      // 评论
      document.querySelectorAll('.comment-item, [class*="comment-item"], .parent-comment, .sub-comment').forEach(el => {
        if (el.dataset._jmScanned === '1') return;
        const text = (el.innerText || '').trim();
        if (!text || text.length < 3) return;
        // 作者
        const authorEl = el.querySelector('.name, [class*="name"], .user-name');
        const author = authorEl ? authorEl.innerText.trim() : '';
        const id = hashStr(location.href + '|cmt|' + text.slice(0, 80));
        out.push({ id, author, text, element: el, url: location.href, type: 'comment' });
      });
    }

    // 标记已扫描
    out.forEach(o => { if (o.element) o.element.dataset._jmScanned = '1'; });
    return out;
  }

  // 从笔记卡片元素里抠出真正可直接跳转的链接
  // 小红书 2024+ 的笔记 URL 必须带 xsec_token,不然打开会被重定向
  function extractNoteUrl(el) {
    // 优先找带 xsec_token 的 a
    const candidates = [];
    if (el.tagName === 'A' && el.href) candidates.push(el);
    candidates.push(...el.querySelectorAll('a[href]'));
    // 去重并打分:带 xsec_token 的优先,其次是 /explore/ 或 /search_result/
    let best = null, bestScore = -1;
    for (const a of candidates) {
      const href = a.getAttribute('href') || '';
      if (!href || href.startsWith('#') || href.startsWith('javascript:')) continue;
      let score = 0;
      if (/xsec_token/.test(href)) score += 10;
      if (/\/explore\//.test(href)) score += 5;
      if (/\/search_result\//.test(href)) score += 4;
      if (/\/discovery\/item\//.test(href)) score += 3;
      if (a.classList.contains('cover') || a.querySelector('img')) score += 1;
      if (score > bestScore) { bestScore = score; best = a; }
    }
    if (best) {
      try { return new URL(best.getAttribute('href'), location.origin).href; }
      catch { return location.href; }
    }
    return location.href;
  }

  function matchKeywords(text, keywords) {
    const t = text.toLowerCase();
    return keywords.filter(k => t.includes(k.toLowerCase()));
  }

  function scan(manual = false) {
    const candidates = extractCandidates();
    let newHitCount = 0;
    for (const c of candidates) {
      if (seen.has(c.id)) continue;
      seen.add(c.id);
      const needHit = matchKeywords(c.text, config.needKeywords);
      const areaHit = matchKeywords(c.text, config.areaKeywords);
      const isHit = config.requireBoth
        ? (needHit.length > 0 && areaHit.length > 0)
        : (needHit.length > 0 || areaHit.length > 0);
      if (isHit) {
        const hit = {
          id: c.id,
          at: Date.now(),
          type: c.type,
          author: c.author,
          text: c.text.slice(0, 500),
          url: c.url,
          needMatch: needHit,
          areaMatch: areaHit,
        };
        hits.unshift(hit);
        newHitCount++;
        if (c.element) {
          // 打 id 方便"定位"按钮用
          c.element.setAttribute('data-_jm-hit-id', c.id);
          if (config.highlightInPage) {
            c.element.classList.add('jm-radar-highlight');
          }
        }
      }
    }
    if (hits.length > 500) hits = hits.slice(0, 500); // 限制长度
    if (seen.size > 5000) seen = new Set(Array.from(seen).slice(-3000)); // 限制内存
    saveJSON(STORAGE_KEY_HITS, hits);
    saveJSON(STORAGE_KEY_SEEN, Array.from(seen));

    scanCount++;
    lastScanAt = Date.now();

    if (newHitCount > 0 && !isQuiet()) {
      if (config.enableSound) beep(newHitCount);
      if (config.enableNotify) {
        try {
          GM_notification({
            title: `🎯 发现 ${newHitCount} 条集美租房线索`,
            text: hits[0].text.slice(0, 80),
            timeout: 8000,
          });
        } catch {}
      }
    }
    renderAll();
    if (manual) toast(`扫描完成：新增 ${newHitCount} 条命中`);
  }

  function isQuiet() {
    if (!config.quietNight) return false;
    const h = new Date().getHours();
    return h >= 23 || h < 7;
  }

  // ==================== 渲染 ====================
  function renderAll() {
    renderHits();
    renderStatus();
  }
  function renderHits() {
    const wrap = panel.querySelector('#jm-hits-list');
    const badge = panel.querySelector('#jm-hits-badge');
    if (!hits.length) {
      wrap.innerHTML = '<div class="jm-empty">暂无命中线索<br/><br/>在小红书搜索"集美 租房"等关键词即可自动开始扫描</div>';
      badge.style.display = 'none';
      return;
    }
    badge.textContent = hits.length;
    badge.style.display = '';
    wrap.innerHTML = hits.slice(0, 50).map((h, i) => {
      const highlighted = highlightMatches(escapeHtml(h.text), [...h.needMatch, ...h.areaMatch]);
      const isNew = (Date.now() - h.at) < 5000 && i < 3;
      // 判断链接是不是"真正的笔记链接"(而不只是当前页地址)
      const hasRealLink = h.url && h.url !== location.href && /\/explore\/|\/search_result\/|\/discovery\/item\//.test(h.url);
      const openBtn = hasRealLink
        ? `<a class="h-btn primary" data-act="open" data-idx="${i}" href="${escapeHtml(h.url)}" target="_blank" rel="noopener noreferrer">打开原帖</a>`
        : `<a class="h-btn primary disabled" title="这条没抓到直接链接(可能是笔记正文/评论),请去当前页找" data-idx="${i}">无直链</a>`;
      return `
        <div class="jm-hit ${isNew ? 'new' : ''}">
          <div class="h-meta">
            <span style="color:#ef4444;font-weight:500;">${typeLabel(h.type)}</span>
             · ${fmtRelative(h.at)}
             · 命中：${[...h.needMatch, ...h.areaMatch].map(k => `<span style="background:#fde047;padding:0 3px;border-radius:2px;">${escapeHtml(k)}</span>`).join(' ')}
          </div>
          ${h.author ? `<div><span class="h-author">@${escapeHtml(h.author)}</span></div>` : ''}
          <div class="h-text">${highlighted}</div>
          <div class="h-actions">
            ${openBtn}
            <button class="h-btn" data-act="copy" data-idx="${i}">复制</button>
            <button class="h-btn" data-act="locate" data-idx="${i}">定位</button>
            <button class="h-btn" data-act="del" data-idx="${i}">忽略</button>
          </div>
        </div>
      `;
    }).join('');
    // 绑定按钮
    wrap.querySelectorAll('.h-btn').forEach(btn => {
      // 打开原帖用 a 标签的默认行为,不拦截
      if (btn.dataset.act === 'open') return;
      btn.onclick = (e) => {
        e.preventDefault();
        const i = parseInt(btn.dataset.idx);
        const h = hits[i];
        if (!h) return;
        if (btn.dataset.act === 'copy') {
          const payload = `[${typeLabel(h.type)}] @${h.author || ''}\n${h.text}\n${h.url}`;
          navigator.clipboard.writeText(payload).then(() => toast('已复制'));
        } else if (btn.dataset.act === 'locate') {
          // 滚动到页面对应元素
          const el = document.querySelector(`[data-_jm-hit-id="${h.id}"]`);
          if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            el.classList.add('jm-radar-highlight');
            setTimeout(() => el.classList.remove('jm-radar-highlight'), 3000);
            el.classList.add('jm-radar-highlight');
            toast('已定位到页面');
          } else {
            toast('此条已不在当前页面 DOM 中（可能页面已滚动过）');
          }
        } else if (btn.dataset.act === 'del') {
          hits.splice(i, 1);
          saveJSON(STORAGE_KEY_HITS, hits);
          renderHits();
        }
      };
    });
  }
  function renderStatus() {
    panel.querySelector('#jm-s-scans').textContent = scanCount;
    panel.querySelector('#jm-s-seen').textContent = seen.size;
    panel.querySelector('#jm-s-hits').textContent = hits.length;
    panel.querySelector('#jm-s-pagetype').textContent = {
      search: '搜索页', note: '笔记详情', user: '用户主页', other: '其他'
    }[detectPageType()];
    panel.querySelector('#jm-s-lastscan').textContent = lastScanAt ? fmtRelative(lastScanAt) : '未开始';
    panel.querySelector('#jm-s-uptime').textContent = fmtDuration(Date.now() - startAt);
  }

  function typeLabel(t) {
    return { 'note-card': '笔记卡', 'note-body': '笔记正文', 'comment': '评论' }[t] || t;
  }

  // ==================== 自动滚动 ====================
  function setupAutoScroll() {
    if (autoScrollTimer) { clearInterval(autoScrollTimer); autoScrollTimer = null; }
    if (!config.autoScroll) return;
    autoScrollTimer = setInterval(() => {
      const doc = document.documentElement;
      const nearBottom = window.scrollY + window.innerHeight >= doc.scrollHeight - 200;
      if (nearBottom) {
        // 到底了,稍等再滚回顶部触发重新加载
        window.scrollTo({ top: 0, behavior: 'smooth' });
      } else {
        // 模拟真人:随机滚动距离
        const dist = 400 + Math.random() * 500;
        window.scrollBy({ top: dist, behavior: 'smooth' });
      }
    }, config.autoScrollInterval * 1000);
  }

  // ==================== 工具 ====================
  function hashStr(s) {
    let h = 0;
    for (let i = 0; i < s.length; i++) h = ((h << 5) - h + s.charCodeAt(i)) | 0;
    return h.toString(36);
  }
  function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, c => ({
      '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'
    }[c]));
  }
  function highlightMatches(html, keywords) {
    let out = html;
    const sorted = [...new Set(keywords)].sort((a,b) => b.length - a.length);
    for (const k of sorted) {
      if (!k) continue;
      const re = new RegExp(escapeRegex(k), 'gi');
      out = out.replace(re, m => `<mark>${m}</mark>`);
    }
    return out;
  }
  function escapeRegex(s) { return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); }
  function fmtRelative(ts) {
    const d = Date.now() - ts;
    const m = Math.floor(d / 60000);
    if (m < 1) return '刚刚';
    if (m < 60) return m + '分钟前';
    const h = Math.floor(m / 60);
    if (h < 24) return h + '小时前';
    return Math.floor(h / 24) + '天前';
  }
  function fmtDuration(ms) {
    const s = Math.floor(ms / 1000);
    if (s < 60) return s + '秒';
    const m = Math.floor(s / 60);
    if (m < 60) return m + '分' + (s % 60) + '秒';
    const h = Math.floor(m / 60);
    return h + '时' + (m % 60) + '分';
  }

  // 响铃（用 Web Audio API，不需要音频文件）
  let audioCtx = null;
  function beep(count = 1) {
    try {
      audioCtx = audioCtx || new (window.AudioContext || window.webkitAudioContext)();
      const times = Math.min(count, 3);
      for (let i = 0; i < times; i++) {
        setTimeout(() => {
          const osc = audioCtx.createOscillator();
          const gain = audioCtx.createGain();
          osc.connect(gain); gain.connect(audioCtx.destination);
          osc.frequency.value = 880;
          osc.type = 'sine';
          gain.gain.setValueAtTime(0.25, audioCtx.currentTime);
          gain.gain.exponentialRampToValueAtTime(0.01, audioCtx.currentTime + 0.3);
          osc.start();
          osc.stop(audioCtx.currentTime + 0.3);
        }, i * 350);
      }
    } catch (e) {}
  }

  function toast(msg) {
    const t = document.createElement('div');
    t.textContent = msg;
    t.style.cssText = 'position:fixed;bottom:30px;left:50%;transform:translateX(-50%);background:rgba(0,0,0,0.8);color:#fff;padding:8px 16px;border-radius:20px;z-index:9999999;font-size:13px;';
    document.body.appendChild(t);
    setTimeout(() => t.remove(), 2000);
  }

  function makeDraggable(el, handle) {
    let ox = 0, oy = 0, startX = 0, startY = 0, dragging = false;
    handle.addEventListener('mousedown', e => {
      if (e.target.tagName === 'BUTTON') return;
      dragging = true;
      const r = el.getBoundingClientRect();
      ox = r.left; oy = r.top;
      startX = e.clientX; startY = e.clientY;
      e.preventDefault();
    });
    document.addEventListener('mousemove', e => {
      if (!dragging) return;
      const nx = ox + (e.clientX - startX);
      const ny = oy + (e.clientY - startY);
      el.style.left = Math.max(0, Math.min(window.innerWidth - el.offsetWidth, nx)) + 'px';
      el.style.top = Math.max(0, Math.min(window.innerHeight - 40, ny)) + 'px';
      el.style.right = 'auto';
    });
    document.addEventListener('mouseup', () => { dragging = false; });
  }

  function exportCSV() {
    if (!hits.length) return toast('暂无数据');
    const headers = ['时间', '类型', '作者', '内容', '命中关键词', '链接'];
    const rows = hits.map(h => [
      new Date(h.at).toLocaleString('zh-CN'),
      typeLabel(h.type),
      h.author || '',
      h.text,
      [...h.needMatch, ...h.areaMatch].join(';'),
      h.url,
    ]);
    const csv = [headers, ...rows].map(r =>
      r.map(c => `"${String(c || '').replace(/"/g, '""')}"`).join(',')
    ).join('\n');
    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `集美租房线索_${Date.now()}.csv`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 100);
  }

  // ==================== 启动 ====================
  const startAt = Date.now();

  // 周期扫描（受 scanEnabled 控制，可暂停）
  function setupScanTimer() {
    if (scanTimer) { clearInterval(scanTimer); scanTimer = null; }
    if (!scanEnabled) return;
    scanTimer = setInterval(() => scan(false), 5000);
  }
  setupScanTimer();
  // 初始扫描
  setTimeout(() => { if (scanEnabled) scan(false); }, 1500);
  // 状态面板定时刷新
  setInterval(renderStatus, 1000);

  // MutationObserver：监听小红书 SPA 路由切换 / 新内容加载
  const obs = new MutationObserver(() => {
    // 防抖：由定时 scan 兜底，这里仅在内容剧烈变化时触发一次
  });
  obs.observe(document.body, { childList: true, subtree: true });

  setupAutoScroll();
  renderAll();
  updateQuickBtns();

  console.log('[集美截流雷达] 已启动', config);
})();

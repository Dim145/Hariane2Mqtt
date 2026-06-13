/*!
 * Hariane Water Card — a Lovelace card for Home Assistant
 * Shows daily water consumption (from Hariane2Mqtt long-term statistics) in the
 * spirit of content-card-linky / lovelace-gazpar-card: a hero reading, period
 * tiles (week / month / year) with trends, and a bar chart of recent days.
 *
 * Data source: the external statistic imported by Hariane2Mqtt
 * (e.g. `hariane:water_123456789`). Requires IMPORT_ENERGY_STATISTICS enabled.
 *
 * Config:
 *   type: custom:hariane-water-card
 *   statistic_id: hariane:water_123456789   # required
 *   title: Eau                              # optional
 *   days: 14                                # optional, bars in the chart
 *   unit: m³                                # optional
 *   price_per_m3: 4.30                      # optional, shows estimated cost
 *   show_header: true                       # optional
 *   show_icon: true                         # optional
 *   color: '#22d3ee'                        # optional accent override
 */

const VERSION = "1.0.0";

const DEFAULTS = {
  title: "Eau",
  days: 14,
  unit: "m³",
  show_header: true,
  show_icon: true,
};

class HarianeWaterCard extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: "open" });
    this._lastFetch = 0;
    this._fetching = false;
    this._animated = false;
    this._daily = [];
    this._monthly = [];
  }

  static getConfigElement() {
    return document.createElement("hariane-water-card-editor");
  }

  static getStubConfig() {
    return { statistic_id: "hariane:water_123456789", title: "Eau", days: 14 };
  }

  setConfig(config) {
    if (!config || !config.statistic_id) {
      throw new Error("Définissez 'statistic_id' (ex: hariane:water_123456789).");
    }
    this._config = { ...DEFAULTS, ...config };
    this._lastFetch = 0;
    this._animated = false;
    this._buildShell();
    if (this._hass) this._refresh(true);
  }

  set hass(hass) {
    this._hass = hass;
    if (!this.shadowRoot.firstChild) this._buildShell();
    const stale = Date.now() - this._lastFetch > 5 * 60 * 1000;
    if (stale) this._refresh();
  }

  getCardSize() {
    return 5;
  }

  // ---- data ---------------------------------------------------------------

  async _refresh(force = false) {
    if (this._fetching || !this._hass || !this._config) return;
    this._fetching = true;
    try {
      await this._fetchStatistics();
      this._lastFetch = Date.now();
      this._render();
    } catch (err) {
      this._renderError(err);
    } finally {
      this._fetching = false;
    }
  }

  async _fetchStatistics() {
    const id = this._config.statistic_id;
    const now = new Date();

    const dayStart = new Date(now);
    dayStart.setHours(0, 0, 0, 0);
    dayStart.setDate(dayStart.getDate() - (Math.max(this._config.days, 35) + 2));

    // Two calendar years of months: enables this-year, last-year and YoY.
    const monthStart = new Date(now.getFullYear() - 1, 0, 1, 0, 0, 0, 0);

    const query = (start, period) =>
      this._hass.callWS({
        type: "recorder/statistics_during_period",
        start_time: start.toISOString(),
        end_time: now.toISOString(),
        statistic_ids: [id],
        period,
      });

    const [daily, monthly] = await Promise.all([query(dayStart, "day"), query(monthStart, "month")]);

    this._daily = this._changes((daily && daily[id]) || []);
    this._monthly = this._changes((monthly && monthly[id]) || []);
    this._latestSum = this._lastSum((daily && daily[id]) || []);
  }

  // Turn raw statistic rows into [{ date, value }] consumption deltas.
  _changes(rows) {
    const out = [];
    for (let i = 0; i < rows.length; i++) {
      const r = rows[i];
      let v = null;
      if (r.change !== undefined && r.change !== null) v = r.change;
      else if (i > 0 && r.sum != null && rows[i - 1].sum != null) v = r.sum - rows[i - 1].sum;
      if (v === null) continue; // drop the leading baseline row with no delta
      out.push({ date: new Date(r.start), value: Math.max(0, v) });
    }
    return out;
  }

  _lastSum(rows) {
    for (let i = rows.length - 1; i >= 0; i--) if (rows[i].sum != null) return rows[i].sum;
    return null;
  }

  // ---- derived figures ----------------------------------------------------

  _compute() {
    const d = this._daily;
    const m = this._monthly;
    const now = new Date();

    const latest = d.length ? d[d.length - 1] : null;

    const weekStart = this._startOfWeek(now);
    const prevWeekStart = new Date(weekStart);
    prevWeekStart.setDate(prevWeekStart.getDate() - 7);
    const sumBetween = (arr, from, to) =>
      arr.filter((x) => x.date >= from && x.date < to).reduce((a, x) => a + x.value, 0);

    const thisWeek = sumBetween(d, weekStart, new Date(now.getTime() + 86400000));
    const lastWeek = sumBetween(d, prevWeekStart, weekStart);

    const ym = (x) => x.date.getFullYear() * 12 + x.date.getMonth();
    const curYm = now.getFullYear() * 12 + now.getMonth();
    const thisMonth = m.filter((x) => ym(x) === curYm).reduce((a, x) => a + x.value, 0);
    const lastMonth = m.filter((x) => ym(x) === curYm - 1).reduce((a, x) => a + x.value, 0);

    const yearSum = (year, untilMonth) =>
      m.filter((x) => x.date.getFullYear() === year && x.date.getMonth() <= untilMonth)
        .reduce((a, x) => a + x.value, 0);
    const thisYear = yearSum(now.getFullYear(), now.getMonth());
    const lastYearYtd = yearSum(now.getFullYear() - 1, now.getMonth());

    return {
      latest,
      total: this._latestSum,
      week: { value: thisWeek, prev: lastWeek },
      month: { value: thisMonth, prev: lastMonth },
      year: { value: thisYear, prev: lastYearYtd },
      bars: d.slice(-this._config.days),
    };
  }

  // ---- rendering ----------------------------------------------------------

  _buildShell() {
    this.shadowRoot.innerHTML = `<style>${this._css()}</style>
      <ha-card><div class="root"></div></ha-card>`;
    this._root = this.shadowRoot.querySelector(".root");
    this._root.innerHTML = `<div class="state">Chargement…</div>`;
  }

  _renderError(err) {
    if (!this._root) return;
    this._root.innerHTML = `<div class="state err">${this._esc(String(err && err.message ? err.message : err))}</div>`;
  }

  _render() {
    if (!this._root) return;
    const c = this._config;
    const f = this._compute();

    if (!f.latest && !f.bars.length) {
      this._root.innerHTML = `<div class="state">
        <strong>Aucune donnée de statistique</strong>
        <span>Vérifiez <code>${this._esc(c.statistic_id)}</code> — activez <code>IMPORT_ENERGY_STATISTICS</code> et attendez le prochain relevé.</span>
      </div>`;
      return;
    }

    const max = Math.max(0.0001, ...f.bars.map((b) => b.value));
    const bars = f.bars
      .map((b, i) => {
        const h = Math.round((b.value / max) * 100);
        const wd = b.date.toLocaleDateString("fr-FR", { weekday: "narrow" }).toUpperCase();
        const dom = b.date.getDate();
        return `<div class="col" style="--h:${h}%;--i:${i}">
            <div class="tip">${this._fmt(b.value)} ${this._esc(c.unit)}<small>${b.date.toLocaleDateString("fr-FR")}</small></div>
            <div class="bar"></div>
            <div class="x"><b>${dom}</b><span>${this._esc(wd)}</span></div>
          </div>`;
      })
      .join("");

    const cost =
      c.price_per_m3 && f.latest
        ? `<div class="cost">≈ ${this._fmt(f.month.value * c.price_per_m3, 2)} € ce mois<span>· ${this._fmt(c.price_per_m3, 2)} €/${this._esc(c.unit)}</span></div>`
        : "";

    const header = c.show_header
      ? `<header>
           <div class="brand">${c.show_icon ? this._drop() : ""}<span class="title">${this._esc(c.title)}</span></div>
           ${f.latest ? `<div class="upd">relevé du ${f.latest.date.toLocaleDateString("fr-FR", { day: "2-digit", month: "short" })}</div>` : ""}
         </header>`
      : "";

    this._root.innerHTML = `
      <div class="card${this._animated ? "" : " enter"}">
        ${header}
        <section class="hero">
          ${this._waves()}
          <div class="hlabel">Dernier relevé</div>
          <div class="hval"><span class="num">${f.latest ? this._fmt(f.latest.value) : "—"}</span><span class="unit">${this._esc(c.unit)}</span></div>
          ${f.total != null ? `<div class="htot">Total compteur · <b>${this._fmt(f.total, 1)} ${this._esc(c.unit)}</b></div>` : ""}
        </section>
        <section class="tiles">
          ${this._tile("Semaine", f.week, c.unit)}
          ${this._tile("Mois", f.month, c.unit)}
          ${this._tile("Année", f.year, c.unit)}
        </section>
        <section class="chart"><div class="bars">${bars}</div></section>
        ${cost}
      </div>`;

    this._animated = true;
  }

  _tile(label, data, unit) {
    const t = this._trend(data.value, data.prev);
    return `<div class="tile">
      <div class="tlabel">${label}</div>
      <div class="tval">${this._fmt(data.value)}<span>${this._esc(unit)}</span></div>
      ${t}
    </div>`;
  }

  _trend(cur, prev) {
    if (!prev || prev <= 0) return `<div class="trend flat">—</div>`;
    const pct = Math.round(((cur - prev) / prev) * 100);
    if (pct === 0) return `<div class="trend flat">±0 %</div>`;
    const up = pct > 0;
    return `<div class="trend ${up ? "up" : "down"}">${up ? "▲" : "▼"} ${Math.abs(pct)} %</div>`;
  }

  // ---- helpers ------------------------------------------------------------

  _startOfWeek(d) {
    const x = new Date(d);
    const day = (x.getDay() + 6) % 7; // Monday = 0
    x.setHours(0, 0, 0, 0);
    x.setDate(x.getDate() - day);
    return x;
  }

  _fmt(v, digits = 2) {
    if (v == null || isNaN(v)) return "—";
    return v.toLocaleString("fr-FR", { minimumFractionDigits: digits, maximumFractionDigits: digits });
  }

  _esc(s) {
    return String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  }

  _drop() {
    return `<svg class="drop" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 2.5S5 10 5 14.5a7 7 0 0 0 14 0C19 10 12 2.5 12 2.5z"/></svg>`;
  }

  _waves() {
    return `<svg class="waves" viewBox="0 0 400 60" preserveAspectRatio="none" aria-hidden="true">
      <path class="w1" d="M0 30 Q50 12 100 30 T200 30 T300 30 T400 30 V60 H0 Z"/>
      <path class="w2" d="M0 36 Q50 20 100 36 T200 36 T300 36 T400 36 V60 H0 Z"/>
    </svg>`;
  }

  _css() {
    return `
    :host { --c1: var(--hariane-c1, #0e7490); --c2: var(--hariane-c2, ${this._config && this._config.color ? this._config.color : "#22d3ee"});
            --txt: var(--primary-text-color, #111); --sub: var(--secondary-text-color, #666);
            --line: var(--divider-color, rgba(127,127,127,.18)); }
    ha-card { overflow: hidden; }
    .root { font-family: "Hanken Grotesk", var(--paper-font-body1_-_font-family, system-ui), sans-serif; }
    @import url('https://fonts.googleapis.com/css2?family=Hanken+Grotesk:wght@400;500;700;800&display=swap');
    .card { padding: 14px 16px 16px; }
    .state { padding: 28px 18px; color: var(--sub); display:flex; flex-direction:column; gap:6px; text-align:center; }
    .state.err { color: var(--error-color, #c0392b); }
    .state code { background: var(--line); padding: 1px 5px; border-radius: 5px; font-size: .85em; }

    header { display:flex; align-items:center; justify-content:space-between; margin-bottom: 10px; }
    .brand { display:flex; align-items:center; gap:9px; }
    .drop { width: 22px; height: 22px; fill: url(#none); }
    .drop { fill: var(--c2); filter: drop-shadow(0 1px 2px color-mix(in srgb, var(--c1) 50%, transparent)); }
    .title { font-weight: 800; letter-spacing:.2px; color: var(--txt); font-size: 1.05rem; }
    .upd { font-size:.72rem; color: var(--sub); text-transform: uppercase; letter-spacing:.6px; }

    .hero { position:relative; border-radius: 16px; padding: 16px 16px 26px;
            background: linear-gradient(135deg, var(--c1), var(--c2)); color:#fff; overflow:hidden;
            box-shadow: 0 10px 24px -12px color-mix(in srgb, var(--c1) 80%, transparent); }
    .hlabel { font-size:.72rem; text-transform:uppercase; letter-spacing:1.4px; opacity:.85; }
    .hval { display:flex; align-items:baseline; gap:8px; margin-top:2px; }
    .hval .num { font-size: 2.9rem; font-weight: 800; line-height:1; font-variant-numeric: tabular-nums; text-shadow:0 2px 10px rgba(0,0,0,.18); }
    .hval .unit { font-size:1rem; font-weight:600; opacity:.9; }
    .htot { margin-top:8px; font-size:.8rem; opacity:.92; position:relative; z-index:2; }
    .htot b { font-variant-numeric: tabular-nums; }
    .waves { position:absolute; left:0; right:0; bottom:-1px; width:100%; height:46px; z-index:1; }
    .waves path { fill: rgba(255,255,255,.16); }
    .waves .w2 { fill: rgba(255,255,255,.10); animation: drift 9s ease-in-out infinite alternate; }
    .waves .w1 { animation: drift 7s ease-in-out infinite alternate-reverse; }
    @keyframes drift { from { transform: translateX(-18px);} to { transform: translateX(18px);} }

    .tiles { display:grid; grid-template-columns: repeat(3,1fr); gap:7px; margin:14px 0 4px; }
    .tile { background: color-mix(in srgb, var(--c2) 8%, transparent); border:1px solid var(--line);
            border-radius:12px; padding:8px 9px; min-width:0; }
    .tlabel { font-size:.66rem; text-transform:uppercase; letter-spacing:.6px; color:var(--sub); white-space:nowrap; }
    .tval { font-size:1.06rem; font-weight:800; color:var(--txt); font-variant-numeric:tabular-nums; margin-top:2px; white-space:nowrap; }
    .tval span { font-size:.68rem; font-weight:600; color:var(--sub); margin-left:2px; }
    .trend { font-size:.7rem; font-weight:700; margin-top:4px; display:inline-flex; gap:3px; padding:1px 6px; border-radius:20px; }
    .trend.up { color:#b45309; background: color-mix(in srgb, #f59e0b 18%, transparent); }
    .trend.down { color:#0e7490; background: color-mix(in srgb, var(--c2) 22%, transparent); }
    .trend.flat { color:var(--sub); background: var(--line); }

    .chart { margin-top:14px; }
    .bars { display:flex; align-items:flex-end; gap:4px; height:108px; padding-top:18px; }
    .col { flex:1; min-width:0; height:100%; display:flex; flex-direction:column; justify-content:flex-end; align-items:center; position:relative; }
    .bar { width:100%; max-width:26px; height:var(--h); min-height:3px; border-radius:6px 6px 3px 3px;
           background: linear-gradient(var(--c2), var(--c1)); box-shadow: inset 0 0 0 1px rgba(255,255,255,.08);
           transition: filter .15s, transform .15s; }
    .col:hover .bar { filter: brightness(1.12) saturate(1.1); transform: scaleY(1.015); }
    .x { margin-top:6px; text-align:center; line-height:1.1; overflow:hidden; width:100%; }
    .x b { display:block; font-size:.68rem; font-weight:700; color:var(--txt); font-variant-numeric:tabular-nums; }
    .x span { font-size:.58rem; color:var(--sub); }
    .tip { position:absolute; bottom:calc(var(--h) + 6px); left:50%; transform:translateX(-50%) translateY(4px);
           background:var(--txt); color:var(--card-background-color,#fff); padding:3px 7px; border-radius:7px;
           font-size:.7rem; font-weight:700; white-space:nowrap; opacity:0; pointer-events:none; transition:opacity .15s, transform .15s; z-index:5; }
    .tip small { display:block; font-weight:500; opacity:.75; font-size:.62rem; }
    .col:hover .tip { opacity:1; transform:translateX(-50%) translateY(0); }

    .cost { margin-top:12px; text-align:center; font-weight:700; color:var(--txt); font-size:.9rem; }
    .cost span { color:var(--sub); font-weight:500; font-size:.8rem; margin-left:4px; }

    /* one-shot entrance */
    .card.enter .hero { animation: rise .5s cubic-bezier(.2,.7,.2,1) both; }
    .card.enter .tile { animation: rise .5s cubic-bezier(.2,.7,.2,1) both; }
    .card.enter .tile:nth-child(2){ animation-delay:.05s } .card.enter .tile:nth-child(3){ animation-delay:.1s }
    .card.enter .col .bar { animation: grow .6s cubic-bezier(.2,.8,.2,1) both; animation-delay: calc(var(--i) * 28ms); transform-origin:bottom; }
    @keyframes rise { from { opacity:0; transform:translateY(10px);} to { opacity:1; transform:none;} }
    @keyframes grow { from { transform: scaleY(0);} to { transform: scaleY(1);} }
    @media (prefers-reduced-motion: reduce){ * { animation:none !important; } }
    `;
  }
}

customElements.define("hariane-water-card", HarianeWaterCard);

/** Visual (GUI) config editor shown in the Lovelace dashboard. */
class HarianeWaterCardEditor extends HTMLElement {
  setConfig(config) {
    this._config = { ...config };
    if (!this._built) {
      this._build();
      this._built = true;
    }
    this._sync();
  }

  set hass(hass) {
    this._hass = hass;
  }

  _build() {
    this.innerHTML = `
      <style>
        .he-form { display: grid; gap: 12px; padding: 8px 0; }
        .he-row { display: flex; flex-direction: column; gap: 4px; }
        .he-row > label { font-size: .8rem; font-weight: 600; color: var(--secondary-text-color, #667); }
        .he-row input[type=text], .he-row input[type=number] {
          padding: 8px 10px; border: 1px solid var(--divider-color, #ccc); border-radius: 8px;
          background: var(--card-background-color, #fff); color: var(--primary-text-color, #111); font: inherit;
        }
        .he-check { flex-direction: row; align-items: center; gap: 8px; font-size: .9rem; }
        .he-hint { font-size: .72rem; color: var(--secondary-text-color, #888); }
      </style>
      <div class="he-form">
        <div class="he-row">
          <label>statistic_id *</label>
          <input type="text" data-key="statistic_id" placeholder="hariane:water_123456789" />
          <span class="he-hint">Statistique importée par Hariane2Mqtt (option import_energy_statistics).</span>
        </div>
        <div class="he-row"><label>Titre</label><input type="text" data-key="title" placeholder="Eau" /></div>
        <div class="he-row"><label>Jours affichés</label><input type="number" data-key="days" min="1" max="60" /></div>
        <div class="he-row"><label>Unité</label><input type="text" data-key="unit" placeholder="m³" /></div>
        <div class="he-row"><label>Prix au m³ (optionnel)</label><input type="number" data-key="price_per_m3" step="0.01" min="0" /></div>
        <div class="he-row"><label>Couleur d'accent (optionnel)</label><input type="text" data-key="color" placeholder="#22d3ee" /></div>
        <label class="he-row he-check"><input type="checkbox" data-key="show_header" /> Afficher l'en-tête</label>
        <label class="he-row he-check"><input type="checkbox" data-key="show_icon" /> Afficher l'icône</label>
      </div>`;

    this.querySelectorAll("[data-key]").forEach((el) => {
      const evt = el.type === "checkbox" ? "change" : "input";
      el.addEventListener(evt, () => this._update(el));
    });
  }

  _sync() {
    const booleans = new Set(["show_header", "show_icon"]);
    this.querySelectorAll("[data-key]").forEach((el) => {
      if (el === document.activeElement) return; // don't clobber the field being edited
      const key = el.dataset.key;
      const value = this._config[key];
      if (el.type === "checkbox") el.checked = booleans.has(key) ? value !== false : !!value;
      else el.value = value === undefined || value === null ? "" : value;
    });
  }

  _update(el) {
    const key = el.dataset.key;
    let value;
    if (el.type === "checkbox") value = el.checked;
    else if (el.type === "number") value = el.value === "" ? undefined : Number(el.value);
    else value = el.value === "" ? undefined : el.value;

    if (value === undefined) delete this._config[key];
    else this._config[key] = value;

    this.dispatchEvent(new CustomEvent("config-changed", {
      detail: { config: this._config },
      bubbles: true,
      composed: true,
    }));
  }
}
customElements.define("hariane-water-card-editor", HarianeWaterCardEditor);

window.customCards = window.customCards || [];
window.customCards.push({
  type: "hariane-water-card",
  name: "Hariane Water Card",
  description: "Consommation d'eau Hariane (statistiques HA) : relevé, semaine/mois/année et historique journalier.",
  preview: true,
  documentationURL: "https://github.com/Dim145/Hariane2Mqtt",
});

console.info(`%c HARIANE-WATER-CARD %c v${VERSION} `, "background:#0e7490;color:#fff;border-radius:3px 0 0 3px;padding:2px 6px", "background:#22d3ee;color:#063;border-radius:0 3px 3px 0;padding:2px 6px");

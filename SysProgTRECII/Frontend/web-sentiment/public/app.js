const grid = document.getElementById('grid');
const stats = document.getElementById('stats');
const loadBtn = document.getElementById('loadBtn');
const feedEl = document.getElementById('feed');
const periodEl = document.getElementById('period');
const limitEl = document.getElementById('limit');

const API_BASE = 'http://localhost:5062'; // tvoj .NET backend

function badge(prob, isPos) {
  const p = (prob ?? 0).toFixed(3);
  if (isPos === true) return `<span class="badge pos">positive • ${p}</span>`;
  if (isPos === false) return `<span class="badge neg">negative • ${p}</span>`;
  return `<span class="badge neu">neutral • ${p}</span>`;
}

function render(items) {
  grid.innerHTML = items.map(it => {
    const published = it.published ? new Date(it.published).toISOString().slice(0,10) : '—';
    const s = it.sentiment || {};
    return `<article class="card">
      <div class="meta">${published}</div>
      <div class="title">${it.title}</div>
      <div class="abstract">${it.abstract_ || ''}</div>
      <div class="badges">
        ${badge(s.probability, s.isPositive)}
        <a class="link" href="${it.url}" target="_blank" rel="noopener">Open article →</a>
      </div>
    </article>`;
  }).join('');
}

async function load() {
  loadBtn.disabled = true; loadBtn.textContent = 'Loading...';
  try {
    const feed = feedEl.value;
    const period = periodEl.value;
    const limit = limitEl.value;
    const url = `${API_BASE}/api/most-popular?feed=${encodeURIComponent(feed)}&period=${encodeURIComponent(period)}&limit=${encodeURIComponent(limit)}`;
    const r = await fetch(url);
    const data = await r.json();
    if (!r.ok) throw new Error(data.error || 'API error');
    const items = data.items || [];
    render(items);
    const pos = items.filter(i => i.sentiment?.isPositive === true).length;
    const neg = items.filter(i => i.sentiment?.isPositive === false).length;
    const neu = items.length - pos - neg;
    stats.classList.remove('hidden');
    stats.innerHTML = `<strong>Results:</strong> ${items.length} • Positive: ${pos} • Negative: ${neg} • Neutral: ${neu}`;
  } catch (e) {
    grid.innerHTML = `<div class="card">Greška: ${e.message}</div>`;
    stats.classList.add('hidden');
  } finally {
    loadBtn.disabled = false; loadBtn.textContent = 'Load';
  }
}

loadBtn.addEventListener('click', load);
load();

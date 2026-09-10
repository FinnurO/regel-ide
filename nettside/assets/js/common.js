/*!
 * Delte hjelpefunksjoner for DigRett-showcasen. Ingen avhengigheter, ingen
 * byggesteg — lastes rått i nettleseren på alle sider.
 */
var DigRettSite = (function () {
  'use strict';

  /** Finner riktig relativ sti til nettside/-roten fra en gitt side, basert på <html data-root>. */
  function rootPath() {
    var el = document.documentElement;
    return el.getAttribute('data-root') || './';
  }

  /** henter en JSON-fil under data/, med et forklarende feilobjekt hvis noe går galt. */
  function fetchData(relPath) {
    var url = rootPath() + 'data/' + relPath;
    return fetch(url).then(function (res) {
      if (!res.ok) throw new Error('Fant ikke ' + url + ' (HTTP ' + res.status + ')');
      return res.json();
    });
  }

  function esc(s) {
    if (s === null || s === undefined) return '';
    var d = document.createElement('div');
    d.textContent = String(s);
    return d.innerHTML;
  }

  /** Er dette datasettet et uendret skjelett (ikke eksportert mot ekte API)? */
  function isSkeleton(obj) {
    return !!(obj && typeof obj === 'object' && typeof obj._status === 'string' && obj._status.indexOf('IKKE EKSPORTERT') === 0);
  }

  function skeletonBanner(obj) {
    if (!isSkeleton(obj)) return '';
    return '<p class="skeleton-badge">Ikke eksportert ennå — kjør <code>nettside/tools/eksporter-data.ps1</code> mot en kjørende lokal instans av RegelIde.Api</p>';
  }

  function renderError(container, err) {
    container.innerHTML = '<p class="error-note">Klarte ikke å laste data: ' + esc(err.message || String(err)) + '</p>';
  }

  /** Enkelt case-/diakritikk-uavhengig fritekstsøk over et sett med strengfelt. */
  function norm(s) {
    return String(s == null ? '' : s)
      .toLowerCase()
      .normalize('NFD')
      .replace(/[̀-ͯ]/g, '');
  }

  function matches(query, fields) {
    if (!query) return true;
    var q = norm(query);
    return fields.some(function (f) { return norm(f).indexOf(q) !== -1; });
  }

  /** Sorter en tabell (thead th[data-sort-key]) v.h.a. en dataliste og en render-funksjon. */
  function makeSortableTable(theadRow, getRows, renderRows) {
    var state = { key: null, dir: 1 };
    var ths = Array.prototype.slice.call(theadRow.querySelectorAll('th[data-sort-key]'));
    ths.forEach(function (th) {
      th.setAttribute('tabindex', '0');
      th.setAttribute('role', 'button');
      function activate() {
        var key = th.getAttribute('data-sort-key');
        if (state.key === key) { state.dir = -state.dir; } else { state.key = key; state.dir = 1; }
        ths.forEach(function (t) { t.removeAttribute('aria-sort'); });
        th.setAttribute('aria-sort', state.dir === 1 ? 'ascending' : 'descending');
        var rows = getRows().slice().sort(function (a, b) {
          var av = a[key], bv = b[key];
          if (av == null) av = '';
          if (bv == null) bv = '';
          if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * state.dir;
          return String(av).localeCompare(String(bv), 'nb') * state.dir;
        });
        renderRows(rows);
      }
      th.addEventListener('click', activate);
      th.addEventListener('keydown', function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); activate(); } });
    });
  }

  function debounce(fn, ms) {
    var t;
    return function () {
      var args = arguments, ctx = this;
      clearTimeout(t);
      t = setTimeout(function () { fn.apply(ctx, args); }, ms);
    };
  }

  return {
    fetchData: fetchData,
    esc: esc,
    isSkeleton: isSkeleton,
    skeletonBanner: skeletonBanner,
    renderError: renderError,
    matches: matches,
    norm: norm,
    makeSortableTable: makeSortableTable,
    debounce: debounce
  };
})();

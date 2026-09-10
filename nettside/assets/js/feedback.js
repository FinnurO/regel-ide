/*!
 * Tilbakemeldingswidget — kopiert fra FinnurO/tjenestedesign-no sitt mønster
 * (raw.githubusercontent.com/FinnurO/tjenestedesign-no/main/guide/index.html),
 * tilpasset kun i repo-navn og label. Samme oppførsel:
 * flytende knapp → <dialog> med automatisk kontekst (side-URL, tittel, ev.
 * markert tekst) → bygger en github.com/FinnurO/regel-ide/issues/new-URL og
 * åpner den i ny fane. Ingen backend, ingen egen tilstand.
 */
(function () {
  var fbOpenBtn = document.getElementById('feedback-open-btn');
  var fbDialog = document.getElementById('feedback-dialog');
  if (!fbOpenBtn || !fbDialog || typeof fbDialog.showModal !== 'function') return;

  var fbCancelBtn = document.getElementById('feedback-cancel-btn');
  var fbSubmitBtn = document.getElementById('feedback-submit-btn');
  var fbTextEl = document.getElementById('feedback-text');
  var fbCtxEl = document.getElementById('feedback-context');
  var fbPendingSelection = '';

  function fbGatherContext() {
    var openSections = Array.prototype.slice
      .call(document.querySelectorAll('main details[open] > summary'))
      .map(function (s) {
        var title = s.querySelector('.s-title');
        var text = title ? title.textContent : s.textContent;
        return text.trim().replace(/\s+/g, ' ');
      })
      .filter(Boolean);
    return { url: location.href, title: document.title, openSections: openSections, selection: fbPendingSelection };
  }

  function fbEsc(s) {
    var d = document.createElement('div');
    d.textContent = s;
    return d.innerHTML;
  }

  function fbRenderContext(ctx) {
    var lines = ['<div><strong>Side:</strong> ' + fbEsc(ctx.title) + '</div>'];
    if (ctx.openSections.length) {
      lines.push('<div><strong>Åpne seksjoner:</strong> ' + fbEsc(ctx.openSections.slice(0, 4).join(' › ')) + '</div>');
    }
    if (ctx.selection) {
      var sel = ctx.selection.length > 200 ? ctx.selection.slice(0, 200) + '…' : ctx.selection;
      lines.push('<div><strong>Merket tekst:</strong> «' + fbEsc(sel) + '»</div>');
    }
    fbCtxEl.innerHTML = lines.join('');
  }

  fbOpenBtn.addEventListener('click', function () {
    var sel = window.getSelection ? window.getSelection().toString().trim() : '';
    fbPendingSelection = sel;
    fbRenderContext(fbGatherContext());
    fbTextEl.value = '';
    fbDialog.showModal();
    fbTextEl.focus();
  });
  fbCancelBtn.addEventListener('click', function () { fbDialog.close(); });
  fbDialog.addEventListener('click', function (e) { if (e.target === fbDialog) fbDialog.close(); });
  fbSubmitBtn.addEventListener('click', function () {
    var ctx = fbGatherContext();
    var body = '**Side:** ' + ctx.url + '\n';
    if (ctx.openSections.length) { body += '**Åpne seksjoner:** ' + ctx.openSections.join(' › ') + '\n'; }
    if (ctx.selection) { body += '**Merket tekst:**\n> ' + ctx.selection.replace(/\n/g, '\n> ') + '\n'; }
    body += '\n---\n\n' + (fbTextEl.value.trim() || '_(ingen tekst skrevet inn)_');
    var title = 'Tilbakemelding: ' + ctx.title;
    var issueUrl = 'https://github.com/FinnurO/regel-ide/issues/new'
      + '?title=' + encodeURIComponent(title)
      + '&body=' + encodeURIComponent(body)
      + '&labels=' + encodeURIComponent('nettside-tilbakemelding');
    window.open(issueUrl, '_blank', 'noopener');
    fbDialog.close();
  });
})();

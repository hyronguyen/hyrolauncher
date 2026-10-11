(() => {
  const queue = [];
  let active = false;
  function showNext() {
    if (active || !queue.length) return;
    active = true;
    const { kind, message, initial, resolve } = queue.shift();
    const vi = document.documentElement.lang === 'vi' || document.body.dataset.language === 'vi';
    const previous = document.activeElement;
    const layer = document.createElement('div');
    layer.className = 'appDialogLayer';
    layer.innerHTML = `<form class="dialog appDialog" role="dialog" aria-modal="true" aria-labelledby="appDialogTitle"><h2 id="appDialogTitle">${kind === 'confirm' ? (vi ? 'Xác nhận' : 'Confirm') : kind === 'prompt' ? (vi ? 'Thêm danh mục' : 'Add category') : (vi ? 'Thông báo' : 'Notice')}</h2><p class="appDialogMessage"></p>${kind === 'prompt' ? '<input class="appDialogInput" aria-label="Category name" maxlength="100" required>' : ''}<div class="appDialogButtons">${kind !== 'alert' ? `<button type="button" class="ghost appDialogCancel">${vi ? 'Hủy' : 'Cancel'}</button>` : ''}<button type="submit" class="primary">${kind === 'prompt' ? (vi ? 'Thêm' : 'Add') : 'OK'}</button></div></form>`;
    layer.querySelector('.appDialogMessage').textContent = message;
    const input = layer.querySelector('input');
    if (input) input.value = initial || '';
    document.body.appendChild(layer);
    window.dispatchEvent(new Event('appdialogopen'));
    const finish = value => {
      layer.remove();
      active = false;
      if (previous && previous.isConnected) previous.focus();
      resolve(value);
      if (!queue.length) window.dispatchEvent(new Event('appdialogclose'));
      showNext();
    };
    layer.querySelector('form').onsubmit = event => {
      event.preventDefault();
      if (input && !input.value.trim()) { input.focus(); return; }
      finish(kind === 'prompt' ? input.value.trim() : true);
    };
    const cancel = layer.querySelector('.appDialogCancel');
    if (cancel) cancel.onclick = () => finish(kind === 'prompt' ? null : false);
    layer.onkeydown = event => {
      if (event.key === 'Escape') { event.preventDefault(); finish(kind === 'prompt' ? null : kind === 'alert'); }
      if (event.key === 'Tab') {
        const controls = [...layer.querySelectorAll('button, input')];
        const first = controls[0], last = controls[controls.length - 1];
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
      }
    };
    (input || cancel || layer.querySelector('button')).focus();
  }
  const open = (kind, message, initial) => new Promise(resolve => { queue.push({ kind, message: String(message || ''), initial, resolve }); showNext(); });
  window.appAlert = message => open('alert', message);
  window.appConfirm = message => open('confirm', message);
  window.appPrompt = (message, initial = '') => open('prompt', message, initial);
})();

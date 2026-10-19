(() => {
  'use strict';
  let toastTimer;
  const toast = message => {
    const node = document.getElementById('toast');
    clearTimeout(toastTimer);
    node.replaceChildren();
    const content = document.createElement('span');
    content.textContent = message;
    const dismiss = document.createElement('button');
    dismiss.type = 'button';
    dismiss.className = 'toast-dismiss';
    dismiss.setAttribute('aria-label', '关闭通知');
    dismiss.textContent = '×';
    const hide = () => { node.classList.remove('show'); node.hidden = true; };
    dismiss.addEventListener('click', hide);
    node.append(content, dismiss);
    node.setAttribute('role', 'status');
    node.setAttribute('aria-atomic', 'true');
    node.hidden = false;
    node.classList.add('show');
    const schedule = () => { clearTimeout(toastTimer); toastTimer = setTimeout(hide, 6000); };
    node.onmouseenter = node.onfocusin = () => clearTimeout(toastTimer);
    node.onmouseleave = node.onfocusout = schedule;
    schedule();
  };
  const enhance = root => {
    root.querySelectorAll('table').forEach(table => {
      const headers = [...table.querySelectorAll('thead th')].map(th => th.textContent.trim());
      if (!headers.length) return;
      table.classList.add('responsive-table');
      table.querySelectorAll('thead th').forEach(th => th.setAttribute('scope', 'col'));
      table.querySelectorAll('tbody tr').forEach(row => [...row.children].forEach((cell, index) => {
        cell.dataset.label = headers[index] || '';
      }));
    });
  };
  document.addEventListener('keydown', () => document.documentElement.classList.add('keyboard-input'), { capture: true });
  document.addEventListener('pointerdown', () => document.documentElement.classList.remove('keyboard-input'), { capture: true });
  window.PrototypeUI = { toast, enhance };
})();

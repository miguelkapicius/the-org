// Navegação do mockup + escala do palco 1600x900 (equivale ao PanelScaleMode.ScaleWithScreenSize da Unity).
const telas = [...document.querySelectorAll('.tela')];
const itens = [...document.querySelectorAll('.menu .item')];
const menu = document.getElementById('menu');

function ir(nome) {
  telas.forEach(t => (t.hidden = t.id !== nome));
  itens.forEach(i => i.classList.toggle('ativo', i.dataset.tela === nome));
  menu.hidden = nome === 'prematch';
}
document.querySelectorAll('[data-tela],[data-ir]').forEach(el =>
  el.addEventListener('click', () => ir(el.dataset.tela || el.dataset.ir)));

function escala() {
  const s = Math.min(innerWidth / 1600, innerHeight / 900);
  document.getElementById('palco').style.transform = `translate(-50%,-50%) scale(${s})`;
}
addEventListener('resize', escala); escala();
ir(location.hash.slice(1) || 'prematch');
